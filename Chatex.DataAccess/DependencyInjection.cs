using Chatex.DataAccess.Abstractions;
using Chatex.DataAccess.Entities;
using Chatex.DataAccess.Interceptors;
using Chatex.DataAccess.Repositories;
using Chatex.DataAccess.Storage;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Chatex.DataAccess;

public static class DependencyInjection
{
    /// <summary>
    /// Registers the DbContext, repositories, unit of work and the Identity stores for <see cref="User"/>.
    /// Identity options (password rules, lockout) and token providers are configured by the caller.
    /// </summary>
    public static IServiceCollection AddDataAccess(
        this IServiceCollection services, string connectionString, Action<FileStorageOptions> configureStorage)
    {
        services.TryAddSingleton(TimeProvider.System);
        services.AddSingleton<TimestampInterceptor>();

        services.AddOptions<FileStorageOptions>()
            .Configure(configureStorage)
            .Validate(o => !string.IsNullOrWhiteSpace(o.RootPath), "Storage root path is not configured.")
            .ValidateOnStart();
        services.AddSingleton<IFileStorage, LocalFileStorage>();

        services.AddDbContext<ChatexDbContext>((sp, options) =>
        {
            Configure(options, connectionString);
            options.AddInterceptors(sp.GetRequiredService<TimestampInterceptor>());
        });

        services.AddIdentityCore<User>()
            .AddRoles<IdentityRole<Guid>>()
            .AddEntityFrameworkStores<ChatexDbContext>();

        services.AddScoped<IUnitOfWork, UnitOfWork>();
        services.AddScoped<IUserRepository, UserRepository>();
        services.AddScoped<IRefreshTokenRepository, RefreshTokenRepository>();
        services.AddScoped<IChatRepository, ChatRepository>();
        services.AddScoped<IChatMemberRepository, ChatMemberRepository>();
        services.AddScoped<IMessageRepository, MessageRepository>();
        services.AddScoped<IAttachmentRepository, AttachmentRepository>();

        return services;
    }

    internal static void Configure(DbContextOptionsBuilder options, string connectionString) =>
        options.UseNpgsql(connectionString).UseSnakeCaseNamingConvention();
}
