using Chatex.Business.Common;
using Chatex.DataAccess.Abstractions;
using Chatex.DataAccess.Entities;

namespace Chatex.Business.Chats;

/// <summary>Loads a chat together with the caller's membership, applying the visibility rules.</summary>
internal sealed class ChatAccess(IChatRepository chats, IChatMemberRepository members)
{
    /// <summary>
    /// The chat if the caller is a member or the chat is public. <c>Member</c> is null for a public chat the caller has not joined.
    /// Private chats of others are reported as not found.
    /// </summary>
    public async Task<Result<(Chat Chat, ChatMember? Member)>> GetVisibleAsync(
        Guid userId, Guid chatId, CancellationToken cancellationToken)
    {
        var chat = await chats.GetByIdAsync(chatId, cancellationToken);
        if (chat is null)
        {
            return ChatErrors.NotFound;
        }

        var member = await members.GetAsync(chatId, userId, cancellationToken);
        if (member is null && chat.PublicHandle is null)
        {
            return ChatErrors.NotFound;
        }

        return (chat, member);
    }

    /// <summary>The chat and the caller's membership; fails unless the caller is a member.</summary>
    public async Task<Result<(Chat Chat, ChatMember Member)>> GetMembershipAsync(
        Guid userId, Guid chatId, CancellationToken cancellationToken)
    {
        var visible = await GetVisibleAsync(userId, chatId, cancellationToken);
        if (!visible.IsSuccess)
        {
            return visible.Error;
        }

        var (chat, member) = visible.Value;
        return member is null ? ChatErrors.NotAMember : (chat, member);
    }
}
