using Chatex.DataAccess.Abstractions;
using Chatex.DataAccess.Entities;

namespace Chatex.DataAccess.Tests;

public sealed class MessageRepositoryTests(PostgresFixture fixture) : DbTestBase(fixture)
{
    [Fact]
    public async Task GetPageAsync_pages_backwards_through_history_newest_first()
    {
        var alice = await CreateUserAsync("alice");
        var chat = await CreateChatAsync(ChatType.Group, alice, []);
        var messages = await AddMessagesAsync(chat, alice, 120);
        var ids = messages.Select(m => m.Id).ToList();

        await using var scope = NewScope();
        var repository = Get<IMessageRepository>(scope);

        var page1 = await repository.GetPageAsync(chat.Id, before: null, after: null, limit: 50, Ct);
        var page2 = await repository.GetPageAsync(chat.Id, before: page1.NextCursor, after: null, limit: 50, Ct);
        var page3 = await repository.GetPageAsync(chat.Id, before: page2.NextCursor, after: null, limit: 50, Ct);

        page1.Items.Select(m => m.Id).ShouldBe(ids[70..120].AsEnumerable().Reverse());
        page2.Items.Select(m => m.Id).ShouldBe(ids[20..70].AsEnumerable().Reverse());
        page3.Items.Select(m => m.Id).ShouldBe(ids[0..20].AsEnumerable().Reverse());
        page1.NextCursor.ShouldBe(ids[70]);
        page3.NextCursor.ShouldBeNull();
        page1.Items[0].Sender.Profile.ShouldNotBeNull();
    }

    [Fact]
    public async Task GetPageAsync_after_returns_newer_messages_oldest_first()
    {
        var alice = await CreateUserAsync("alice");
        var chat = await CreateChatAsync(ChatType.Group, alice, []);
        var ids = (await AddMessagesAsync(chat, alice, 30)).Select(m => m.Id).ToList();

        await using var scope = NewScope();
        var repository = Get<IMessageRepository>(scope);

        var page1 = await repository.GetPageAsync(chat.Id, before: null, after: ids[9], limit: 15, Ct);
        var page2 = await repository.GetPageAsync(chat.Id, before: null, after: page1.NextCursor, limit: 15, Ct);

        page1.Items.Select(m => m.Id).ShouldBe(ids[10..25]);
        page2.Items.Select(m => m.Id).ShouldBe(ids[25..30]);
        page2.NextCursor.ShouldBeNull();
    }

    [Fact]
    public async Task GetPageAsync_only_returns_messages_of_the_chat()
    {
        var alice = await CreateUserAsync("alice");
        var chat = await CreateChatAsync(ChatType.Group, alice, []);
        var other = await CreateChatAsync(ChatType.Group, alice, []);
        await AddMessagesAsync(chat, alice, 3);
        await AddMessagesAsync(other, alice, 5);

        await using var scope = NewScope();
        var page = await Get<IMessageRepository>(scope).GetPageAsync(chat.Id, null, null, 50, Ct);

        page.Items.Count.ShouldBe(3);
        page.Items.ShouldAllBe(m => m.ChatId == chat.Id);
    }
}
