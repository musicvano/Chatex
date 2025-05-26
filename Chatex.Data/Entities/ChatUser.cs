using Microsoft.EntityFrameworkCore;

namespace Chatex.Data.Entities
{
    [PrimaryKey(nameof(ChatId), nameof(UserId))]
    public class ChatUser
    {        
        public Guid ChatId { get; set; }
        public Guid UserId { get; set; }
        public DateTime Joined { get; set; }
        public DateTime Seen { get; set; }
    }
}
