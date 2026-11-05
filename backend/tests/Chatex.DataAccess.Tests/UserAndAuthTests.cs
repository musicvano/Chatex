using Chatex.DataAccess.Abstractions;
using Chatex.DataAccess.Entities;
using Microsoft.AspNetCore.Identity;

namespace Chatex.DataAccess.Tests;

public sealed class UserAndAuthTests(PostgresFixture fixture) : DbTestBase(fixture)
{
    [Fact]
    public async Task Registration_saves_user_and_profile_in_one_transaction()
    {
        Guid userId;
        await using (var scope = NewScope())
        {
            userId = await Get<IUnitOfWork>(scope).ExecuteInTransactionAsync(async ct =>
            {
                var user = new User { UserName = "alice", Email = "alice@example.test" };
                (await Get<UserManager<User>>(scope).CreateAsync(user, "Passw0rd!")).Succeeded.ShouldBeTrue();
                Get<IUserRepository>(scope).AddProfile(new UserProfile { UserId = user.Id, DisplayName = "Alice" });
                await Get<IUnitOfWork>(scope).SaveChangesAsync(ct);
                return user.Id;
            }, Ct);
        }

        await using var check = NewScope();
        var saved = await Get<IUserRepository>(check).GetWithProfileAsync(userId, Ct);
        saved.ShouldNotBeNull();
        saved.CreatedAt.ShouldNotBe(default);
        saved.Profile.ShouldNotBeNull().DisplayName.ShouldBe("Alice");
        saved.Profile.UpdatedAt.ShouldNotBe(default);
    }

    [Fact]
    public async Task Registration_rolls_back_the_user_when_a_later_step_fails()
    {
        await using (var scope = NewScope())
        {
            await Should.ThrowAsync<InvalidOperationException>(() =>
                Get<IUnitOfWork>(scope).ExecuteInTransactionAsync(async _ =>
                {
                    var user = new User { UserName = "alice", Email = "alice@example.test" };
                    (await Get<UserManager<User>>(scope).CreateAsync(user, "Passw0rd!")).Succeeded.ShouldBeTrue();
                    throw new InvalidOperationException("profile step failed");
                }, Ct));
        }

        await using var check = NewScope();
        (await Get<UserManager<User>>(check).FindByNameAsync("alice")).ShouldBeNull();
    }

    [Fact]
    public async Task SearchAsync_treats_like_wildcards_as_plain_text()
    {
        await CreateUserAsync("alice");
        await CreateUserAsync("bob_smith");

        await using var scope = NewScope();
        var repository = Get<IUserRepository>(scope);

        (await repository.SearchAsync("_", 10, Ct)).Select(u => u.UserName).ShouldBe(["bob_smith"]);
        (await repository.SearchAsync("ALI", 10, Ct)).Select(u => u.UserName).ShouldBe(["alice"]);
    }

    [Fact]
    public async Task RevokeAllForUserAsync_revokes_only_active_tokens_of_that_user()
    {
        var alice = await CreateUserAsync("alice");
        var bob = await CreateUserAsync("bob");
        var now = DateTimeOffset.UtcNow;

        await using (var seed = NewScope())
        {
            var tokens = Get<IRefreshTokenRepository>(seed);
            tokens.Add(new RefreshToken { UserId = alice.Id, TokenHash = "a1", ExpiresAt = now.AddDays(7) });
            tokens.Add(new RefreshToken { UserId = alice.Id, TokenHash = "a2", ExpiresAt = now.AddDays(7) });
            tokens.Add(new RefreshToken { UserId = alice.Id, TokenHash = "a-expired", ExpiresAt = now.AddDays(-1) });
            tokens.Add(new RefreshToken { UserId = bob.Id, TokenHash = "b1", ExpiresAt = now.AddDays(7) });
            await Get<IUnitOfWork>(seed).SaveChangesAsync(Ct);
        }

        await using var scope = NewScope();
        var repository = Get<IRefreshTokenRepository>(scope);

        (await repository.RevokeAllForUserAsync(alice.Id, now, Ct)).ShouldBe(2);
        (await repository.GetByHashAsync("a1", Ct)).ShouldNotBeNull().IsActiveAt(now).ShouldBeFalse();
        (await repository.GetByHashAsync("b1", Ct)).ShouldNotBeNull().IsActiveAt(now).ShouldBeTrue();

        (await repository.DeleteStaleAsync(now.AddSeconds(1), Ct)).ShouldBe(3);
    }
}
