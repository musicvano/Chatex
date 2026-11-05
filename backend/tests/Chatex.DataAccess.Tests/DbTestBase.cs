using Chatex.DataAccess.Abstractions;
using Chatex.DataAccess.Entities;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.DependencyInjection;

// Tests share one database that is wiped before each test; xunit.runner.json turns off parallel runs.
[assembly: AssemblyFixture(typeof(PostgresFixture))]

namespace Chatex.DataAccess.Tests;

/// <summary>Data access layer on a clean database, plus seeding helpers.</summary>
public abstract class DbTestBase(PostgresFixture fixture) : DatabaseTest(fixture)
{
    protected override void ConfigureServices(IServiceCollection services, string connectionString) =>
        services.AddDataAccess(connectionString, o => o.RootPath = StorageRoot);

    protected async Task<User> CreateUserAsync(string userName)
    {
        await using var scope = NewScope();
        var user = new User { UserName = userName, Email = $"{userName}@example.test" };
        var result = await Get<UserManager<User>>(scope).CreateAsync(user, "Passw0rd!");
        result.Succeeded.ShouldBeTrue(string.Join("; ", result.Errors.Select(e => e.Description)));

        Get<IUserRepository>(scope).AddProfile(new UserProfile { UserId = user.Id, DisplayName = userName.ToUpperInvariant() });
        await Get<IUnitOfWork>(scope).SaveChangesAsync(Ct);
        return user;
    }

    protected async Task<Chat> CreateChatAsync(
        ChatType type, User owner, IEnumerable<User> others, DateTimeOffset? lastActivityAt = null)
    {
        await using var scope = NewScope();
        var othersList = others.ToList();
        var chat = new Chat
        {
            Type = type,
            Title = type == ChatType.Direct ? null : $"{type} chat",
            DirectKey = type == ChatType.Direct ? Chat.MakeDirectKey(owner.Id, othersList.Single().Id) : null,
            CreatedById = owner.Id,
            LastActivityAt = lastActivityAt ?? default,
        };

        var ownerRole = type == ChatType.Direct ? ChatMemberRole.Member : ChatMemberRole.Owner;
        var otherRole = type == ChatType.Channel ? ChatMemberRole.Subscriber : ChatMemberRole.Member;
        chat.Members.Add(new ChatMember { UserId = owner.Id, Role = ownerRole });
        foreach (var other in othersList)
        {
            chat.Members.Add(new ChatMember { UserId = other.Id, Role = otherRole });
        }

        Get<IChatRepository>(scope).Add(chat);
        await Get<IUnitOfWork>(scope).SaveChangesAsync(Ct);
        return chat;
    }

    /// <summary>Adds messages whose UUIDv7 ids are one millisecond apart, so their order is known.</summary>
    protected async Task<List<Message>> AddMessagesAsync(Chat chat, User sender, int count)
    {
        await using var scope = NewScope();
        var start = DateTimeOffset.UtcNow.AddMinutes(-count);
        var messages = Enumerable.Range(0, count)
            .Select(i => new Message
            {
                Id = Guid.CreateVersion7(start.AddMilliseconds(i)),
                ChatId = chat.Id,
                SenderId = sender.Id,
                Body = $"message {i}",
            })
            .ToList();

        var repository = Get<IMessageRepository>(scope);
        messages.ForEach(repository.Add);
        await Get<IUnitOfWork>(scope).SaveChangesAsync(Ct);
        return messages;
    }
}
