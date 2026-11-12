using Chatex.Business.Abstractions;
using Chatex.Business.Attachments;
using Chatex.Business.Auth;
using Chatex.Business.Chats;
using Chatex.Business.Common;
using Chatex.Business.Messages;
using Chatex.Business.Users;
using Chatex.DataAccess.Abstractions;
using Chatex.DataAccess.Entities;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Time.Testing;

// Tests share one database that is wiped before each test; xunit.runner.json turns off parallel runs.
[assembly: AssemblyFixture(typeof(PostgresFixture))]

namespace Chatex.Business.Tests;

/// <summary>
/// The full business layer on a clean database, with a controllable clock, in-memory files and a recording notifier.
/// Every service call runs in its own scope, like separate HTTP requests.
/// </summary>
public abstract class BusinessTestBase(PostgresFixture fixture) : DatabaseTest(fixture)
{
    public const string Password = "Passw0rd!";
    public const string Issuer = "chatex-tests";
    public const string SigningKey = "test-signing-key-that-is-long-enough-for-hmac";
    public const long MaxAttachmentBytes = 1024;

    protected FakeTimeProvider Time { get; } = new(DateTimeOffset.UtcNow);

    protected InMemoryFileStorage Files { get; } = new();

    protected RecordingNotifier Notifier { get; } = new();

    protected override void ConfigureServices(IServiceCollection services, string connectionString)
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["ConnectionStrings:Chatex"] = connectionString,
                ["Storage:RootPath"] = StorageRoot,
                ["Jwt:Issuer"] = Issuer,
                ["Jwt:Audience"] = Issuer,
                ["Jwt:SigningKey"] = SigningKey,
                ["Attachments:MaxBytes"] = MaxAttachmentBytes.ToString(),
            })
            .Build();

        services.AddSingleton<TimeProvider>(Time);
        services.AddSingleton<IChatNotifier>(Notifier);
        services.AddBusiness(configuration);
        services.Replace(ServiceDescriptor.Singleton<IFileStorage>(Files));
    }

    protected Task<T> Auth<T>(Func<IAuthService, Task<T>> call) => Use(call);

    protected Task<T> Profiles<T>(Func<IProfileService, Task<T>> call) => Use(call);

    protected Task<T> Chats<T>(Func<IChatService, Task<T>> call) => Use(call);

    protected Task<T> Messages<T>(Func<IMessageService, Task<T>> call) => Use(call);

    protected Task<T> Attachments<T>(Func<IAttachmentService, Task<T>> call) => Use(call);

    /// <summary>Registers <paramref name="userName"/> and returns the signed-in session.</summary>
    protected async Task<AuthResult> RegisterAsync(string userName)
    {
        var result = await Auth(s => s.RegisterAsync(
            new RegisterCommand(userName, $"{userName}@example.test", Password, $"{userName} display"), ClientInfo.Unknown, Ct));
        return result.ShouldSucceed();
    }

    protected async Task<Guid> RegisterUserAsync(string userName) => (await RegisterAsync(userName)).User.Id;

    protected async Task<ChatDto> CreateChatAsync(
        Guid ownerId, ChatType type, string? handle = null, params Guid[] memberIds)
    {
        var result = await Chats(s => s.CreateAsync(ownerId, new CreateChatCommand(type, $"{type} chat", null, handle, memberIds), Ct));
        return result.ShouldSucceed();
    }

    protected async Task<MessageDto> SendAsync(Guid userId, Guid chatId, string body, params Guid[] attachmentIds)
    {
        var result = await Messages(s => s.SendAsync(userId, chatId, new SendMessageCommand(body, null, attachmentIds), Ct));
        return result.ShouldSucceed();
    }

    protected async Task<AttachmentDto> UploadAsync(Guid userId, int size = 100, string contentType = "image/png")
    {
        var result = await Attachments(s => s.UploadAsync(userId, Upload(size, contentType), Ct));
        return result.ShouldSucceed();
    }

    protected static FileUpload Upload(int size, string contentType, long? claimedLength = null) =>
        new(new MemoryStream(new byte[size]), "photo.png", contentType, claimedLength ?? size);

    private async Task<T> Use<TService, T>(Func<TService, Task<T>> call) where TService : notnull
    {
        await using var scope = NewScope();
        return await call(Get<TService>(scope));
    }
}

internal static class ResultAssertions
{
    public static T ShouldSucceed<T>(this Result<T> result)
    {
        result.IsSuccess.ShouldBeTrue($"Expected success but got {result.Error}");
        return result.Value;
    }

    public static void ShouldSucceed(this Result result) =>
        result.IsSuccess.ShouldBeTrue($"Expected success but got {result.Error}");

    public static Error ShouldFail<T>(this Result<T> result, ErrorKind kind, string code)
    {
        result.IsSuccess.ShouldBeFalse("Expected a failure but the operation succeeded.");
        result.Error.Kind.ShouldBe(kind);
        result.Error.Code.ShouldBe(code);
        return result.Error;
    }

    public static Error ShouldFail(this Result result, ErrorKind kind, string code)
    {
        result.IsSuccess.ShouldBeFalse("Expected a failure but the operation succeeded.");
        result.Error.Kind.ShouldBe(kind);
        result.Error.Code.ShouldBe(code);
        return result.Error;
    }
}
