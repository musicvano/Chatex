using Chat.Data;

namespace Chat.Core.Services
{
    public class ChatService(Context context)
    {
        public List<Data.Entities.Chat> GetAll()
        {
            return context.Chats.ToList();
        }

        public Data.Entities.Chat? Get(Guid id)
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
