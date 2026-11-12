using Chatex.DataAccess.Abstractions;
using Chatex.DataAccess.Entities;
using Chatex.DataAccess.Exceptions;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace Chatex.DataAccess.Tests;

public sealed class ChatRepositoryTests(PostgresFixture fixture) : DbTestBase(fixture)
{
    [Fact]
    public async Task A_pair_of_users_can_have_only_one_direct_chat()
    {
        var alice = await CreateUserAsync("alice");
        var bob = await CreateUserAsync("bob");
        await CreateChatAsync(ChatType.Direct, alice, [bob]);

        var error = await Should.ThrowAsync<UniqueConstraintException>(() => CreateChatAsync(ChatType.Direct, bob, [alice]));

        error.ConstraintName.ShouldBe("ix_chats_direct_key");
    }

    [Fact]
    public async Task Only_direct_chats_have_a_direct_key()
    {
        var alice = await CreateUserAsync("alice");
        var bob = await CreateUserAsync("bob");

        await using var scope = NewScope();
        Get<IChatRepository>(scope).Add(new Chat
        {
            Type = ChatType.Group,
            Title = "group",
            DirectKey = Chat.MakeDirectKey(alice.Id, bob.Id),
            CreatedById = alice.Id,
        });

        var error = await Should.ThrowAsync<DbUpdateException>(() => Get<IUnitOfWork>(scope).SaveChangesAsync(Ct));

        error.InnerException.ShouldBeOfType<PostgresException>().SqlState.ShouldBe(PostgresErrorCodes.CheckViolation);
    }

    [Fact]
    public async Task GetDirectAsync_finds_the_chat_in_either_order()
    {
        var alice = await CreateUserAsync("alice");
        var bob = await CreateUserAsync("bob");
        var chat = await CreateChatAsync(ChatType.Direct, alice, [bob]);

        await using var scope = NewScope();
        var found = await Get<IChatRepository>(scope).GetDirectAsync(bob.Id, alice.Id, Ct);

        found.ShouldNotBeNull().Id.ShouldBe(chat.Id);
    }

    [Fact]
    public async Task GetUserChatsAsync_orders_by_activity_and_counts_unread_messages()
    {
        var alice = await CreateUserAsync("alice");
        var bob = await CreateUserAsync("bob");
        var now = DateTimeOffset.UtcNow;
        var group = await CreateChatAsync(ChatType.Group, alice, [bob], now.AddHours(-3));
        var channel = await CreateChatAsync(ChatType.Channel, bob, [alice], now.AddHours(-2));
        var direct = await CreateChatAsync(ChatType.Direct, alice, [bob], now.AddHours(-1));
        await CreateChatAsync(ChatType.Group, bob, [], now); // alice is not a member

        var fromBob = await AddMessagesAsync(direct, bob, 3);
        await AddMessagesAsync(direct, alice, 1); // own messages are never unread
        await AddMessagesAsync(channel, bob, 4);

        await using var scope = NewScope();
        (await Get<IChatMemberRepository>(scope).MarkReadAsync(direct.Id, alice.Id, fromBob[0].Id, Ct)).ShouldBeTrue();
        var repository = Get<IChatRepository>(scope);

        var page1 = await repository.GetUserChatsAsync(alice.Id, after: null, limit: 2, Ct);
        var page2 = await repository.GetUserChatsAsync(alice.Id, after: page1.NextCursor, limit: 2, Ct);

        page1.Items.Select(i => i.Chat.Id).ShouldBe([direct.Id, channel.Id]);
        page2.Items.Select(i => i.Chat.Id).ShouldBe([group.Id]);
        page2.NextCursor.ShouldBeNull();

        var directItem = page1.Items[0];
        directItem.UnreadCount.ShouldBe(2);
        directItem.Peer.ShouldNotBeNull().UserName.ShouldBe("bob");
        directItem.LastMessage.ShouldNotBeNull().SenderId.ShouldBe(alice.Id);

        var channelItem = page1.Items[1];
        channelItem.UnreadCount.ShouldBe(4);
        channelItem.Role.ShouldBe(ChatMemberRole.Subscriber);
        channelItem.Peer.ShouldBeNull();

        page2.Items[0].LastMessage.ShouldBeNull();
        page2.Items[0].UnreadCount.ShouldBe(0);
    }

    [Fact]
    public async Task SearchPublicAsync_matches_handle_or_title_of_public_chats_only()
    {
        var alice = await CreateUserAsync("alice");
        await using (var seed = NewScope())
        {
            var chats = Get<IChatRepository>(seed);
            chats.Add(new Chat { Type = ChatType.Channel, Title = "Dotnet news", PublicHandle = "dotnet", CreatedById = alice.Id });
            chats.Add(new Chat { Type = ChatType.Group, Title = "Private dotnet", CreatedById = alice.Id });
            await Get<IUnitOfWork>(seed).SaveChangesAsync(Ct);
        }

        await using var scope = NewScope();
        var results = await Get<IChatRepository>(scope).SearchPublicAsync("DOTNET", 10, Ct);

        results.Select(c => c.PublicHandle).ShouldBe(["dotnet"]);
    }
}
