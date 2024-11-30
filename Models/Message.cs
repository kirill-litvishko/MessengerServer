using System;

namespace MessengerServer.Models
{
    public class Message
    {
        public int Id { get; set; }
        public int ChatId { get; set; }
        public Chat Chat { get; set; }
        public int SenderId { get; set; }
        public User Sender { get; set; }
        public string Content { get; set; }
        public DateTime SentAt { get; set; }
    }

}
