using System.Text.Json;
using System.Text.Json.Serialization;
using Chatex.Api.Infrastructure;
using Chatex.Business.Auth;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Tokens;

namespace Chatex.Api;

public static class ApiServiceCollectionExtensions
{
    /// <summary>The name of the bearer scheme in the OpenAPI document.</summary>
    public const string BearerSchemeName = "Bearer";

    /// <summary>Registers what the HTTP layer needs on top of <c>AddBusiness</c>: JSON, JWT auth, ProblemDetails, OpenAPI.</summary>
    public static IServiceCollection AddApi(this IServiceCollection services)
    {
        services.ConfigureHttpJsonOptions(o =>
            o.SerializerOptions.Converters.Add(new JsonStringEnumConverter(JsonNamingPolicy.CamelCase)));

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
