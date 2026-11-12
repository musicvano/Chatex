using Chatex.DataAccess.Abstractions;
using Chatex.DataAccess.Entities;

namespace Chatex.DataAccess.Tests;

public sealed class ChatMemberRepositoryTests(PostgresFixture fixture) : DbTestBase(fixture)
{
    [Fact]
    public async Task MarkReadAsync_only_moves_the_read_position_forward()
    {
        var alice = await CreateUserAsync("alice");
        var bob = await CreateUserAsync("bob");
        var chat = await CreateChatAsync(ChatType.Group, alice, [bob]);
        var messages = await AddMessagesAsync(chat, alice, 10);

        await using var scope = NewScope();
        var repository = Get<IChatMemberRepository>(scope);

        (await repository.MarkReadAsync(chat.Id, bob.Id, messages[5].Id, Ct)).ShouldBeTrue();
        (await repository.MarkReadAsync(chat.Id, bob.Id, messages[3].Id, Ct)).ShouldBeFalse();
        (await repository.MarkReadAsync(chat.Id, bob.Id, messages[7].Id, Ct)).ShouldBeTrue();

        var member = await repository.GetAsync(chat.Id, bob.Id, Ct);
        member.ShouldNotBeNull().LastReadMessageId.ShouldBe(messages[7].Id);
    }

    [Fact]
    public async Task GetMembersAsync_pages_through_members_with_profiles()
    {
        var owner = await CreateUserAsync("owner");
        var others = new List<User>();
        for (var i = 0; i < 4; i++)
        {
            others.Add(await CreateUserAsync($"user{i}"));
        }

        var chat = await CreateChatAsync(ChatType.Channel, owner, others);

        await using var scope = NewScope();
        var repository = Get<IChatMemberRepository>(scope);
        var page1 = await repository.GetMembersAsync(chat.Id, afterUserId: null, limit: 3, Ct);
        var page2 = await repository.GetMembersAsync(chat.Id, afterUserId: page1.NextCursor, limit: 3, Ct);

        page1.Items.Count.ShouldBe(3);
        page2.Items.Count.ShouldBe(2);
        page2.NextCursor.ShouldBeNull();
        page1.Items.Concat(page2.Items).Select(m => m.UserId).ShouldBeUnique();
        page1.Items.ShouldAllBe(m => m.User.Profile != null);
        (await repository.CountAsync(chat.Id, Ct)).ShouldBe(5);
    }

    [Fact]
    public async Task GetChatIdsForUserAsync_skips_deleted_chats()
    {
        var alice = await CreateUserAsync("alice");
        var kept = await CreateChatAsync(ChatType.Group, alice, []);
        var deleted = await CreateChatAsync(ChatType.Group, alice, []);

        await using var scope = NewScope();
        var chat = await Get<IChatRepository>(scope).GetByIdAsync(deleted.Id, Ct);
        chat.ShouldNotBeNull().DeletedAt = DateTimeOffset.UtcNow;
        await Get<IUnitOfWork>(scope).SaveChangesAsync(Ct);

        var ids = await Get<IChatMemberRepository>(scope).GetChatIdsForUserAsync(alice.Id, Ct);

        ids.ShouldBe([kept.Id]);
    }

    [Fact]
    public void Only_owners_and_admins_post_in_channels()
    {
        new ChatMember { Role = ChatMemberRole.Subscriber }.CanPost(ChatType.Channel).ShouldBeFalse();
        new ChatMember { Role = ChatMemberRole.Admin }.CanPost(ChatType.Channel).ShouldBeTrue();
        new ChatMember { Role = ChatMemberRole.Member }.CanPost(ChatType.Group).ShouldBeTrue();
    }
}
