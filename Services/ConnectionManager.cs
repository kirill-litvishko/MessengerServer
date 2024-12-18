using System.Collections.Concurrent;
using System.Net.WebSockets;
using System.Text;

namespace MessengerServer.Services
{
    public class ConnectionManager
    {
        // Хранит все активные WebSocket-соединения
        private readonly ConcurrentDictionary<string, WebSocket> _connections = new();

        /// <summary>
        /// Добавление нового WebSocket-соединения.
        /// </summary>
        /// <param name="id">Уникальный идентификатор соединения</param>
        /// <param name="socket">WebSocket-соединение</param>
        public void AddConnection(string id, WebSocket socket)
        {
            _connections.TryAdd(id, socket);
            Console.WriteLine($"Added connection {id}");
        }

        /// <summary>
        /// Удаление соединения.
        /// </summary>
        /// <param name="id">ID соединения</param>
        public void RemoveConnection(string id)
        {
            _connections.TryRemove(id, out _);
            Console.WriteLine($"Removed connection {id}");
        }

        /// <summary>
        /// Отправка сообщения определенному клиенту.
        /// </summary>
        /// <param name="id">ID клиента</param>
        /// <param name="message">Сообщение</param>
        public async Task SendMessageToClientAsync(string id, string message)
        {
            try
            {
                if (_connections.TryGetValue(id, out WebSocket socket) && socket.State == WebSocketState.Open)
                {
                    var messageBytes = Encoding.UTF8.GetBytes(message);
                    await socket.SendAsync(new ArraySegment<byte>(messageBytes), WebSocketMessageType.Text, true, CancellationToken.None);
                }
                else
                {
                    Console.WriteLine($"Socket for client {id} is not open or not found.");
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error sending message to client {id}: {ex.Message}");
            }
        }

        /// <summary>
        /// Отправка сообщения всем подключенным клиентам.
        /// </summary>
        /// <param name="message">Сообщение</param>
        public async Task SendMessageToAllAsync(string message)
        {
            try
            {
                var messageBytes = Encoding.UTF8.GetBytes(message);
                foreach (var (id, socket) in _connections)
                {
                    if (socket.State == WebSocketState.Open)
                    {
                        await socket.SendAsync(new ArraySegment<byte>(messageBytes), WebSocketMessageType.Text, true, CancellationToken.None);
                    }
                    else
                    {
                        Console.WriteLine($"Socket for client {id} is not open.");
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error sending message to all clients: {ex.Message}");
            }
        }
    }
}
