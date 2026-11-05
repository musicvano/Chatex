using Chatex.Business.Auth;
using Chatex.Business.Common;
using Chatex.Business.Users;

namespace Chatex.Business.Tests;

public sealed class ProfileServiceTests(PostgresFixture fixture) : BusinessTestBase(fixture)
{
    [Fact]
    public async Task UpdateProfileAsync_changes_display_name_and_bio()
    {
        var alice = await RegisterUserAsync("alice");

        var updated = await Profiles(s => s.UpdateProfileAsync(alice, new UpdateProfileCommand(" Alice A. ", "  "), Ct));
        var seenByOthers = await Profiles(s => s.GetUserAsync(alice, Ct));

        updated.ShouldSucceed().DisplayName.ShouldBe("Alice A.");
        seenByOthers.ShouldSucceed().DisplayName.ShouldBe("Alice A.");
        seenByOthers.Value.Bio.ShouldBeNull();
    }

    [Fact]
    public async Task ChangePasswordAsync_ends_all_sessions()
    {
        var session = await RegisterAsync("alice");

        (await Profiles(s => s.ChangePasswordAsync(session.User.Id, new ChangePasswordCommand(Password, "NewPassw0rd"), Ct)))
            .ShouldSucceed();

        (await Auth(s => s.RefreshAsync(new RefreshCommand(session.RefreshToken), ClientInfo.Unknown, Ct)))
            .ShouldFail(ErrorKind.Unauthorized, "invalid_refresh_token");
        (await Auth(s => s.LoginAsync(new LoginCommand("alice", "NewPassw0rd"), ClientInfo.Unknown, Ct))).ShouldSucceed();
    }

    [Fact]
    public async Task ChangePasswordAsync_requires_the_current_password()
    {
        var alice = await RegisterUserAsync("alice");

        var result = await Profiles(s => s.ChangePasswordAsync(alice, new ChangePasswordCommand("Wrong1234", "NewPassw0rd"), Ct));

        result.ShouldFail(ErrorKind.Validation, "wrong_password");
    }

    [Fact]
    public async Task SetAvatarAsync_accepts_images_only_and_replaces_the_old_file()
    {
        var alice = await RegisterUserAsync("alice");

        (await Profiles(s => s.SetAvatarAsync(alice, Upload(10, "text/html"), Ct)))
            .ShouldFail(ErrorKind.Validation, "avatar_invalid_type");

        (await Profiles(s => s.SetAvatarAsync(alice, Upload(10, "image/png"), Ct))).ShouldSucceed().HasAvatar.ShouldBeTrue();
        (await Profiles(s => s.SetAvatarAsync(alice, Upload(20, "image/webp"), Ct))).ShouldSucceed();
        Files.Count.ShouldBe(1);

        await using var avatar = (await Profiles(s => s.OpenAvatarAsync(alice, Ct))).ShouldSucceed();
        avatar.Length.ShouldBe(20);
    }

    [Fact]
    public async Task SearchUsersAsync_needs_two_characters_and_finds_by_display_name()
    {
        await RegisterUserAsync("alice");
        await RegisterUserAsync("bob");

        (await Profiles(s => s.SearchUsersAsync("a", null, Ct))).ShouldFail(ErrorKind.Validation, "query_too_short");
        (await Profiles(s => s.SearchUsersAsync("BOB DISP", null, Ct))).ShouldSucceed().Select(u => u.UserName).ShouldBe(["bob"]);
    }
}
