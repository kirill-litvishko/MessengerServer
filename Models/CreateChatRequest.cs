namespace MessengerServer.Models
{
    public class CreateChatRequest
    {
        public string Name { get; set; }
        public bool IsGroup { get; set; }
        public List<int> UserIds { get; set; }
    }

}
