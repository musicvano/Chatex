namespace Chatex.Data.Entities
{
    public class Message
    {
        public Guid Id { get; set; }
        public required string Content { get; set; }
        public User? User { get; set; }
        public Chat? Chat { get; set; }
    }
}
