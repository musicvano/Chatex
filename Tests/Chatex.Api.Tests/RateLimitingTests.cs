using System.Globalization;
using System.Net;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Hosting;

namespace Chatex.Api.Tests;

public sealed class RateLimitingTests(PostgresFixture fixture) : ApiTestBase(fixture)
{
    private const int Limit = 3;

    protected override void ConfigureHost(IWebHostBuilder builder) =>
        builder.UseSetting("RateLimiting:AuthPermitLimit", Limit.ToString(CultureInfo.InvariantCulture));

    [Fact]
    public async Task Auth_endpoints_answer_429_problem_with_retry_after_once_the_limit_is_used_up()
    {
        var client = CreateClient();
        for (var i = 0; i < Limit; i++)
        {
            var allowed = await client.PostAsJsonAsync("/api/auth/login", new { login = "nobody", password = Password }, Json, Ct);
            allowed.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
        }

        var throttled = await client.PostAsJsonAsync("/api/auth/login", new { login = "nobody", password = Password }, Json, Ct);

        await throttled.ShouldBeProblemAsync(HttpStatusCode.TooManyRequests, "too_many_requests");
        throttled.Headers.RetryAfter.ShouldNotBeNull();
    }

    [Fact]
    public async Task Other_endpoints_are_not_throttled()
    {
        var alice = await RegisterAsync("alice");

        for (var i = 0; i < Limit + 2; i++)
        {
            (await alice.Client.GetAsync("/api/me", Ct)).StatusCode.ShouldBe(HttpStatusCode.OK);
        }
    }
}
