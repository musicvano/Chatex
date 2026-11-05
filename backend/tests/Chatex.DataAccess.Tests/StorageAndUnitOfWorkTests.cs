using Chatex.DataAccess.Abstractions;
using Chatex.DataAccess.Entities;
using Chatex.DataAccess.Exceptions;

namespace Chatex.DataAccess.Tests;

public sealed class StorageAndUnitOfWorkTests(PostgresFixture fixture) : DbTestBase(fixture)
{
    [Fact]
    public async Task SaveChangesAsync_turns_a_unique_violation_into_UniqueConstraintException()
    {
        var alice = await CreateUserAsync("alice");

        await using var scope = NewScope();
        var chats = Get<IChatRepository>(scope);
        chats.Add(new Chat { Type = ChatType.Channel, Title = "a", PublicHandle = "taken", CreatedById = alice.Id });
        chats.Add(new Chat { Type = ChatType.Channel, Title = "b", PublicHandle = "taken", CreatedById = alice.Id });

        var error = await Should.ThrowAsync<UniqueConstraintException>(() => Get<IUnitOfWork>(scope).SaveChangesAsync(Ct));

        error.ConstraintName.ShouldBe("ix_chats_public_handle");
    }

    [Fact]
    public async Task LocalFileStorage_saves_reads_and_deletes_a_file()
    {
        await using var scope = NewScope();
        var storage = Get<IFileStorage>(scope);

        var path = await storage.SaveAsync(new MemoryStream([1, 2, 3]), Ct);
        path.ShouldMatch(@"^\d{4}/\d{2}/[0-9a-f]{32}$");

        await using (var read = await storage.OpenReadAsync(path, Ct))
        {
            var copy = new MemoryStream();
            await read.CopyToAsync(copy, Ct);
            copy.ToArray().ShouldBe([1, 2, 3]);
        }

        await storage.DeleteAsync(path, Ct);
        await Should.ThrowAsync<FileNotFoundException>(() => storage.OpenReadAsync(path, Ct));
    }

    [Fact]
    public async Task LocalFileStorage_rejects_paths_outside_its_root()
    {
        await using var scope = NewScope();

        await Should.ThrowAsync<ArgumentException>(() => Get<IFileStorage>(scope).OpenReadAsync("../secret.txt", Ct));
    }
}
