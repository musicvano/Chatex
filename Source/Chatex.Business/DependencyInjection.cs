using Chatex.Business.Abstractions;
using Chatex.Business.Attachments;
using Chatex.Business.Auth;
using Chatex.Business.Chats;
using Chatex.Business.Common;
using Chatex.Business.Messages;
using Chatex.Business.Users;
using Chatex.DataAccess;
using FluentValidation;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Chatex.Business;

public static class DependencyInjection
{
    /// <summary>
    /// Registers the business services and, beneath them, the data access layer. Reads the <c>Chatex</c>
    /// connection string, <c>Storage:RootPath</c>, and the <c>Jwt</c> and <c>Attachments</c> sections.
    /// Register your own <see cref="IChatNotifier"/> (SignalR) before calling this to replace the no-op one.
    /// </summary>
    public static IServiceCollection AddBusiness(this IServiceCollection services, IConfiguration configuration)
    {
        var connectionString = configuration.GetConnectionString("Chatex")
            ?? throw new InvalidOperationException("Connection string 'Chatex' is not configured.");

        services.AddDataAccess(connectionString, o => o.RootPath = configuration["Storage:RootPath"] ?? "");

        services.AddOptions<JwtOptions>()
            .Bind(configuration.GetSection(JwtOptions.Section))
            .ValidateDataAnnotations()
            .ValidateOnStart();
        services.AddOptions<AttachmentOptions>()
            .Bind(configuration.GetSection(AttachmentOptions.Section))
            .ValidateDataAnnotations()
            .ValidateOnStart();

        services.Configure<IdentityOptions>(o =>
        {
            o.User.RequireUniqueEmail = true;
            o.User.AllowedUserNameCharacters = "abcdefghijklmnopqrstuvwxyz0123456789_";
            o.Password.RequiredLength = 8;
            o.Password.RequireDigit = true;
            o.Password.RequireLowercase = false;
            o.Password.RequireUppercase = false;
            o.Password.RequireNonAlphanumeric = false;
            o.Lockout.MaxFailedAccessAttempts = 5;
            o.Lockout.DefaultLockoutTimeSpan = TimeSpan.FromMinutes(15);
        });

        services.AddValidatorsFromAssemblyContaining<AuthService>(includeInternalTypes: true);

        services.TryAddSingleton<IChatNotifier, NullChatNotifier>();
        services.AddSingleton<ITokenService, TokenService>();
        services.AddScoped<AvatarStore>();
        services.AddScoped<ChatAccess>();
        services.AddScoped<IAuthService, AuthService>();
        services.AddScoped<IProfileService, ProfileService>();
        services.AddScoped<IChatService, ChatService>();
        services.AddScoped<IMessageService, MessageService>();
        services.AddScoped<IAttachmentService, AttachmentService>();

        return services;
    }
}
