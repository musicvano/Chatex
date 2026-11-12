using Chatex.Business.Abstractions;
using Chatex.Business.Common;
using Chatex.Business.Messages;
using Chatex.Business.Users;
using Chatex.DataAccess.Abstractions;
using Chatex.DataAccess.Entities;
using Chatex.DataAccess.Exceptions;
using Chatex.DataAccess.Models;
using FluentValidation;

namespace Chatex.Business.Chats;

public interface IChatService
{
    /// <summary>Returns the direct chat with <paramref name="otherUserId"/>, creating it on first use.</summary>
    Task<Result<ChatDto>> OpenDirectAsync(Guid userId, Guid otherUserId, CancellationToken cancellationToken = default);

    /// <summary>Creates a group or channel owned by the caller.</summary>
    Task<Result<ChatDto>> CreateAsync(Guid userId, CreateChatCommand command, CancellationToken cancellationToken = default);

    Task<Result<ChatDto>> GetAsync(Guid userId, Guid chatId, CancellationToken cancellationToken = default);

    /// <summary>The caller's chats, most recently active first.</summary>
    Task<Result<PageDto<ChatListItemDto>>> GetMyChatsAsync(
        Guid userId, string? cursor, int? limit, CancellationToken cancellationToken = default);

    Task<Result<ChatSummaryDto[]>> SearchPublicAsync(string query, int? limit, CancellationToken cancellationToken = default);

    Task<Result<ChatDto>> UpdateAsync(Guid userId, Guid chatId, UpdateChatCommand command, CancellationToken cancellationToken = default);

    Task<Result<ChatDto>> SetAvatarAsync(Guid userId, Guid chatId, FileUpload upload, CancellationToken cancellationToken = default);

    Task<Result<ChatDto>> RemoveAvatarAsync(Guid userId, Guid chatId, CancellationToken cancellationToken = default);

    Task<Result<Stream>> OpenAvatarAsync(Guid userId, Guid chatId, CancellationToken cancellationToken = default);

    /// <summary>Owner only. The chat disappears for everyone.</summary>
    Task<Result> DeleteAsync(Guid userId, Guid chatId, CancellationToken cancellationToken = default);

    /// <summary>Joins a public group or channel. Joining twice is not an error.</summary>
    Task<Result<ChatDto>> JoinAsync(Guid userId, Guid chatId, CancellationToken cancellationToken = default);

    Task<Result> LeaveAsync(Guid userId, Guid chatId, CancellationToken cancellationToken = default);

    /// <summary>Adds users to a group or channel; returns how many were not members yet.</summary>
    Task<Result<int>> AddMembersAsync(
        Guid userId, Guid chatId, IReadOnlyList<Guid> userIds, CancellationToken cancellationToken = default);

    Task<Result> RemoveMemberAsync(Guid userId, Guid chatId, Guid memberUserId, CancellationToken cancellationToken = default);

    /// <summary>Owner only. Giving someone <see cref="ChatMemberRole.Owner"/> transfers ownership; the caller becomes an admin.</summary>
    Task<Result> ChangeRoleAsync(
        Guid userId, Guid chatId, Guid memberUserId, ChatMemberRole role, CancellationToken cancellationToken = default);

    Task<Result<PageDto<ChatMemberDto>>> GetMembersAsync(
        Guid userId, Guid chatId, string? cursor, int? limit, CancellationToken cancellationToken = default);

    Task<Result> SetMutedAsync(Guid userId, Guid chatId, bool muted, CancellationToken cancellationToken = default);

    /// <summary>Moves the caller's read position forward to <paramref name="messageId"/>; never backwards.</summary>
    Task<Result> MarkReadAsync(Guid userId, Guid chatId, Guid messageId, CancellationToken cancellationToken = default);

    /// <summary>Ids of all the user's chats; the SignalR hub joins their groups on connect.</summary>
    Task<IReadOnlyList<Guid>> GetMyChatIdsAsync(Guid userId, CancellationToken cancellationToken = default);

    Task<bool> IsMemberAsync(Guid chatId, Guid userId, CancellationToken cancellationToken = default);
}

internal sealed class ChatService(
    IChatRepository chats,
    IChatMemberRepository members,
    IUserRepository users,
    IMessageRepository messages,
    IUnitOfWork unitOfWork,
    IFileStorage storage,
    ChatAccess chatAccess,
    AvatarStore avatars,
    IChatNotifier notifier,
    TimeProvider timeProvider,
    IValidator<CreateChatCommand> createValidator,
    IValidator<UpdateChatCommand> updateValidator) : IChatService
{
    private const int MinQueryLength = 2;

    public async Task<Result<ChatDto>> OpenDirectAsync(Guid userId, Guid otherUserId, CancellationToken cancellationToken = default)
    {
        if (userId == otherUserId)
        {
            return ChatErrors.DirectWithSelf;
        }

        if ((await users.GetExistingIdsAsync([otherUserId], cancellationToken)).Count == 0)
        {
            return UserErrors.NotFound;
        }

        var chat = await chats.GetDirectAsync(userId, otherUserId, cancellationToken);
        if (chat is null)
        {
            chat = new Chat
            {
                Type = ChatType.Direct,
                DirectKey = Chat.MakeDirectKey(userId, otherUserId),
                CreatedById = userId,
            };
            chat.Members.Add(new ChatMember { UserId = userId, Role = ChatMemberRole.Member });
            chat.Members.Add(new ChatMember { UserId = otherUserId, Role = ChatMemberRole.Member });
            chats.Add(chat);

            try
            {
                await unitOfWork.SaveChangesAsync(cancellationToken);
                await notifier.MembersAddedAsync(chat.Id, [userId, otherUserId], cancellationToken);
            }
            catch (UniqueConstraintException)
            {
                // A concurrent request created the chat first; use that one.
                chat = await chats.GetDirectAsync(userId, otherUserId, cancellationToken)
                    ?? throw new InvalidOperationException("The direct chat exists but could not be loaded.");
            }
        }

        var member = await members.GetAsync(chat.Id, userId, cancellationToken);
        return await ToDtoAsync(chat, member, userId, cancellationToken);
    }

    public async Task<Result<ChatDto>> CreateAsync(Guid userId, CreateChatCommand command, CancellationToken cancellationToken = default)
    {
        if (await createValidator.ValidateToErrorAsync(command, cancellationToken) is { } invalid)
        {
            return invalid;
        }

        var memberIds = (command.MemberIds ?? []).Where(id => id != userId).Distinct().ToList();
        if (memberIds.Count > 0 && (await users.GetExistingIdsAsync(memberIds, cancellationToken)).Count != memberIds.Count)
        {
            return ChatErrors.UnknownUsers;
        }

        var handle = command.PublicHandle?.ToLowerInvariant();
        if (handle is not null && await chats.GetByHandleAsync(handle, cancellationToken) is not null)
        {
            return ChatErrors.HandleTaken;
        }

        var chat = new Chat
        {
            Type = command.Type,
            Title = command.Title.Trim(),
            Description = NullIfBlank(command.Description),
            PublicHandle = handle,
            CreatedById = userId,
        };
        var owner = new ChatMember { UserId = userId, Role = ChatMemberRole.Owner };
        chat.Members.Add(owner);
        var role = ChatPermissions.RegularRole(command.Type);
        foreach (var memberId in memberIds)
        {
            chat.Members.Add(new ChatMember { UserId = memberId, Role = role });
        }

        chats.Add(chat);
        try
        {
            await unitOfWork.SaveChangesAsync(cancellationToken);
        }
        catch (UniqueConstraintException)
        {
            return ChatErrors.HandleTaken;
        }

        await notifier.MembersAddedAsync(chat.Id, [userId, .. memberIds], cancellationToken);
        return await ToDtoAsync(chat, owner, userId, cancellationToken);
    }

    public async Task<Result<ChatDto>> GetAsync(Guid userId, Guid chatId, CancellationToken cancellationToken = default)
    {
        var access = await chatAccess.GetVisibleAsync(userId, chatId, cancellationToken);
        if (!access.IsSuccess)
        {
            return access.Error;
        }

        var (chat, member) = access.Value;
        return await ToDtoAsync(chat, member, userId, cancellationToken);
    }

    public async Task<Result<PageDto<ChatListItemDto>>> GetMyChatsAsync(
        Guid userId, string? cursor, int? limit, CancellationToken cancellationToken = default)
    {
        if (!CursorCodec.TryDecode(cursor, out ChatListCursor? after))
        {
            return Paging.InvalidCursor;
        }

        var page = await chats.GetUserChatsAsync(userId, after, Paging.Clamp(limit), cancellationToken);
        return new PageDto<ChatListItemDto>(page.Items.Select(i => i.ToDto()).ToList(), CursorCodec.Encode(page.NextCursor));
    }

    public async Task<Result<ChatSummaryDto[]>> SearchPublicAsync(
        string query, int? limit, CancellationToken cancellationToken = default)
    {
        query = query?.Trim() ?? "";
        if (query.Length < MinQueryLength)
        {
            return CommonErrors.QueryTooShort;
        }

        var found = await chats.SearchPublicAsync(query, Paging.Clamp(limit), cancellationToken);
        return found.Select(c => c.ToSummaryDto()).ToArray();
    }

    public async Task<Result<ChatDto>> UpdateAsync(
        Guid userId, Guid chatId, UpdateChatCommand command, CancellationToken cancellationToken = default)
    {
        if (await updateValidator.ValidateToErrorAsync(command, cancellationToken) is { } invalid)
        {
            return invalid;
        }

        var access = await RequireAsync(userId, chatId, ChatPermissions.CanEditInfo, cancellationToken);
        if (!access.IsSuccess)
        {
            return access.Error;
        }

        var (chat, member) = access.Value;
        var handle = command.PublicHandle?.ToLowerInvariant();
        if (handle is not null && handle != chat.PublicHandle && await chats.GetByHandleAsync(handle, cancellationToken) is not null)
        {
            return ChatErrors.HandleTaken;
        }

        chat.Title = command.Title.Trim();
        chat.Description = NullIfBlank(command.Description);
        chat.PublicHandle = handle;
        try
        {
            await unitOfWork.SaveChangesAsync(cancellationToken);
        }
        catch (UniqueConstraintException)
        {
            return ChatErrors.HandleTaken;
        }

        await notifier.ChatUpdatedAsync(chatId, cancellationToken);
        return await ToDtoAsync(chat, member, userId, cancellationToken);
    }

    public async Task<Result<ChatDto>> SetAvatarAsync(
        Guid userId, Guid chatId, FileUpload upload, CancellationToken cancellationToken = default)
    {
        var access = await RequireAsync(userId, chatId, ChatPermissions.CanEditInfo, cancellationToken);
        if (!access.IsSuccess)
        {
            return access.Error;
        }

        var saved = await avatars.SaveAsync(upload, cancellationToken);
        if (!saved.IsSuccess)
        {
            return saved.Error;
        }

        return await ReplaceAvatarAsync(access.Value.Chat, access.Value.Member, saved.Value, userId, cancellationToken);
    }

    public async Task<Result<ChatDto>> RemoveAvatarAsync(Guid userId, Guid chatId, CancellationToken cancellationToken = default)
    {
        var access = await RequireAsync(userId, chatId, ChatPermissions.CanEditInfo, cancellationToken);
        if (!access.IsSuccess)
        {
            return access.Error;
        }

        return await ReplaceAvatarAsync(access.Value.Chat, access.Value.Member, null, userId, cancellationToken);
    }

    public async Task<Result<Stream>> OpenAvatarAsync(Guid userId, Guid chatId, CancellationToken cancellationToken = default)
    {
        var access = await chatAccess.GetVisibleAsync(userId, chatId, cancellationToken);
        if (!access.IsSuccess)
        {
            return access.Error;
        }

        if (access.Value.Chat.AvatarPath is not { } path)
        {
            return CommonErrors.AvatarNotFound;
        }

        return await storage.OpenReadAsync(path, cancellationToken);
    }

    public async Task<Result> DeleteAsync(Guid userId, Guid chatId, CancellationToken cancellationToken = default)
    {
        var access = await RequireAsync(userId, chatId, ChatPermissions.CanDelete, cancellationToken);
        if (!access.IsSuccess)
        {
            return access.Error;
        }

        access.Value.Chat.DeletedAt = timeProvider.GetUtcNow();
        await unitOfWork.SaveChangesAsync(cancellationToken);
        await notifier.ChatDeletedAsync(chatId, cancellationToken);
        return Result.Success;
    }

    public async Task<Result<ChatDto>> JoinAsync(Guid userId, Guid chatId, CancellationToken cancellationToken = default)
    {
        var chat = await chats.GetByIdAsync(chatId, cancellationToken);
        if (chat?.PublicHandle is null)
        {
            return ChatErrors.NotFound;
        }

        var member = await members.GetAsync(chatId, userId, cancellationToken);
        if (member is null)
        {
            if (chat.Type == ChatType.Group && await members.CountAsync(chatId, cancellationToken) >= ChatLimits.MaxGroupMembers)
            {
                return ChatErrors.ChatFull;
            }

            member = new ChatMember { ChatId = chatId, UserId = userId, Role = ChatPermissions.RegularRole(chat.Type) };
            members.Add(member);
            try
            {
                await unitOfWork.SaveChangesAsync(cancellationToken);
                await notifier.MembersAddedAsync(chatId, [userId], cancellationToken);
            }
            catch (UniqueConstraintException)
            {
                // Joined by a concurrent request; the change tracker was cleared, so reload both.
                return await GetAsync(userId, chatId, cancellationToken);
            }
        }

        return await ToDtoAsync(chat, member, userId, cancellationToken);
    }

    public async Task<Result> LeaveAsync(Guid userId, Guid chatId, CancellationToken cancellationToken = default)
    {
        var access = await chatAccess.GetMembershipAsync(userId, chatId, cancellationToken);
        if (!access.IsSuccess)
        {
            return access.Error;
        }

        var (chat, member) = access.Value;
        if (chat.Type == ChatType.Direct)
        {
            return ChatErrors.NotForDirectChats;
        }

        if (member.Role == ChatMemberRole.Owner)
        {
            return ChatErrors.OwnerMustTransfer;
        }

        members.Remove(member);
        await unitOfWork.SaveChangesAsync(cancellationToken);
        await notifier.MemberRemovedAsync(chatId, userId, cancellationToken);
        return Result.Success;
    }

    public async Task<Result<int>> AddMembersAsync(
        Guid userId, Guid chatId, IReadOnlyList<Guid> userIds, CancellationToken cancellationToken = default)
    {
        if (userIds is not { Count: > 0 and <= ChatLimits.MaxMembersPerRequest })
        {
            return Error.Validation(
                "userIds", "invalid_user_count", $"Add between 1 and {ChatLimits.MaxMembersPerRequest} users at a time.");
        }

        var access = await RequireAsync(userId, chatId, ChatPermissions.CanManageMembers, cancellationToken);
        if (!access.IsSuccess)
        {
            return access.Error;
        }

        var chat = access.Value.Chat;
        var ids = userIds.Distinct().ToList();
        if ((await users.GetExistingIdsAsync(ids, cancellationToken)).Count != ids.Count)
        {
            return ChatErrors.UnknownUsers;
        }

        var toAdd = ids.Except(await members.GetExistingUserIdsAsync(chatId, ids, cancellationToken)).ToList();
        if (toAdd.Count == 0)
        {
            return 0;
        }

        if (chat.Type == ChatType.Group
            && await members.CountAsync(chatId, cancellationToken) + toAdd.Count > ChatLimits.MaxGroupMembers)
        {
            return ChatErrors.ChatFull;
        }

        var role = ChatPermissions.RegularRole(chat.Type);
        foreach (var id in toAdd)
        {
            members.Add(new ChatMember { ChatId = chatId, UserId = id, Role = role });
        }

        await unitOfWork.SaveChangesAsync(cancellationToken);
        await notifier.MembersAddedAsync(chatId, toAdd, cancellationToken);
        return toAdd.Count;
    }

    public async Task<Result> RemoveMemberAsync(
        Guid userId, Guid chatId, Guid memberUserId, CancellationToken cancellationToken = default)
    {
        if (memberUserId == userId)
        {
            return ChatErrors.UseLeave;
        }

        var access = await RequireAsync(userId, chatId, ChatPermissions.CanManageMembers, cancellationToken);
        if (!access.IsSuccess)
        {
            return access.Error;
        }

        var target = await members.GetAsync(chatId, memberUserId, cancellationToken);
        if (target is null)
        {
            return ChatErrors.MemberNotFound;
        }

        if (!ChatPermissions.CanRemove(access.Value.Member, target))
        {
            return ChatErrors.NotAllowed;
        }

        members.Remove(target);
        await unitOfWork.SaveChangesAsync(cancellationToken);
        await notifier.MemberRemovedAsync(chatId, memberUserId, cancellationToken);
        return Result.Success;
    }

    public async Task<Result> ChangeRoleAsync(
        Guid userId, Guid chatId, Guid memberUserId, ChatMemberRole role, CancellationToken cancellationToken = default)
    {
        if (memberUserId == userId)
        {
            return Error.Validation("userId", "own_role", "You cannot change your own role.");
        }

        var access = await RequireAsync(
            userId, chatId, (chat, member) => chat.Type != ChatType.Direct && member.Role == ChatMemberRole.Owner, cancellationToken);
        if (!access.IsSuccess)
        {
            return access.Error;
        }

        var (chat, owner) = access.Value;
        if (role != ChatMemberRole.Owner && role != ChatMemberRole.Admin && role != ChatPermissions.RegularRole(chat.Type))
        {
            return ChatErrors.InvalidRole;
        }

        var target = await members.GetAsync(chatId, memberUserId, cancellationToken);
        if (target is null)
        {
            return ChatErrors.MemberNotFound;
        }

        target.Role = role;
        if (role == ChatMemberRole.Owner)
        {
            owner.Role = ChatMemberRole.Admin;
        }

        await unitOfWork.SaveChangesAsync(cancellationToken);
        await notifier.ChatUpdatedAsync(chatId, cancellationToken);
        return Result.Success;
    }

    public async Task<Result<PageDto<ChatMemberDto>>> GetMembersAsync(
        Guid userId, Guid chatId, string? cursor, int? limit, CancellationToken cancellationToken = default)
    {
        if (!CursorCodec.TryDecode(cursor, out Guid? after))
        {
            return Paging.InvalidCursor;
        }

        var access = await RequireAsync(userId, chatId, ChatPermissions.CanListMembers, cancellationToken);
        if (!access.IsSuccess)
        {
            return access.Error;
        }

        var page = await members.GetMembersAsync(chatId, after, Paging.Clamp(limit), cancellationToken);
        return new PageDto<ChatMemberDto>(page.Items.Select(m => m.ToDto()).ToList(), CursorCodec.Encode(page.NextCursor));
    }

    public async Task<Result> SetMutedAsync(Guid userId, Guid chatId, bool muted, CancellationToken cancellationToken = default)
    {
        var access = await chatAccess.GetMembershipAsync(userId, chatId, cancellationToken);
        if (!access.IsSuccess)
        {
            return access.Error;
        }

        access.Value.Member.IsMuted = muted;
        await unitOfWork.SaveChangesAsync(cancellationToken);
        return Result.Success;
    }

    public async Task<Result> MarkReadAsync(Guid userId, Guid chatId, Guid messageId, CancellationToken cancellationToken = default)
    {
        var access = await chatAccess.GetMembershipAsync(userId, chatId, cancellationToken);
        if (!access.IsSuccess)
        {
            return access.Error;
        }

        if (!await messages.ExistsInChatAsync(chatId, messageId, cancellationToken))
        {
            return MessageErrors.NotFound;
        }

        var moved = await members.MarkReadAsync(chatId, userId, messageId, cancellationToken);

        // Channels can have many subscribers; their read positions are private.
        if (moved && access.Value.Chat.Type != ChatType.Channel)
        {
            await notifier.MessagesReadAsync(chatId, userId, messageId, cancellationToken);
        }

        return Result.Success;
    }

    public Task<IReadOnlyList<Guid>> GetMyChatIdsAsync(Guid userId, CancellationToken cancellationToken = default) =>
        members.GetChatIdsForUserAsync(userId, cancellationToken);

    public Task<bool> IsMemberAsync(Guid chatId, Guid userId, CancellationToken cancellationToken = default) =>
        members.IsMemberAsync(chatId, userId, cancellationToken);

    /// <summary>The caller's membership, failing with NotAllowed (or NotForDirectChats) when <paramref name="allowed"/> says no.</summary>
    private async Task<Result<(Chat Chat, ChatMember Member)>> RequireAsync(
        Guid userId, Guid chatId, Func<Chat, ChatMember, bool> allowed, CancellationToken cancellationToken)
    {
        var access = await chatAccess.GetMembershipAsync(userId, chatId, cancellationToken);
        if (!access.IsSuccess)
        {
            return access;
        }

        var (chat, member) = access.Value;
        if (allowed(chat, member))
        {
            return access;
        }

        return chat.Type == ChatType.Direct ? ChatErrors.NotForDirectChats : ChatErrors.NotAllowed;
    }

    private async Task<ChatDto> ReplaceAvatarAsync(
        Chat chat, ChatMember member, string? avatarPath, Guid userId, CancellationToken cancellationToken)
    {
        var previous = chat.AvatarPath;
        chat.AvatarPath = avatarPath;
        await unitOfWork.SaveChangesAsync(cancellationToken);
        await avatars.DeleteAsync(previous, cancellationToken);
        await notifier.ChatUpdatedAsync(chat.Id, cancellationToken);
        return await ToDtoAsync(chat, member, userId, cancellationToken);
    }

    private async Task<ChatDto> ToDtoAsync(Chat chat, ChatMember? member, Guid userId, CancellationToken cancellationToken)
    {
        var memberCount = await members.CountAsync(chat.Id, cancellationToken);

        UserSummaryDto? peer = null;
        if (chat.Type == ChatType.Direct)
        {
            var both = await members.GetMembersAsync(chat.Id, null, 2, cancellationToken);
            peer = both.Items.FirstOrDefault(m => m.UserId != userId)?.User.ToSummaryDto();
        }

        return new ChatDto(
            chat.Id,
            chat.Type,
            chat.Title,
            chat.Description,
            chat.PublicHandle,
            chat.AvatarPath is not null,
            chat.CreatedAt,
            memberCount,
            member?.Role,
            member?.IsMuted ?? false,
            peer);
    }

    private static string? NullIfBlank(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}
