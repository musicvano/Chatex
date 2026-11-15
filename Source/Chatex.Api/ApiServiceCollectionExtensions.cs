using System.Text.Json;
using System.Text.Json.Serialization;
using Chatex.Api.Hubs;
using Chatex.Api.Infrastructure;
using Chatex.Business.Abstractions;
using Chatex.Business.Auth;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Tokens;

namespace Chatex.Api;

public static class ApiServiceCollectionExtensions
{
    /// <summary>The name of the bearer scheme in the OpenAPI document.</summary>
    public const string BearerSchemeName = "Bearer";

    /// <summary>
    /// Registers what the HTTP layer needs on top of <c>AddBusiness</c>: JSON, JWT auth, ProblemDetails, OpenAPI and
    /// the SignalR hub. Call it before <c>AddBusiness</c> so the SignalR <see cref="IChatNotifier"/> wins.
    /// Reads the optional <c>Presence</c> section.
    /// </summary>
    public static IServiceCollection AddApi(this IServiceCollection services)
    {
        services.ConfigureHttpJsonOptions(o =>
            o.SerializerOptions.Converters.Add(new JsonStringEnumConverter(JsonNamingPolicy.CamelCase)));

        services.AddSignalR().AddJsonProtocol(o =>
            o.PayloadSerializerOptions.Converters.Add(new JsonStringEnumConverter(JsonNamingPolicy.CamelCase)));
        services.AddOptions<PresenceOptions>()
            .BindConfiguration(PresenceOptions.Section)
            .ValidateDataAnnotations()
            .ValidateOnStart();
        services.AddSingleton<PresenceTracker>();
        services.AddSingleton<PresenceService>();
        services.AddSingleton<IUserIdProvider, SubClaimUserIdProvider>();
        services.AddSingleton<IChatNotifier, SignalRChatNotifier>();

        services.AddProblemDetails();

        // Malformed bodies answer 400 (turned into problem details by UseStatusCodePages) in every environment,
        // instead of throwing to the exception handler in Development.
        services.Configure<RouteHandlerOptions>(o => o.ThrowOnBadRequest = false);

        services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme).AddJwtBearer();
        services.AddOptions<JwtBearerOptions>(JwtBearerDefaults.AuthenticationScheme)
            .Configure<IOptions<JwtOptions>>((bearer, jwtOptions) =>
            {
                var jwt = jwtOptions.Value;
                bearer.MapInboundClaims = false;
                bearer.TokenValidationParameters = new TokenValidationParameters
                {
                    ValidIssuer = jwt.Issuer,
                    ValidAudience = jwt.Audience,
                    IssuerSigningKey = jwt.CreateSigningKey(),
                    ValidAlgorithms = [SecurityAlgorithms.HmacSha256],
                    NameClaimType = JwtRegisteredClaimNames.UniqueName,
                    ClockSkew = TimeSpan.FromSeconds(30),
                };

                // Browsers cannot send headers on WebSocket and SSE requests, so the SignalR client puts the token
                // in the query string. Accept it there only for hub paths, to keep tokens out of other URLs and logs.
                bearer.Events = new JwtBearerEvents
                {
                    OnMessageReceived = context =>
                    {
                        var token = context.Request.Query["access_token"].ToString();
                        if (token.Length > 0 && context.HttpContext.Request.Path.StartsWithSegments("/hubs"))
                        {
                            context.Token = token;
                        }

                        return Task.CompletedTask;
                    },
                };
            });
        services.AddAuthorization();

        services.Configure<ForwardedHeadersOptions>(o =>
            o.ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto);

        services.AddOpenApi(o =>
        {
            o.AddDocumentTransformer<BearerSecuritySchemeTransformer>();
            o.AddOperationTransformer<BearerSecuritySchemeTransformer>();
        });

        return services;
    }
}
