using Chatex.Business.Chats;
using Chatex.Business.Common;
using Chatex.Business.Messages;
using Chatex.DataAccess.Entities;

namespace Chatex.Business.Tests;

public sealed class ChatServiceTests(PostgresFixture fixture) : BusinessTestBase(fixture)
{
    [Fact]
    public async Task OpenDirectAsync_returns_the_same_chat_from_both_sides()
    {
        var alice = await RegisterUserAsync("alice");
        var bob = await RegisterUserAsync("bob");

        var first = (await Chats(s => s.OpenDirectAsync(alice, bob, Ct))).ShouldSucceed();
        var second = (await Chats(s => s.OpenDirectAsync(bob, alice, Ct))).ShouldSucceed();

        second.Id.ShouldBe(first.Id);
        first.Type.ShouldBe(ChatType.Direct);
        first.Peer.ShouldNotBeNull().UserName.ShouldBe("bob");
        second.Peer.ShouldNotBeNull().UserName.ShouldBe("alice");
        first.MemberCount.ShouldBe(2);
    }

    [Fact]
    public async Task OpenDirectAsync_rejects_yourself_and_unknown_users()
    {
        var alice = await RegisterUserAsync("alice");

        (await Chats(s => s.OpenDirectAsync(alice, alice, Ct))).ShouldFail(ErrorKind.Validation, "direct_with_self");
        (await Chats(s => s.OpenDirectAsync(alice, Guid.NewGuid(), Ct))).ShouldFail(ErrorKind.NotFound, "user_not_found");
    }

    [Fact]
    public async Task CreateAsync_makes_the_caller_owner_and_keeps_handles_unique()
    {
        var alice = await RegisterUserAsync("alice");
        var bob = await RegisterUserAsync("bob");

        var channel = await CreateChatAsync(alice, ChatType.Channel, "News_Room", bob);
        var duplicate = await Chats(s => s.CreateAsync(bob, new CreateChatCommand(ChatType.Group, "Copy", PublicHandle: "news_ROOM"), Ct));

        channel.MyRole.ShouldBe(ChatMemberRole.Owner);
        channel.PublicHandle.ShouldBe("news_room");
        channel.MemberCount.ShouldBe(2);
        duplicate.ShouldFail(ErrorKind.Conflict, "handle_taken");
        Notifier.Events.ShouldContain($"MembersAdded:{channel.Id}");
    }

    [Fact]
    public async Task Subscribers_cannot_post_in_a_channel()
    {
        var alice = await RegisterUserAsync("alice");
        var bob = await RegisterUserAsync("bob");
        var channel = await CreateChatAsync(alice, ChatType.Channel, null, bob);

        var fromBob = await Messages(s => s.SendAsync(bob, channel.Id, new SendMessageCommand("hi"), Ct));
        var fromAlice = await Messages(s => s.SendAsync(alice, channel.Id, new SendMessageCommand("hello"), Ct));

        fromBob.ShouldFail(ErrorKind.Forbidden, "cannot_post");
        fromAlice.ShouldSucceed();
    }

    [Fact]
    public async Task Private_chats_are_invisible_to_non_members()
    {
        var alice = await RegisterUserAsync("alice");
        var carol = await RegisterUserAsync("carol");
        var group = await CreateChatAsync(alice, ChatType.Group);

        (await Chats(s => s.GetAsync(carol, group.Id, Ct))).ShouldFail(ErrorKind.NotFound, "chat_not_found");
        (await Messages(s => s.GetHistoryAsync(carol, group.Id, null, null, null, Ct))).ShouldFail(ErrorKind.NotFound, "chat_not_found");
        (await Chats(s => s.JoinAsync(carol, group.Id, Ct))).ShouldFail(ErrorKind.NotFound, "chat_not_found");
    }

    [Fact]
    public async Task Anyone_can_read_and_join_a_public_channel()
    {
        var alice = await RegisterUserAsync("alice");
        var carol = await RegisterUserAsync("carol");
        var channel = await CreateChatAsync(alice, ChatType.Channel, "public_news");
        await SendAsync(alice, channel.Id, "first post");

        var preview = await Messages(s => s.GetHistoryAsync(carol, channel.Id, null, null, null, Ct));
        var joined = await Chats(s => s.JoinAsync(carol, channel.Id, Ct));
        var joinedAgain = await Chats(s => s.JoinAsync(carol, channel.Id, Ct));

        preview.ShouldSucceed().Items.Count.ShouldBe(1);
        joined.ShouldSucceed().MyRole.ShouldBe(ChatMemberRole.Subscriber);
        joinedAgain.ShouldSucceed().MemberCount.ShouldBe(2);
        (await Chats(s => s.SearchPublicAsync("NEWS", null, Ct))).ShouldSucceed().Select(c => c.Id).ShouldBe([channel.Id]);
    }

    [Fact]
    public async Task The_owner_must_hand_over_ownership_before_leaving()
    {
        var alice = await RegisterUserAsync("alice");
        var bob = await RegisterUserAsync("bob");
        var group = await CreateChatAsync(alice, ChatType.Group, null, bob);

        (await Chats(s => s.LeaveAsync(alice, group.Id, Ct))).ShouldFail(ErrorKind.Conflict, "owner_must_transfer");

        (await Chats(s => s.ChangeRoleAsync(alice, group.Id, bob, ChatMemberRole.Owner, Ct))).ShouldSucceed();
        (await Chats(s => s.GetAsync(alice, group.Id, Ct))).ShouldSucceed().MyRole.ShouldBe(ChatMemberRole.Admin);
        (await Chats(s => s.LeaveAsync(alice, group.Id, Ct))).ShouldSucceed();
        (await Chats(s => s.GetAsync(bob, group.Id, Ct))).ShouldSucceed().MyRole.ShouldBe(ChatMemberRole.Owner);
    }

    [Fact]
    public async Task Admins_remove_members_but_not_the_owner()
    {
        var alice = await RegisterUserAsync("alice");
        var bob = await RegisterUserAsync("bob");
        var carol = await RegisterUserAsync("carol");
        var group = await CreateChatAsync(alice, ChatType.Group, null, bob, carol);
        (await Chats(s => s.ChangeRoleAsync(alice, group.Id, bob, ChatMemberRole.Admin, Ct))).ShouldSucceed();

        (await Chats(s => s.RemoveMemberAsync(bob, group.Id, alice, Ct))).ShouldFail(ErrorKind.Forbidden, "not_allowed");
        (await Chats(s => s.RemoveMemberAsync(bob, group.Id, carol, Ct))).ShouldSucceed();
        (await Chats(s => s.RemoveMemberAsync(carol, group.Id, bob, Ct))).ShouldFail(ErrorKind.NotFound, "chat_not_found");
    }

    [Fact]
    public async Task Channel_roles_and_member_lists_follow_channel_rules()
    {
        var alice = await RegisterUserAsync("alice");
        var bob = await RegisterUserAsync("bob");
        var channel = await CreateChatAsync(alice, ChatType.Channel, null, bob);

        (await Chats(s => s.ChangeRoleAsync(alice, channel.Id, bob, ChatMemberRole.Member, Ct)))
            .ShouldFail(ErrorKind.Validation, "invalid_role");
        (await Chats(s => s.GetMembersAsync(bob, channel.Id, null, null, Ct))).ShouldFail(ErrorKind.Forbidden, "not_allowed");
        (await Chats(s => s.GetMembersAsync(alice, channel.Id, null, null, Ct))).ShouldSucceed().Items.Count.ShouldBe(2);
    }

    [Fact]
    public async Task AddMembersAsync_skips_existing_members_and_rejects_unknown_users()
    {
        var alice = await RegisterUserAsync("alice");
        var bob = await RegisterUserAsync("bob");
        var carol = await RegisterUserAsync("carol");
        var group = await CreateChatAsync(alice, ChatType.Group, null, bob);

        (await Chats(s => s.AddMembersAsync(alice, group.Id, [bob, carol], Ct))).ShouldSucceed().ShouldBe(1);
        (await Chats(s => s.AddMembersAsync(alice, group.Id, [Guid.NewGuid()], Ct))).ShouldFail(ErrorKind.Validation, "unknown_users");
        (await Chats(s => s.AddMembersAsync(bob, group.Id, [carol], Ct))).ShouldFail(ErrorKind.Forbidden, "not_allowed");
    }

    [Fact]
    public async Task MarkReadAsync_requires_a_message_of_that_chat()
    {
        var alice = await RegisterUserAsync("alice");
        var bob = await RegisterUserAsync("bob");
        var group = await CreateChatAsync(alice, ChatType.Group, null, bob);
        var message = await SendAsync(alice, group.Id, "hi");

        (await Chats(s => s.MarkReadAsync(bob, group.Id, Guid.NewGuid(), Ct))).ShouldFail(ErrorKind.NotFound, "message_not_found");
        (await Chats(s => s.MarkReadAsync(bob, group.Id, message.Id, Ct))).ShouldSucceed();
        Notifier.Events.ShouldContain($"MessagesRead:{group.Id}");
    }

    [Fact]
    public async Task Deleted_chats_disappear_for_everyone()
    {
        var alice = await RegisterUserAsync("alice");
        var bob = await RegisterUserAsync("bob");
        var group = await CreateChatAsync(alice, ChatType.Group, null, bob);

        (await Chats(s => s.DeleteAsync(bob, group.Id, Ct))).ShouldFail(ErrorKind.Forbidden, "not_allowed");
        (await Chats(s => s.DeleteAsync(alice, group.Id, Ct))).ShouldSucceed();

        (await Chats(s => s.GetAsync(bob, group.Id, Ct))).ShouldFail(ErrorKind.NotFound, "chat_not_found");
        (await Chats(s => s.GetMyChatsAsync(bob, null, null, Ct))).ShouldSucceed().Items.ShouldBeEmpty();
    }

    [Fact]
    public async Task GetMyChatsAsync_rejects_a_tampered_cursor()
    {
        var alice = await RegisterUserAsync("alice");

        var result = await Chats(s => s.GetMyChatsAsync(alice, "not-a-cursor", null, Ct));

        result.ShouldFail(ErrorKind.Validation, "invalid_cursor");
    }
}
