using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace Chatex.DataAccess;

/// <summary>Used by <c>dotnet ef</c> only. Reads the connection string from the CHATEX_DB environment variable.</summary>
internal sealed class ChatexDbContextFactory : IDesignTimeDbContextFactory<ChatexDbContext>
{
    private const string DevConnectionString = "Host=localhost;Port=5432;Database=chatex;Username=postgres;Password=postgres";

    public ChatexDbContext CreateDbContext(string[] args)
    {
        var connectionString = Environment.GetEnvironmentVariable("CHATEX_DB") ?? DevConnectionString;
        var options = new DbContextOptionsBuilder<ChatexDbContext>();
        DependencyInjection.Configure(options, connectionString);
        return new ChatexDbContext(options.Options);
    }
}
