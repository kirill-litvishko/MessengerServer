namespace MessengerServer.Models
{
    public class User
    {
        public int Id { get; set; }
        public string Username { get; set; }
        public string PasswordHash { get; set; }
        public string Email { get; set; }
        public bool EmailConfirmed { get; set; } = false;
        public DateTime CreatedAt { get; set; }
    }
}
