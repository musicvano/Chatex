using Chatex.Business.Common;
using Chatex.Business.Messages;
using Chatex.DataAccess.Entities;

namespace Chatex.Business.Tests;

public sealed class MessageServiceTests(PostgresFixture fixture) : BusinessTestBase(fixture)
{
    [Fact]
    public async Task Sending_moves_the_chat_to_the_top_and_counts_as_unread_for_others()
    {
        var alice = await RegisterUserAsync("alice");
        var bob = await RegisterUserAsync("bob");
        var older = await CreateChatAsync(alice, ChatType.Group, null, bob);
        Time.Advance(TimeSpan.FromMinutes(1));
        var newer = await CreateChatAsync(alice, ChatType.Group, null, bob);

        Time.Advance(TimeSpan.FromMinutes(1));
        var message = await SendAsync(bob, older.Id, "  hello  ");

        var list = (await Chats(s => s.GetMyChatsAsync(alice, null, null, Ct))).ShouldSucceed();
        list.Items.Select(c => c.Id).ShouldBe([older.Id, newer.Id]);
        list.Items[0].UnreadCount.ShouldBe(1);
        list.Items[0].LastMessage.ShouldNotBeNull().Body.ShouldBe("hello");
        message.Sender.UserName.ShouldBe("bob");
        Notifier.Events.ShouldContain($"MessageCreated:{older.Id}");
    }

    [Fact]
    public async Task Only_the_sender_can_edit()
    {
        var alice = await RegisterUserAsync("alice");
        var bob = await RegisterUserAsync("bob");
        var group = await CreateChatAsync(alice, ChatType.Group, null, bob);
        var message = await SendAsync(bob, group.Id, "typo");

        (await Messages(s => s.EditAsync(alice, message.Id, new EditMessageCommand("fixed"), Ct)))
            .ShouldFail(ErrorKind.Forbidden, "not_sender");

        var edited = (await Messages(s => s.EditAsync(bob, message.Id, new EditMessageCommand("fixed"), Ct))).ShouldSucceed();
        edited.Body.ShouldBe("fixed");
        // Postgres keeps microseconds; the fake clock has 100 ns ticks.
        edited.EditedAt.ShouldNotBeNull().ShouldBe(Time.GetUtcNow(), TimeSpan.FromMilliseconds(1));
    }

    [Fact]
    public async Task An_admin_can_delete_a_message_and_its_files()
    {
        var alice = await RegisterUserAsync("alice");
        var bob = await RegisterUserAsync("bob");
        var group = await CreateChatAsync(alice, ChatType.Group, null, bob);
        var file = await UploadAsync(bob);
        var message = await SendAsync(bob, group.Id, "see attached", file.Id);
        message.Attachments.Select(a => a.Id).ShouldBe([file.Id]);

        (await Messages(s => s.DeleteAsync(alice, message.Id, Ct))).ShouldSucceed();

        var history = (await Messages(s => s.GetHistoryAsync(bob, group.Id, null, null, null, Ct))).ShouldSucceed();
        var deleted = history.Items.ShouldHaveSingleItem();
        deleted.IsDeleted.ShouldBeTrue();
        deleted.Body.ShouldBeNull();
        deleted.Attachments.ShouldBeEmpty();
        Files.Count.ShouldBe(0);
        (await Attachments(s => s.GetDownloadAsync(bob, file.Id, Ct))).ShouldFail(ErrorKind.NotFound, "attachment_not_found");
    }

    [Fact]
    public async Task A_reply_must_point_to_the_same_chat()
    {
        var alice = await RegisterUserAsync("alice");
        var first = await CreateChatAsync(alice, ChatType.Group);
        var second = await CreateChatAsync(alice, ChatType.Group);
        var elsewhere = await SendAsync(alice, second.Id, "elsewhere");

        var result = await Messages(s => s.SendAsync(alice, first.Id, new SendMessageCommand("re", elsewhere.Id), Ct));

        result.ShouldFail(ErrorKind.Validation, "reply_not_found");
    }

    [Fact]
    public async Task Attachments_of_another_user_cannot_be_sent()
    {
        var alice = await RegisterUserAsync("alice");
        var bob = await RegisterUserAsync("bob");
        var group = await CreateChatAsync(alice, ChatType.Group, null, bob);
        var bobsFile = await UploadAsync(bob);

        var result = await Messages(s => s.SendAsync(alice, group.Id, new SendMessageCommand(null, null, [bobsFile.Id]), Ct));

        result.ShouldFail(ErrorKind.Validation, "invalid_attachments");
    }

    [Fact]
    public async Task An_empty_message_without_attachments_is_rejected()
    {
        var alice = await RegisterUserAsync("alice");
        var group = await CreateChatAsync(alice, ChatType.Group);

        var result = await Messages(s => s.SendAsync(alice, group.Id, new SendMessageCommand("   "), Ct));

        result.ShouldFail(ErrorKind.Validation, "validation_failed").Details.ShouldNotBeNull().ShouldContainKey("body");
    }

    [Fact]
    public async Task GetHistoryAsync_pages_with_opaque_cursors()
    {
        var alice = await RegisterUserAsync("alice");
        var group = await CreateChatAsync(alice, ChatType.Group);
        for (var i = 0; i < 5; i++)
        {
            await SendAsync(alice, group.Id, $"m{i}");
        }

        var page1 = (await Messages(s => s.GetHistoryAsync(alice, group.Id, null, null, 2, Ct))).ShouldSucceed();
        var page2 = (await Messages(s => s.GetHistoryAsync(alice, group.Id, page1.NextCursor, null, 2, Ct))).ShouldSucceed();
        var page3 = (await Messages(s => s.GetHistoryAsync(alice, group.Id, page2.NextCursor, null, 2, Ct))).ShouldSucceed();
        var newer = (await Messages(s => s.GetHistoryAsync(alice, group.Id, null, page2.NextCursor, 10, Ct))).ShouldSucceed();

        page1.Items.Concat(page2.Items).Concat(page3.Items).Select(m => m.Body).ShouldBe(["m4", "m3", "m2", "m1", "m0"]);
        page3.NextCursor.ShouldBeNull();
        newer.Items.Select(m => m.Body).ShouldBe(["m2", "m3", "m4"]);
    }
}
