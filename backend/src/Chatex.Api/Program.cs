using Chatex.Business;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddBusiness(builder.Configuration);

var app = builder.Build();

app.MapGet("/health", () => Results.Ok());

app.Run();
