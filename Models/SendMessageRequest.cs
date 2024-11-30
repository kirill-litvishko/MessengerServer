namespace MessengerServer.Models
{
    public class SendMessageRequest
    {
        public int SenderId { get; set; }
        public string Content { get; set; }
    }
}
