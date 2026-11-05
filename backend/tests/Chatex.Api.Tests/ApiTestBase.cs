using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using Chatex.Api.Contracts;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;

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
            .UseSetting("Attachments:MaxBytes", MaxAttachmentBytes.ToString()));
    }

    public async ValueTask DisposeAsync()
    {
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
