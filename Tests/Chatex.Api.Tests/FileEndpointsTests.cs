using System.Net;
using System.Net.Http.Json;
using Chatex.Business.Attachments;
using Chatex.Business.Chats;
using Chatex.Business.Users;

namespace Chatex.Api.Tests;

public sealed class FileEndpointsTests(PostgresFixture fixture) : ApiTestBase(fixture)
{
    [Fact]
    public async Task Attachments_are_uploaded_sent_and_downloaded_by_chat_members()
    {
        var alice = await RegisterAsync("alice");
        var bob = await RegisterAsync("bob");
        var eve = await RegisterAsync("eve");
        var chat = await (await alice.Client.PostAsJsonAsync(
                "/api/chats", new { type = "group", title = "Team", memberIds = new[] { bob.UserId } }, Json, Ct))
            .ShouldHaveAsync<ChatDto>(HttpStatusCode.Created);

        var upload = await alice.Client.PostAsync("/api/attachments", FileContent(PngBytes, "photo.png", "image/png"), Ct);
        var attachment = await upload.ShouldHaveAsync<AttachmentDto>(HttpStatusCode.Created);
        attachment.SizeBytes.ShouldBe(PngBytes.Length);
        upload.Headers.Location.ShouldBe(new Uri($"/api/attachments/{attachment.Id}", UriKind.Relative));

        await (await alice.Client.PostAsJsonAsync(
                $"/api/chats/{chat.Id}/messages", new { attachmentIds = new[] { attachment.Id } }, Json, Ct))
            .ShouldHaveStatusAsync(HttpStatusCode.Created);

        var download = await bob.Client.GetAsync($"/api/attachments/{attachment.Id}", Ct);
        await download.ShouldHaveStatusAsync(HttpStatusCode.OK);
        (await download.Content.ReadAsByteArrayAsync(Ct)).ShouldBe(PngBytes);
        download.Content.Headers.ContentType?.MediaType.ShouldBe("image/png");
        download.Content.Headers.ContentDisposition?.DispositionType.ShouldBe("inline");
        download.Headers.GetValues("X-Content-Type-Options").ShouldBe(["nosniff"]);

        var outsider = await eve.Client.GetAsync($"/api/attachments/{attachment.Id}", Ct);
        await outsider.ShouldBeProblemAsync(HttpStatusCode.NotFound, "attachment_not_found");
    }

    [Fact]
    public async Task Non_image_attachments_download_under_their_file_name()
    {
        var alice = await RegisterAsync("alice");
        var bytes = "plain text"u8.ToArray();

        var attachment = await (await alice.Client.PostAsync("/api/attachments", FileContent(bytes, "notes.txt", "text/plain"), Ct))
            .ShouldHaveAsync<AttachmentDto>(HttpStatusCode.Created);
        var download = await alice.Client.GetAsync($"/api/attachments/{attachment.Id}", Ct);

        await download.ShouldHaveStatusAsync(HttpStatusCode.OK);
        var disposition = download.Content.Headers.ContentDisposition.ShouldNotBeNull();
        disposition.DispositionType.ShouldBe("attachment");
        (disposition.FileNameStar ?? disposition.FileName).ShouldBe("notes.txt");
    }

    [Fact]
    public async Task Too_large_and_disallowed_uploads_are_400()
    {
        var alice = await RegisterAsync("alice");

        var tooLarge = await alice.Client.PostAsync(
            "/api/attachments", FileContent(new byte[MaxAttachmentBytes + 1], "big.png", "image/png"), Ct);
        var html = await alice.Client.PostAsync(
            "/api/attachments", FileContent("<script></script>"u8.ToArray(), "x.html", "text/html"), Ct);

        await tooLarge.ShouldBeProblemAsync(HttpStatusCode.BadRequest, "attachment_too_large");
        await html.ShouldBeProblemAsync(HttpStatusCode.BadRequest, "attachment_invalid_type");
    }

    [Fact]
    public async Task Avatars_are_set_served_with_their_image_type_and_removed()
    {
        var alice = await RegisterAsync("alice");
        var bob = await RegisterAsync("bob");

        var profile = await (await alice.Client.PutAsync("/api/me/avatar", FileContent(PngBytes, "me.png", "image/png"), Ct))
            .ShouldHaveAsync<MyProfileDto>(HttpStatusCode.OK);
        profile.HasAvatar.ShouldBeTrue();

        var avatar = await bob.Client.GetAsync($"/api/users/{alice.UserId}/avatar", Ct);
        await avatar.ShouldHaveStatusAsync(HttpStatusCode.OK);
        avatar.Content.Headers.ContentType?.MediaType.ShouldBe("image/png");
        (await avatar.Content.ReadAsByteArrayAsync(Ct)).ShouldBe(PngBytes);

        var removed = await (await alice.Client.DeleteAsync("/api/me/avatar", Ct)).ShouldHaveAsync<MyProfileDto>(HttpStatusCode.OK);
        removed.HasAvatar.ShouldBeFalse();
        await (await bob.Client.GetAsync($"/api/users/{alice.UserId}/avatar", Ct))
            .ShouldBeProblemAsync(HttpStatusCode.NotFound, "avatar_not_found");
    }

    [Fact]
    public async Task Upload_without_a_file_is_400()
    {
        var alice = await RegisterAsync("alice");

        var response = await alice.Client.PostAsync("/api/attachments", new MultipartFormDataContent(), Ct);

        await response.ShouldBeProblemAsync(HttpStatusCode.BadRequest);
    }
}
