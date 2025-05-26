namespace Chatex.Data.Entities
{
    public class Chat
    {
        public Guid Id { get; set; }
        public required string Name { get; set; }
        public DateTime Created { get; set; }
    }
}
