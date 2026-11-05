using System.Net;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using Chatex.Api.Contracts;
using Chatex.Business.Users;

namespace Chatex.Api.Tests;

public sealed class AuthEndpointsTests(PostgresFixture fixture) : ApiTestBase(fixture)
{
    private const string Cookie = "chatex_refresh";

    [Fact]
    public async Task Register_returns_access_token_and_sets_refresh_cookie()
    {
        var client = CreateClient();

        var response = await client.PostAsJsonAsync("/api/auth/register", RegisterBody("alice"), Json, Ct);

        await response.ShouldHaveStatusAsync(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>(Ct);
        body.GetProperty("accessToken").GetString().ShouldNotBeNullOrEmpty();
        body.GetProperty("user").GetProperty("userName").GetString().ShouldBe("alice");
        body.TryGetProperty("refreshToken", out _).ShouldBeFalse();

        var cookie = response.SetCookie(Cookie).ShouldNotBeNull().ToLowerInvariant();
        cookie.ShouldContain("httponly");
        cookie.ShouldContain("secure");
        cookie.ShouldContain("samesite=strict");
        cookie.ShouldContain("path=/api/auth");
    }

    [Fact]
    public async Task Access_token_authenticates_requests()
    {
        var alice = await RegisterAsync("alice");

        var me = await (await alice.Client.GetAsync("/api/me", Ct)).ShouldHaveAsync<MyProfileDto>(HttpStatusCode.OK);

        me.Id.ShouldBe(alice.UserId);
        me.Email.ShouldBe("alice@example.test");
    }

    [Fact]
    public async Task Requests_without_a_token_get_401_problem()
    {
        var response = await CreateClient().GetAsync("/api/me", Ct);

        await response.ShouldBeProblemAsync(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Login_accepts_user_name_and_rejects_a_wrong_password()
    {
        await RegisterAsync("alice");
        var client = CreateClient();

        var ok = await client.PostAsJsonAsync("/api/auth/login", new { login = "alice", password = Password }, Json, Ct);
        var wrong = await client.PostAsJsonAsync("/api/auth/login", new { login = "alice", password = "nope12345" }, Json, Ct);

        (await ok.ShouldHaveAsync<AuthResponse>(HttpStatusCode.OK)).User.UserName.ShouldBe("alice");
        ok.SetCookie(Cookie).ShouldNotBeNull();
        await wrong.ShouldBeProblemAsync(HttpStatusCode.Unauthorized, "invalid_credentials");
    }

    [Fact]
    public async Task Refresh_rotates_the_cookie_and_a_reused_token_is_rejected()
    {
        var client = CreateClient();
        var register = await client.PostAsJsonAsync("/api/auth/register", RegisterBody("alice"), Json, Ct);
        var firstToken = HttpAssertions.CookieValue(register.SetCookie(Cookie).ShouldNotBeNull());

        var refreshed = await client.PostAsync("/api/auth/refresh", null, Ct);

        var auth = await refreshed.ShouldHaveAsync<AuthResponse>(HttpStatusCode.OK);
        auth.User.UserName.ShouldBe("alice");
        HttpAssertions.CookieValue(refreshed.SetCookie(Cookie).ShouldNotBeNull()).ShouldNotBe(firstToken);

        var replay = new HttpRequestMessage(HttpMethod.Post, "/api/auth/refresh");
        replay.Headers.Add("Cookie", $"{Cookie}={firstToken}");
        var reused = await CreateClient().SendAsync(replay, Ct);
        await reused.ShouldBeProblemAsync(HttpStatusCode.Unauthorized, "invalid_refresh_token");

        // Reuse signs the user out everywhere, so the newest cookie stops working too.
        var afterReuse = await client.PostAsync("/api/auth/refresh", null, Ct);
        await afterReuse.ShouldBeProblemAsync(HttpStatusCode.Unauthorized, "invalid_refresh_token");
    }

    [Fact]
    public async Task Refresh_without_a_cookie_is_401()
    {
        var response = await CreateClient().PostAsync("/api/auth/refresh", null, Ct);

        await response.ShouldBeProblemAsync(HttpStatusCode.Unauthorized, "invalid_refresh_token");
    }

    [Fact]
    public async Task Logout_clears_the_cookie_and_ends_the_session()
    {
        var client = CreateClient();
        var register = await client.PostAsJsonAsync("/api/auth/register", RegisterBody("alice"), Json, Ct);
        var token = HttpAssertions.CookieValue(register.SetCookie(Cookie).ShouldNotBeNull());

        var logout = await client.PostAsync("/api/auth/logout", null, Ct);

        await logout.ShouldHaveStatusAsync(HttpStatusCode.NoContent);
        logout.SetCookie(Cookie).ShouldNotBeNull().ShouldContain("expires=Thu, 01 Jan 1970", Case.Insensitive);

        var replay = new HttpRequestMessage(HttpMethod.Post, "/api/auth/refresh");
        replay.Headers.Add("Cookie", $"{Cookie}={token}");
        await (await CreateClient().SendAsync(replay, Ct)).ShouldBeProblemAsync(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Duplicate_user_name_is_409()
    {
        await RegisterAsync("alice");

        var response = await CreateClient().PostAsJsonAsync("/api/auth/register", RegisterBody("alice"), Json, Ct);

        await response.ShouldBeProblemAsync(HttpStatusCode.Conflict, "user_name_taken");
    }

    [Fact]
    public async Task Invalid_fields_are_400_with_per_field_errors()
    {
        var body = new { userName = "a", email = "not-an-email", password = Password, displayName = "A" };

        var response = await CreateClient().PostAsJsonAsync("/api/auth/register", body, Json, Ct);

        var problem = await response.ShouldBeProblemAsync(HttpStatusCode.BadRequest, "validation_failed");
        var errors = problem.GetProperty("errors");
        errors.TryGetProperty("userName", out _).ShouldBeTrue();
        errors.TryGetProperty("email", out _).ShouldBeTrue();
    }

    [Fact]
    public async Task Malformed_json_is_400_problem()
    {
        var content = new StringContent("{ not json", Encoding.UTF8, "application/json");

        var response = await CreateClient().PostAsync("/api/auth/login", content, Ct);

        await response.ShouldBeProblemAsync(HttpStatusCode.BadRequest);
    }
}
