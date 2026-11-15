using System.Net;
using Microsoft.AspNetCore.Hosting;

namespace Chatex.Api.Tests;

public sealed class HealthTests(PostgresFixture fixture) : ApiTestBase(fixture)
{
    [Fact]
    public async Task Health_answers_200_without_a_token_when_the_database_is_reachable()
    {
        var response = await CreateClient().GetAsync("/health", Ct);

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        (await response.Content.ReadAsStringAsync(Ct)).ShouldBe("Healthy");
    }
}

public sealed class HealthWithoutDatabaseTests(PostgresFixture fixture) : ApiTestBase(fixture)
{
    // Nothing listens on port 1, so every connection attempt fails at once.
    protected override void ConfigureHost(IWebHostBuilder builder) =>
        builder.UseSetting("ConnectionStrings:Chatex", "Host=127.0.0.1;Port=1;Database=chatex;Username=x;Password=x;Timeout=2");

    [Fact]
    public async Task Health_answers_503_when_the_database_is_unreachable()
    {
        var response = await CreateClient().GetAsync("/health", Ct);

        response.StatusCode.ShouldBe(HttpStatusCode.ServiceUnavailable);
    }
}
