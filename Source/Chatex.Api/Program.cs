using Chatex.Api;
using Chatex.Api.Endpoints;
using Chatex.Api.Hubs;
using Chatex.Api.Infrastructure;
using Chatex.Core;
using Scalar.AspNetCore;

var builder = WebApplication.CreateBuilder(args);

// AddApi first: its SignalR IChatNotifier must be registered before AddCore falls back to the no-op one.
builder.Services.AddApi(builder.Configuration);
builder.Services.AddCore(builder.Configuration);

var app = builder.Build();

app.UseForwardedHeaders();
app.UseExceptionHandler();
app.UseStatusCodePages();

app.UseAuthentication();
app.UseAuthorization();
app.UseRateLimiter();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
    app.MapScalarApiReference(o => o.AddPreferredSecuritySchemes(BearerSecuritySchemeTransformer.SchemeName));
}

// Readiness: 200 when the API can reach the database, 503 otherwise (the deploy rolls back on 503).
app.MapHealthChecks("/health").AllowAnonymous();

var api = app.MapGroup("/api").RequireAuthorization();
api.MapAuthEndpoints();
api.MapProfileEndpoints();
api.MapUserEndpoints();
api.MapChatEndpoints();
api.MapMessageEndpoints();
api.MapAttachmentEndpoints();

app.MapHub<ChatHub>("/hubs/chat");

app.Run();

/// <summary>Visible to <c>WebApplicationFactory</c> in the Api tests.</summary>
public partial class Program;
