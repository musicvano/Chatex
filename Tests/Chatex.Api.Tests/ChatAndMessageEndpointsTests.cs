using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Chatex.Api.Contracts;
using Chatex.Business.Chats;
using Chatex.Business.Common;
using Chatex.Business.Messages;
using Chatex.DataAccess.Entities;

namespace Chatex.Api.Tests;

public sealed class ChatAndMessageEndpointsTests(PostgresFixture fixture) : ApiTestBase(fixture)
{
    [Fact]
    public async Task Creating_a_group_returns_201_with_location_and_camel_case_enums()
    {
        var alice = await RegisterAsync("alice");
        var bob = await RegisterAsync("bob");

        var response = await alice.Client.PostAsJsonAsync(
            "/api/chats", new { type = "group", title = "Team", memberIds = new[] { bob.UserId } }, Json, Ct);

        await response.ShouldHaveStatusAsync(HttpStatusCode.Created);
        var raw = await response.Content.ReadFromJsonAsync<JsonElement>(Ct);
        raw.GetProperty("type").GetString().ShouldBe("group");
        raw.GetProperty("myRole").GetString().ShouldBe("owner");

        var chat = raw.Deserialize<ChatDto>(Json).ShouldNotBeNull();
        chat.MemberCount.ShouldBe(2);
        response.Headers.Location.ShouldBe(new Uri($"/api/chats/{chat.Id}", UriKind.Relative));

        var fetched = await (await bob.Client.GetAsync($"/api/chats/{chat.Id}", Ct)).ShouldHaveAsync<ChatDto>(HttpStatusCode.OK);
        fetched.MyRole.ShouldBe(ChatMemberRole.Member);
    }

    [Fact]
    public async Task Messages_can_be_sent_read_edited_and_deleted()
    {
        var alice = await RegisterAsync("alice");
        var bob = await RegisterAsync("bob");
        var chat = await CreateGroupAsync(alice, bob.UserId);

        var sent = await (await bob.Client.PostAsJsonAsync($"/api/chats/{chat.Id}/messages", new { body = "hi all" }, Json, Ct))
            .ShouldHaveAsync<MessageDto>(HttpStatusCode.Created);
        await SendAsync(alice, chat.Id, "hello bob");

        var page = await (await alice.Client.GetAsync($"/api/chats/{chat.Id}/messages?limit=1", Ct))
            .ShouldHaveAsync<PageDto<MessageDto>>(HttpStatusCode.OK);
        page.Items.Single().Body.ShouldBe("hello bob");
        page.NextCursor.ShouldNotBeNull();

        var older = await (await alice.Client.GetAsync($"/api/chats/{chat.Id}/messages?before={page.NextCursor}", Ct))
            .ShouldHaveAsync<PageDto<MessageDto>>(HttpStatusCode.OK);
        older.Items.Single().Id.ShouldBe(sent.Id);
        older.NextCursor.ShouldBeNull();

        var edited = await (await bob.Client.PutAsJsonAsync($"/api/messages/{sent.Id}", new { body = "hi everyone" }, Json, Ct))
            .ShouldHaveAsync<MessageDto>(HttpStatusCode.OK);
        edited.Body.ShouldBe("hi everyone");
        edited.EditedAt.ShouldNotBeNull();

        var notSender = await alice.Client.PutAsJsonAsync($"/api/messages/{sent.Id}", new { body = "hacked" }, Json, Ct);
        await notSender.ShouldBeProblemAsync(HttpStatusCode.Forbidden, "not_sender");

        await (await bob.Client.DeleteAsync($"/api/messages/{sent.Id}", Ct)).ShouldHaveStatusAsync(HttpStatusCode.NoContent);
        var after = await (await alice.Client.GetAsync($"/api/chats/{chat.Id}/messages", Ct))
            .ShouldHaveAsync<PageDto<MessageDto>>(HttpStatusCode.OK);
        after.Items.Single(m => m.Id == sent.Id).IsDeleted.ShouldBeTrue();
    }

    [Fact]
    public async Task Outsiders_cannot_read_a_private_group()
    {
        var alice = await RegisterAsync("alice");
        var eve = await RegisterAsync("eve");
        var chat = await CreateGroupAsync(alice);

        var response = await eve.Client.GetAsync($"/api/chats/{chat.Id}/messages", Ct);

        await response.ShouldBeProblemAsync(HttpStatusCode.NotFound, "chat_not_found");
    }

    [Fact]
    public async Task Unknown_chat_is_404_problem()
    {
        var alice = await RegisterAsync("alice");

        var response = await alice.Client.GetAsync($"/api/chats/{Guid.NewGuid()}", Ct);

        await response.ShouldBeProblemAsync(HttpStatusCode.NotFound, "chat_not_found");
    }

    [Fact]
    public async Task Public_channels_can_be_found_and_joined_but_subscribers_cannot_post()
    {
        var alice = await RegisterAsync("alice");
        var bob = await RegisterAsync("bob");
        var created = await alice.Client.PostAsJsonAsync(
            "/api/chats", new { type = "channel", title = "News", publicHandle = "news_room" }, Json, Ct);
        var channel = await created.ShouldHaveAsync<ChatDto>(HttpStatusCode.Created);

        var found = await (await bob.Client.GetAsync("/api/chats/search?query=news", Ct))
            .ShouldHaveAsync<ChatSummaryDto[]>(HttpStatusCode.OK);
        found.ShouldContain(c => c.Id == channel.Id);

        var joined = await (await bob.Client.PostAsync($"/api/chats/{channel.Id}/join", null, Ct))
            .ShouldHaveAsync<ChatDto>(HttpStatusCode.OK);
        joined.MyRole.ShouldBe(ChatMemberRole.Subscriber);

        var post = await bob.Client.PostAsJsonAsync($"/api/chats/{channel.Id}/messages", new { body = "me too" }, Json, Ct);
        await post.ShouldBeProblemAsync(HttpStatusCode.Forbidden, "cannot_post");
    }

    [Fact]
    public async Task Direct_chat_members_roles_and_read_state()
    {
        var alice = await RegisterAsync("alice");
        var bob = await RegisterAsync("bob");
        var carol = await RegisterAsync("carol");

        var direct = await (await alice.Client.PostAsJsonAsync("/api/chats/direct", new OpenDirectRequest(bob.UserId), Json, Ct))
            .ShouldHaveAsync<ChatDto>(HttpStatusCode.OK);
        direct.Type.ShouldBe(ChatType.Direct);
        direct.Peer.ShouldNotBeNull().Id.ShouldBe(bob.UserId);

        var group = await CreateGroupAsync(alice);
        var added = await (await alice.Client.PostAsJsonAsync(
                $"/api/chats/{group.Id}/members", new AddMembersRequest([bob.UserId, carol.UserId]), Json, Ct))
            .ShouldHaveAsync<AddMembersResponse>(HttpStatusCode.OK);
        added.Added.ShouldBe(2);

        await (await alice.Client.PutAsJsonAsync(
                $"/api/chats/{group.Id}/members/{bob.UserId}/role", new { role = "admin" }, Json, Ct))
            .ShouldHaveStatusAsync(HttpStatusCode.NoContent);
        await (await bob.Client.DeleteAsync($"/api/chats/{group.Id}/members/{carol.UserId}", Ct))
            .ShouldHaveStatusAsync(HttpStatusCode.NoContent);

        var members = await (await alice.Client.GetAsync($"/api/chats/{group.Id}/members", Ct))
            .ShouldHaveAsync<PageDto<ChatMemberDto>>(HttpStatusCode.OK);
        members.Items.Select(m => (m.User.Id, m.Role)).ShouldBe(
            [(alice.UserId, ChatMemberRole.Owner), (bob.UserId, ChatMemberRole.Admin)], ignoreOrder: true);

        var message = await SendAsync(alice, group.Id, "unread for bob");
        var unread = await BobsChatAsync(bob, group.Id);
        unread.UnreadCount.ShouldBe(1);

        await (await bob.Client.PostAsJsonAsync($"/api/chats/{group.Id}/read", new MarkReadRequest(message.Id), Json, Ct))
            .ShouldHaveStatusAsync(HttpStatusCode.NoContent);
        await (await bob.Client.PutAsJsonAsync($"/api/chats/{group.Id}/mute", new SetMutedRequest(true), Json, Ct))
            .ShouldHaveStatusAsync(HttpStatusCode.NoContent);

        var read = await BobsChatAsync(bob, group.Id);
        read.UnreadCount.ShouldBe(0);
        read.IsMuted.ShouldBeTrue();
    }

    private static async Task<ChatListItemDto> BobsChatAsync(Session bob, Guid chatId)
    {
        var page = await (await bob.Client.GetAsync("/api/chats", Ct)).ShouldHaveAsync<PageDto<ChatListItemDto>>(HttpStatusCode.OK);
        return page.Items.Single(c => c.Id == chatId);
    }

    private static async Task<ChatDto> CreateGroupAsync(Session owner, params Guid[] memberIds)
    {
        var response = await owner.Client.PostAsJsonAsync(
            "/api/chats", new { type = "group", title = "Team", memberIds }, Json, Ct);
        return await response.ShouldHaveAsync<ChatDto>(HttpStatusCode.Created);
    }

    private static async Task<MessageDto> SendAsync(Session sender, Guid chatId, string body)
    {
        var response = await sender.Client.PostAsJsonAsync($"/api/chats/{chatId}/messages", new { body }, Json, Ct);
        return await response.ShouldHaveAsync<MessageDto>(HttpStatusCode.Created);
    }
}
