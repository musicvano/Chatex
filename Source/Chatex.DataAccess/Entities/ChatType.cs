namespace Chatex.DataAccess.Entities;

public enum ChatType
{
    /// <summary>One-to-one conversation between exactly two users.</summary>
    Direct,

    /// <summary>Many members, everyone can post.</summary>
    Group,

    /// <summary>Broadcast: only owner and admins post, everyone else subscribes.</summary>
    Channel,
}
