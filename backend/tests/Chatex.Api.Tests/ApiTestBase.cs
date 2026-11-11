using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using Chatex.Api.Contracts;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http.Connections;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.SignalR.Client;
using Microsoft.Extensions.DependencyInjection;

// Tests share one database that is wiped before each test; xunit.runner.json turns off parallel runs.
[assembly: AssemblyFixture(typeof(PostgresFixture))]

namespace Chatex.Api.Tests;

/// <summary>
/// The real Api in memory (TestServer) on a clean database and a temporary upload folder.
/// Clients talk to https://localhost so the Secure refresh cookie round-trips.
/// </summary>
public abstract class ApiTestBase(PostgresFixture fixture) : IAsyncLifetime
{
    public const string Password = "Passw0rd!";
    public const long MaxAttachmentBytes = 1024;

    protected static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web)
    {
        Converters = { new JsonStringEnumConverter(JsonNamingPolicy.CamelCase) },
    };

    /// <summary>The first bytes of a PNG file, enough for the avatar type sniffing.</summary>
    protected static readonly byte[] PngBytes = [0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A, 0, 0, 0, 13, 1, 2, 3, 4];

    private readonly string _storageRoot = Path.Combine(Path.GetTempPath(), "chatex-api-tests", Guid.NewGuid().ToString("N"));
    private readonly List<HubClient> _hubs = [];
    private WebApplicationFactory<Program> _factory = null!;

    protected static CancellationToken Ct => TestContext.Current.CancellationToken;

    public async ValueTask InitializeAsync()
    {
        await fixture.ResetAsync();
        _factory = new WebApplicationFactory<Program>().WithWebHostBuilder(builder => builder
            .UseEnvironment("Testing")
            .UseSetting("ConnectionStrings:Chatex", fixture.ConnectionString)
            .UseSetting("Jwt:Issuer", "chatex-api-tests")
            .UseSetting("Jwt:Audience", "chatex-api-tests")
            .UseSetting("Jwt:SigningKey", "api-test-signing-key-that-is-long-enough-for-hmac")
            .UseSetting("Storage:RootPath", _storageRoot)
            .UseSetting("Attachments:MaxBytes", MaxAttachmentBytes.ToString())
            .UseSetting("Presence:OfflineGraceSeconds", "0"));
    }

    public async ValueTask DisposeAsync()
    {
        // Close hub connections while the server is still up.
        foreach (var hub in _hubs)
        {
            await hub.DisposeAsync();
        }

        await _factory.DisposeAsync();
        if (Directory.Exists(_storageRoot))
        {
            Directory.Delete(_storageRoot, recursive: true);
        }

        GC.SuppressFinalize(this);
    }

    /// <summary>A client with its own cookie jar, like a separate browser.</summary>
    protected HttpClient CreateClient() =>
        _factory.CreateClient(new WebApplicationFactoryClientOptions { BaseAddress = new Uri("https://localhost") });

    /// <summary>Registers <paramref name="userName"/> and returns a client that sends its access token.</summary>
    protected async Task<Session> RegisterAsync(string userName)
    {
        var client = CreateClient();
        var response = await client.PostAsJsonAsync("/api/auth/register", RegisterBody(userName), Json, Ct);
        var auth = await response.ShouldHaveAsync<AuthResponse>(HttpStatusCode.OK);
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", auth.AccessToken);
        return new Session(client, auth);
    }

    /// <summary>
    /// Opens a hub connection as <paramref name="session"/> (or anonymously) through the in-memory server.
    /// Returns once the server has joined the user's chat groups.
    /// </summary>
    protected async Task<HubClient> ConnectHubAsync(Session? session)
    {
        var hub = new HubClient(CreateHubConnection(session?.Auth.AccessToken));
        _hubs.Add(hub);
        await hub.Connection.StartAsync(Ct);

        // The handshake completes before OnConnectedAsync, but invocations wait for it, so this one marks the point
        // where the connection is in its groups.
        await hub.Connection.InvokeAsync<Guid[]>("GetOnlineUsers", Array.Empty<Guid>(), Ct);
        return hub;
    }

    protected HubConnection CreateHubConnection(string? accessToken) =>
        new HubConnectionBuilder()
            .WithUrl(new Uri("https://localhost/hubs/chat"), o =>
            {
                o.HttpMessageHandlerFactory = _ => _factory.Server.CreateHandler();
                o.Transports = HttpTransportType.LongPolling;
                o.AccessTokenProvider = () => Task.FromResult(accessToken);
            })
            .AddJsonProtocol(o => o.PayloadSerializerOptions.Converters.Add(new JsonStringEnumConverter(JsonNamingPolicy.CamelCase)))
            .Build();

    protected static object RegisterBody(string userName) => new
    {
        userName,
        email = $"{userName}@example.test",
        password = Password,
        displayName = $"{userName} display",
    };

    protected static MultipartFormDataContent FileContent(byte[] bytes, string fileName, string contentType)
    {
        var file = new ByteArrayContent(bytes);
        file.Headers.ContentType = new MediaTypeHeaderValue(contentType);
        return new MultipartFormDataContent { { file, "file", fileName } };
    }
}

public sealed record Session(HttpClient Client, AuthResponse Auth)
{
    public Guid UserId => Auth.User.Id;
}
