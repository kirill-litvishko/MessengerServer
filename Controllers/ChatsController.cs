using MessengerServer.Data;
using MessengerServer.Models;
using MessengerServer.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Text.Json;

namespace MessengerServer.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class ChatsController : ControllerBase
    {
        private readonly AppDbContext _context;
        private readonly ConnectionManager _connectionManager;

        public ChatsController(AppDbContext context, ConnectionManager connectionManager)
        {
            _context = context;
            _connectionManager = connectionManager;
        }

        [HttpPost("create")]
        public async Task<IActionResult> CreateChat([FromBody] CreateChatRequest request)
        {
            var chat = new Chat
            {
                Name = request.Name,
                IsGroup = request.IsGroup,
                Users = await _context.Users.Where(u => request.UserIds.Contains(u.Id)).ToListAsync()
            };

            _context.Chats.Add(chat);
            await _context.SaveChangesAsync();

            // Сообщаем всем подключенным клиентам о новом чате
            var chatNotification = new
            {
                Type = "NewChat",
                ChatId = chat.Id,
                ChatName = chat.Name,
                IsGroup = chat.IsGroup
            };

            var message = JsonSerializer.Serialize(chatNotification);
            await _connectionManager.SendMessageToAllAsync(message);

            return Ok(new { chat.Id, chat.Name, chat.IsGroup });
        }


        [HttpGet("{id}")]
        public async Task<IActionResult> GetChat(int id)
        {
            var chat = await _context.Chats
                .Include(c => c.Users)
                .Include(c => c.Messages)
                .FirstOrDefaultAsync(c => c.Id == id);

            if (chat == null) return NotFound("Chat not found");

            return Ok(chat);
        }

        [HttpPost("{chatId}/send-message")]
        public async Task<IActionResult> SendMessage(int chatId, [FromBody] Models.SendMessageRequest request)
        {
            var sender = await _context.Users.FirstOrDefaultAsync(u => u.Id == request.SenderId);
            if (sender == null)
            {
                return BadRequest("Sender does not exist.");
            }

            var chat = await _context.Chats.FirstOrDefaultAsync(c => c.Id == chatId);
            if (chat == null) return NotFound("Chat not found");

            var message = new Message
            {
                ChatId = chatId,
                SenderId = request.SenderId,
                Content = request.Content,
                SentAt = DateTime.UtcNow
            };

            _context.Messages.Add(message);
            await _context.SaveChangesAsync();

            // Уведомляем всех клиентов о новом сообщении
            var messageNotification = new
            {
                Type = "NewMessage",
                ChatId = chatId,
                SenderId = request.SenderId,
                Content = request.Content,
                SentAt = message.SentAt
            };

            var jsonMessage = JsonSerializer.Serialize(messageNotification);
            await _connectionManager.SendMessageToAllAsync(jsonMessage);

            return Ok(message);
        }

        [HttpGet("{chatId}/messages")]
        public async Task<IActionResult> GetMessages(int chatId)
        {
            var messages = await _context.Messages
                .Where(m => m.ChatId == chatId)
                .OrderBy(m => m.SentAt)
                .ToListAsync();

            return Ok(messages);
        }

        [HttpPost("check")]
        public async Task<IActionResult> CheckExistingChat([FromBody] int[] userIds)
        {
            // Отримуємо всі чати
            var chat = await _context.Chats
                .Include(c => c.Users)
                .FirstOrDefaultAsync(c => c.Users.All(u => userIds.Contains(u.Id)) && c.Users.Count == userIds.Length);

            if (chat == null)
            {
                // Якщо чат не знайдено, повертаємо 404
                return NotFound("Chat not found.");
            }

            return Ok(new { ChatId = chat.Id });
        }

    }
}
