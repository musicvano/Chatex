using Chatex.Api;
using Chatex.Api.Endpoints;
using Chatex.Api.Hubs;
using Chatex.Business;
using Scalar.AspNetCore;

var builder = WebApplication.CreateBuilder(args);

// AddApi first: its SignalR IChatNotifier must be registered before AddBusiness falls back to the no-op one.
builder.Services.AddApi();
builder.Services.AddBusiness(builder.Configuration);

var app = builder.Build();

app.UseForwardedHeaders();
app.UseExceptionHandler();
app.UseStatusCodePages();

app.UseAuthentication();
app.UseAuthorization();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
    app.MapScalarApiReference(o => o.AddPreferredSecuritySchemes(ApiServiceCollectionExtensions.BearerSchemeName));
}

app.MapGet("/health", () => Results.Ok()).AllowAnonymous().ExcludeFromDescription();

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
