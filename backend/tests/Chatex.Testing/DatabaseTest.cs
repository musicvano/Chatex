using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Chatex.Testing;

/// <summary>
/// Base for tests against <see cref="PostgresFixture"/>: wipes the database and builds a fresh service provider
/// (so fakes such as a test clock never leak between tests) before each test.
/// </summary>
public abstract class DatabaseTest(PostgresFixture fixture) : IAsyncLifetime
{
    private ServiceProvider _services = null!;

    protected static CancellationToken Ct => TestContext.Current.CancellationToken;

    /// <summary>A temporary directory for file storage, deleted after the test.</summary>
    protected string StorageRoot { get; } = Path.Combine(Path.GetTempPath(), "chatex-tests", Guid.NewGuid().ToString("N"));

    protected abstract void ConfigureServices(IServiceCollection services, string connectionString);

    public async ValueTask InitializeAsync()
    {
        await fixture.ResetAsync();

        var services = new ServiceCollection();
        services.AddLogging();
        ConfigureServices(services, fixture.ConnectionString);
        _services = services.BuildServiceProvider(validateScopes: true);
    }

    public async ValueTask DisposeAsync()
    {
        await _services.DisposeAsync();
        if (Directory.Exists(StorageRoot))
        {
            Directory.Delete(StorageRoot, recursive: true);
        }

        GC.SuppressFinalize(this);
    }

    /// <summary>A fresh scope, i.e. a fresh DbContext with nothing tracked.</summary>
    protected AsyncServiceScope NewScope() => _services.CreateAsyncScope();

    protected static T Get<T>(AsyncServiceScope scope) where T : notnull =>
        scope.ServiceProvider.GetRequiredService<T>();

    /// <summary>Resolves a singleton (e.g. a fake) from the root provider.</summary>
    protected T GetSingleton<T>() where T : notnull => _services.GetRequiredService<T>();
}
