using System.Text;
using Chatex.Business.Auth;
using Chatex.Business.Common;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Tokens;

namespace Chatex.Business.Tests;

public sealed class AuthServiceTests(PostgresFixture fixture) : BusinessTestBase(fixture)
{
    [Fact]
    public async Task Register_signs_the_user_in_and_creates_the_profile()
    {
        var result = await Auth(s => s.RegisterAsync(
            new RegisterCommand("Alice_1", "alice@example.test", Password, "  Alice  "), ClientInfo.Unknown, Ct));

        var session = result.ShouldSucceed();
        session.AccessToken.ShouldNotBeNullOrEmpty();
        session.RefreshToken.ShouldNotBeNullOrEmpty();
        session.User.UserName.ShouldBe("alice_1");
        session.User.DisplayName.ShouldBe("Alice");
        session.RefreshTokenExpiresAt.ShouldBe(Time.GetUtcNow() + TimeSpan.FromDays(30));
    }

    [Fact]
    public async Task Register_rejects_a_taken_user_name_in_any_case()
    {
        await RegisterAsync("alice");

        var result = await Auth(s => s.RegisterAsync(
            new RegisterCommand("ALICE", "other@example.test", Password, "Other"), ClientInfo.Unknown, Ct));

        result.ShouldFail(ErrorKind.Conflict, "user_name_taken");
    }

    [Fact]
    public async Task Register_rejects_a_weak_password_and_an_invalid_user_name()
    {
        var weak = await Auth(s => s.RegisterAsync(
            new RegisterCommand("alice", "alice@example.test", "short", "Alice"), ClientInfo.Unknown, Ct));
        var badName = await Auth(s => s.RegisterAsync(
            new RegisterCommand("1alice!", "alice@example.test", Password, "Alice"), ClientInfo.Unknown, Ct));

        weak.ShouldFail(ErrorKind.Validation, "validation_failed").Details.ShouldNotBeNull().ShouldContainKey("password");
        badName.ShouldFail(ErrorKind.Validation, "validation_failed").Details.ShouldNotBeNull().ShouldContainKey("userName");
    }

    [Fact]
    public async Task Login_works_with_user_name_or_email()
    {
        var alice = await RegisterUserAsync("alice");

        var byName = await Auth(s => s.LoginAsync(new LoginCommand("Alice", Password), ClientInfo.Unknown, Ct));
        var byEmail = await Auth(s => s.LoginAsync(new LoginCommand("alice@example.test", Password), ClientInfo.Unknown, Ct));

        byName.ShouldSucceed().User.Id.ShouldBe(alice);
        byEmail.ShouldSucceed().User.Id.ShouldBe(alice);
    }

    [Fact]
    public async Task Login_locks_the_account_after_five_wrong_passwords()
    {
        await RegisterAsync("alice");

        for (var attempt = 1; attempt <= 4; attempt++)
        {
            (await Auth(s => s.LoginAsync(new LoginCommand("alice", "Wrong1234"), ClientInfo.Unknown, Ct)))
                .ShouldFail(ErrorKind.Unauthorized, "invalid_credentials");
        }

        (await Auth(s => s.LoginAsync(new LoginCommand("alice", "Wrong1234"), ClientInfo.Unknown, Ct)))
            .ShouldFail(ErrorKind.Unauthorized, "locked_out");
        (await Auth(s => s.LoginAsync(new LoginCommand("alice", Password), ClientInfo.Unknown, Ct)))
            .ShouldFail(ErrorKind.Unauthorized, "locked_out");
    }

    [Fact]
    public async Task Login_does_not_reveal_whether_the_user_exists()
    {
        var result = await Auth(s => s.LoginAsync(new LoginCommand("nobody", Password), ClientInfo.Unknown, Ct));

        result.ShouldFail(ErrorKind.Unauthorized, "invalid_credentials");
    }

    [Fact]
    public async Task Refresh_issues_a_new_pair_and_retires_the_old_token()
    {
        var first = await RegisterAsync("alice");

        var second = (await Auth(s => s.RefreshAsync(new RefreshCommand(first.RefreshToken), ClientInfo.Unknown, Ct))).ShouldSucceed();
        var third = await Auth(s => s.RefreshAsync(new RefreshCommand(second.RefreshToken), ClientInfo.Unknown, Ct));

        second.RefreshToken.ShouldNotBe(first.RefreshToken);
        second.User.Id.ShouldBe(first.User.Id);
        third.ShouldSucceed();
    }

    [Fact]
    public async Task Reusing_a_refresh_token_signs_the_user_out_everywhere()
    {
        var first = await RegisterAsync("alice");
        var second = (await Auth(s => s.RefreshAsync(new RefreshCommand(first.RefreshToken), ClientInfo.Unknown, Ct))).ShouldSucceed();

        var replay = await Auth(s => s.RefreshAsync(new RefreshCommand(first.RefreshToken), ClientInfo.Unknown, Ct));
        var afterReplay = await Auth(s => s.RefreshAsync(new RefreshCommand(second.RefreshToken), ClientInfo.Unknown, Ct));

        replay.ShouldFail(ErrorKind.Unauthorized, "invalid_refresh_token");
        afterReplay.ShouldFail(ErrorKind.Unauthorized, "invalid_refresh_token");
    }

    [Fact]
    public async Task Refresh_fails_once_the_token_expired()
    {
        var session = await RegisterAsync("alice");

        Time.Advance(TimeSpan.FromDays(31));
        var result = await Auth(s => s.RefreshAsync(new RefreshCommand(session.RefreshToken), ClientInfo.Unknown, Ct));

        result.ShouldFail(ErrorKind.Unauthorized, "invalid_refresh_token");
    }

    [Fact]
    public async Task Logout_ends_the_session()
    {
        var session = await RegisterAsync("alice");

        (await Auth(s => s.LogoutAsync(new RefreshCommand(session.RefreshToken), Ct))).ShouldSucceed();
        var result = await Auth(s => s.RefreshAsync(new RefreshCommand(session.RefreshToken), ClientInfo.Unknown, Ct));

        result.ShouldFail(ErrorKind.Unauthorized, "invalid_refresh_token");
    }

    [Fact]
    public async Task Access_token_is_signed_and_identifies_the_user()
    {
        var session = await RegisterAsync("alice");

        var validation = await new JsonWebTokenHandler().ValidateTokenAsync(session.AccessToken, new TokenValidationParameters
        {
            ValidIssuer = Issuer,
            ValidAudience = Issuer,
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(SigningKey)),
        });

        validation.IsValid.ShouldBeTrue(validation.Exception?.Message);
        validation.Claims[JwtRegisteredClaimNames.Sub].ShouldBe(session.User.Id.ToString());
        validation.Claims[JwtRegisteredClaimNames.UniqueName].ShouldBe("alice");
    }
}
