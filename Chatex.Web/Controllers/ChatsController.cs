using Chatex.Core.Services;
using Chatex.Web.Models;
using Microsoft.AspNetCore.Mvc;

namespace Chatex.Web.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class ChatsController(ChatService chatService) : ControllerBase
    {
        // GET: api/chats
        [HttpGet]
        public IActionResult Get()
        {
            var chats = chatService.GetAll();
            return Ok(chats);
        }

        // GET api/chats/{id}
        [HttpGet("{id}")]
        public IActionResult Get(Guid id)
        {
            var chat = chatService.Get(id);
            return Ok(chat);
        }

        // POST api/chats
        [HttpPost]
        public IActionResult Post([FromBody] ChatRequest request)
        {
            chatService.Create(request.Name);
            return Created();
        }

        // PUT api/<ChatsController>/5
        //[HttpPut("{id}")]
        //public void Put(int id, [FromBody] string value)
        //{
        //}

        // DELETE api/chats/{id}
        [HttpDelete("{id}")]
        public IActionResult Delete(Guid id)
        {
            chatService.Delete(id);
            return NoContent();
        }
    }
}
