using System.Net;
using System.Net.Http.Json;
using Chatex.Api.Contracts;
using Chatex.Business.Chats;
using Chatex.Business.Messages;
using Chatex.Business.Users;
using Microsoft.AspNetCore.SignalR;
using Microsoft.AspNetCore.SignalR.Client;

namespace Chatex.Api.Tests;

/// <summary>
/// The SignalR hub at /hubs/chat over long polling against the in-memory server. To prove that an event did
/// not arrive, a test sends a later event the same client does receive: events on one connection keep their order.
/// </summary>
public sealed class ChatHubTests(PostgresFixture fixture) : ApiTestBase(fixture)
{
    [Fact]
    public async Task A_connection_without_a_token_is_rejected()
    {
        var ex = await Should.ThrowAsync<HttpRequestException>(() => ConnectHubAsync(null));

        ex.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task The_access_token_is_accepted_from_the_query_string_on_hub_paths_only()
    {
        var alice = await RegisterAsync("alice");
        var anonymous = CreateClient();
        var token = Uri.EscapeDataString(alice.Auth.AccessToken);

        var negotiate = await anonymous.PostAsync($"/hubs/chat/negotiate?negotiateVersion=1&access_token={token}", null, Ct);
        await negotiate.ShouldHaveStatusAsync(HttpStatusCode.OK);

        var api = await anonymous.GetAsync($"/api/me?access_token={token}", Ct);
        await api.ShouldHaveStatusAsync(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Message_events_reach_members_but_not_outsiders()
    {
        var alice = await RegisterAsync("alice");
        var bob = await RegisterAsync("bob");
        var eve = await RegisterAsync("eve");
        var group = await CreateGroupAsync(alice, bob.UserId);
        var aliceAndEve = await OpenDirectAsync(alice, eve.UserId);
        var bobHub = await ConnectHubAsync(bob);
        var eveHub = await ConnectHubAsync(eve);

        var sent = await SendAsync(alice, group.Id, "hello");
        var created = (await bobHub.WaitForAsync("MessageCreated")).Arg<MessageDto>(0);
        created.Id.ShouldBe(sent.Id);
        created.ChatId.ShouldBe(group.Id);
        created.Body.ShouldBe("hello");
        created.Sender.Id.ShouldBe(alice.UserId);

        await (await alice.Client.PutAsJsonAsync($"/api/messages/{sent.Id}", new { body = "hello all" }, Json, Ct))
            .ShouldHaveStatusAsync(HttpStatusCode.OK);
        var updated = (await bobHub.WaitForAsync("MessageUpdated")).Arg<MessageDto>(0);
        updated.Body.ShouldBe("hello all");
        updated.EditedAt.ShouldNotBeNull();

        await (await alice.Client.DeleteAsync($"/api/messages/{sent.Id}", Ct)).ShouldHaveStatusAsync(HttpStatusCode.NoContent);
        var deleted = await bobHub.WaitForAsync("MessageDeleted");
        deleted.Arg<Guid>(0).ShouldBe(group.Id);
        deleted.Arg<Guid>(1).ShouldBe(sent.Id);

        var barrier = await SendAsync(alice, aliceAndEve.Id, "only for eve");
        var eveEvents = await eveHub.ReadUntilAsync("MessageCreated", e => e.Arg<MessageDto>(0).Id == barrier.Id);
        eveEvents.Where(e => e.Name.StartsWith("Message")).ShouldHaveSingleItem();
    }

    [Fact]
    public async Task Typing_reaches_the_others_but_not_the_sender_and_outsiders_get_an_error()
    {
        var alice = await RegisterAsync("alice");
        var bob = await RegisterAsync("bob");
        var eve = await RegisterAsync("eve");
        var group = await CreateGroupAsync(alice, bob.UserId);
        var aliceHub = await ConnectHubAsync(alice);
        var bobHub = await ConnectHubAsync(bob);
        var eveHub = await ConnectHubAsync(eve);

        await aliceHub.Connection.InvokeAsync("Typing", group.Id, Ct);
        var typing = await bobHub.WaitForAsync("Typing");
        typing.Arg<Guid>(0).ShouldBe(group.Id);
        typing.Arg<Guid>(1).ShouldBe(alice.UserId);

        // Alice's first Typing event is Bob's, so her own never came back to her.
        await bobHub.Connection.InvokeAsync("Typing", group.Id, Ct);
        (await aliceHub.WaitForAsync("Typing")).Arg<Guid>(1).ShouldBe(bob.UserId);

        await Should.ThrowAsync<HubException>(() => eveHub.Connection.InvokeAsync("Typing", group.Id, Ct));
    }

    [Fact]
    public async Task An_added_member_gets_later_messages_without_reconnecting_and_a_removed_one_stops()
    {
        var alice = await RegisterAsync("alice");
        var bob = await RegisterAsync("bob");
        var carol = await RegisterAsync("carol");
        var group = await CreateGroupAsync(alice, bob.UserId);
        var bobHub = await ConnectHubAsync(bob);
        var carolHub = await ConnectHubAsync(carol);

        await (await alice.Client.PostAsJsonAsync($"/api/chats/{group.Id}/members", new AddMembersRequest([carol.UserId]), Json, Ct))
            .ShouldHaveStatusAsync(HttpStatusCode.OK);
        var added = await carolHub.WaitForAsync("MembersAdded");
        added.Arg<Guid>(0).ShouldBe(group.Id);
        added.Arg<Guid[]>(1).ShouldBe([carol.UserId]);

        var welcome = await SendAsync(alice, group.Id, "welcome carol");
        (await carolHub.WaitForAsync("MessageCreated")).Arg<MessageDto>(0).Id.ShouldBe(welcome.Id);

        await (await alice.Client.DeleteAsync($"/api/chats/{group.Id}/members/{bob.UserId}", Ct))
            .ShouldHaveStatusAsync(HttpStatusCode.NoContent);
        var removed = await bobHub.WaitForAsync("MemberRemoved");
        removed.Arg<Guid>(0).ShouldBe(group.Id);
        removed.Arg<Guid>(1).ShouldBe(bob.UserId);

        var afterRemoval = await SendAsync(alice, group.Id, "bob is gone");
        (await carolHub.WaitForAsync("MessageCreated")).Arg<MessageDto>(0).Id.ShouldBe(afterRemoval.Id);

        var aliceAndBob = await OpenDirectAsync(alice, bob.UserId);
        var barrier = await SendAsync(alice, aliceAndBob.Id, "still friends");
        var bobEvents = await bobHub.ReadUntilAsync("MessageCreated", e => e.Arg<MessageDto>(0).Id == barrier.Id);
        bobEvents.ShouldNotContain(e => e.Name == "MessageCreated" && e.Arg<MessageDto>(0).Id == afterRemoval.Id);
    }

    [Fact]
    public async Task Online_and_offline_reach_a_chat_partner_and_last_seen_is_saved()
    {
        var alice = await RegisterAsync("alice");
        var bob = await RegisterAsync("bob");
        await OpenDirectAsync(alice, bob.UserId);
        var bobHub = await ConnectHubAsync(bob);

        var aliceHub = await ConnectHubAsync(alice);
        // Bob's own UserOnline came first: his group includes him.
        await bobHub.WaitForAsync("UserOnline", e => e.Arg<Guid>(0) == alice.UserId);

        await aliceHub.Connection.StopAsync(Ct);
        var offline = await bobHub.WaitForAsync("UserOffline");
        offline.Arg<Guid>(0).ShouldBe(alice.UserId);
        var lastSeenAt = offline.Arg<DateTimeOffset>(1);

        var user = await (await bob.Client.GetAsync($"/api/users/{alice.UserId}", Ct)).ShouldHaveAsync<UserDto>(HttpStatusCode.OK);
        // PostgreSQL keeps microseconds, .NET ticks are 100 ns.
        (user.LastSeenAt.ShouldNotBeNull() - lastSeenAt).Duration().ShouldBeLessThan(TimeSpan.FromMilliseconds(1));
    }

    [Fact]
    public async Task GetOnlineUsers_returns_the_connected_users()
    {
        var alice = await RegisterAsync("alice");
        var bob = await RegisterAsync("bob");
        var carol = await RegisterAsync("carol");
        await OpenDirectAsync(alice, bob.UserId);
        var aliceHub = await ConnectHubAsync(alice);
        var bobHub = await ConnectHubAsync(bob);
        Guid[] everyone = [alice.UserId, bob.UserId, carol.UserId, bob.UserId];

        var online = await aliceHub.Connection.InvokeAsync<Guid[]>("GetOnlineUsers", everyone, Ct);
        online.ShouldBe([alice.UserId, bob.UserId], ignoreOrder: true);

        await bobHub.Connection.StopAsync(Ct);
        await aliceHub.WaitForAsync("UserOffline", e => e.Arg<Guid>(0) == bob.UserId);

        (await aliceHub.Connection.InvokeAsync<Guid[]>("GetOnlineUsers", everyone, Ct)).ShouldBe([alice.UserId]);
    }

    [Fact]
    public async Task Read_receipts_chat_updates_and_chat_deletion_are_forwarded()
    {
        var alice = await RegisterAsync("alice");
        var bob = await RegisterAsync("bob");
        var group = await CreateGroupAsync(alice, bob.UserId);
        var bobHub = await ConnectHubAsync(bob);
        var message = await SendAsync(bob, group.Id, "read me");

        await (await alice.Client.PostAsJsonAsync($"/api/chats/{group.Id}/read", new MarkReadRequest(message.Id), Json, Ct))
            .ShouldHaveStatusAsync(HttpStatusCode.NoContent);
        var read = await bobHub.WaitForAsync("MessagesRead");
        read.Arg<Guid>(0).ShouldBe(group.Id);
        read.Arg<Guid>(1).ShouldBe(alice.UserId);
        read.Arg<Guid>(2).ShouldBe(message.Id);

        await (await alice.Client.PutAsJsonAsync($"/api/chats/{group.Id}", new { title = "Renamed" }, Json, Ct))
            .ShouldHaveStatusAsync(HttpStatusCode.OK);
        (await bobHub.WaitForAsync("ChatUpdated")).Arg<Guid>(0).ShouldBe(group.Id);

        await (await alice.Client.DeleteAsync($"/api/chats/{group.Id}", Ct)).ShouldHaveStatusAsync(HttpStatusCode.NoContent);
        (await bobHub.WaitForAsync("ChatDeleted")).Arg<Guid>(0).ShouldBe(group.Id);
    }

    private static async Task<ChatDto> CreateGroupAsync(Session owner, params Guid[] memberIds)
    {
        var response = await owner.Client.PostAsJsonAsync("/api/chats", new { type = "group", title = "Team", memberIds }, Json, Ct);
        return await response.ShouldHaveAsync<ChatDto>(HttpStatusCode.Created);
    }

    private static async Task<ChatDto> OpenDirectAsync(Session user, Guid otherUserId)
    {
        var response = await user.Client.PostAsJsonAsync("/api/chats/direct", new OpenDirectRequest(otherUserId), Json, Ct);
        return await response.ShouldHaveAsync<ChatDto>(HttpStatusCode.OK);
    }

    private static async Task<MessageDto> SendAsync(Session sender, Guid chatId, string body)
    {
        var response = await sender.Client.PostAsJsonAsync($"/api/chats/{chatId}/messages", new { body }, Json, Ct);
        return await response.ShouldHaveAsync<MessageDto>(HttpStatusCode.Created);
    }
}
