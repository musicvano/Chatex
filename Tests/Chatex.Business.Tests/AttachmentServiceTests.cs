using Chatex.Business.Common;
using Chatex.DataAccess.Entities;

namespace Chatex.Business.Tests;

public sealed class AttachmentServiceTests(PostgresFixture fixture) : BusinessTestBase(fixture)
{
    [Fact]
    public async Task UploadAsync_enforces_size_even_when_the_client_understates_it()
    {
        var alice = await RegisterUserAsync("alice");

        var declaredTooBig = await Attachments(s => s.UploadAsync(alice, Upload(10, "image/png", claimedLength: MaxAttachmentBytes + 1), Ct));
        var actuallyTooBig = await Attachments(s => s.UploadAsync(alice, Upload((int)MaxAttachmentBytes + 1, "image/png", claimedLength: 10), Ct));
        var empty = await Attachments(s => s.UploadAsync(alice, Upload(0, "image/png"), Ct));

        declaredTooBig.ShouldFail(ErrorKind.Validation, "attachment_too_large");
        actuallyTooBig.ShouldFail(ErrorKind.Validation, "attachment_too_large");
        empty.ShouldFail(ErrorKind.Validation, "attachment_empty");
        Files.Count.ShouldBe(0);
    }

    [Fact]
    public async Task UploadAsync_accepts_allowed_types_only_and_cleans_the_file_name()
    {
        var alice = await RegisterUserAsync("alice");

        var html = await Attachments(s => s.UploadAsync(alice, Upload(10, "text/html"), Ct));
        var text = await Attachments(s => s.UploadAsync(
            alice, new FileUpload(new MemoryStream([1, 2, 3]), @"C:\fakepath\..\notes.txt", "text/plain; charset=utf-8", 3), Ct));

        html.ShouldFail(ErrorKind.Validation, "attachment_invalid_type");
        var saved = text.ShouldSucceed();
        saved.FileName.ShouldBe("notes.txt");
        saved.ContentType.ShouldBe("text/plain");
        saved.SizeBytes.ShouldBe(3);
    }

    [Fact]
    public async Task Sent_files_can_be_downloaded_by_chat_members_only()
    {
        var alice = await RegisterUserAsync("alice");
        var bob = await RegisterUserAsync("bob");
        var carol = await RegisterUserAsync("carol");
        var group = await CreateChatAsync(alice, ChatType.Group, null, bob);
        var file = await UploadAsync(bob, size: 42);

        (await Attachments(s => s.GetDownloadAsync(bob, file.Id, Ct))).ShouldSucceed();
        (await Attachments(s => s.GetDownloadAsync(alice, file.Id, Ct))).ShouldFail(ErrorKind.NotFound, "attachment_not_found");

        await SendAsync(bob, group.Id, "file", file.Id);

        var download = (await Attachments(s => s.GetDownloadAsync(alice, file.Id, Ct))).ShouldSucceed();
        await using (var content = await Attachments(s => s.OpenReadAsync(download, Ct)))
        {
            content.Length.ShouldBe(42);
        }

        (await Attachments(s => s.GetDownloadAsync(carol, file.Id, Ct))).ShouldFail(ErrorKind.NotFound, "attachment_not_found");
    }

    [Fact]
    public async Task CleanupStaleAsync_removes_uploads_that_were_never_sent()
    {
        var alice = await RegisterUserAsync("alice");
        var group = await CreateChatAsync(alice, ChatType.Group);
        await UploadAsync(alice);
        var sent = await UploadAsync(alice);
        await SendAsync(alice, group.Id, "keep", sent.Id);

        Time.Advance(TimeSpan.FromDays(2));
        var removed = await Attachments(s => s.CleanupStaleAsync(TimeSpan.FromDays(1), Ct));

        removed.ShouldBe(1);
        Files.Count.ShouldBe(1);
    }
}
