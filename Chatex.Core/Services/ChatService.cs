using Chatex.Data;
using Chatex.Data.Entities;

namespace Chatex.Core.Services
{
    public class ChatService(DataContext context)
    {
        public List<Chat> GetAll()
        {
            return context.Chats.ToList();
        }

        public Chat? Get(Guid id)
        {
            return context.Chats.Find(id);
        }

        public void Create(string name)
        {
            var chat = new Data.Entities.Chat
            {
                Id = Guid.CreateVersion7(),
                Name = name,
                Created = DateTime.UtcNow
            };
            context.Chats.Add(chat);
            context.SaveChanges();
        }

        public void Delete(Guid id)
        {
            var chat = context.Chats.Find(id);
            if (chat != null)
            {
                context.Chats.Remove(chat);
                context.SaveChanges();
            }
        }
    }
}
