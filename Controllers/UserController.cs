namespace MessengerServer.Controllers
{
    using MessengerServer.Data;
    using MessengerServer.Models;
    using MessengerServer.Services;
    using Microsoft.AspNetCore.Mvc;
    using Microsoft.EntityFrameworkCore;
    using System.Security.Cryptography;
    using System.Text;

    [ApiController]
    [Route("api/[controller]")]
    public class UsersController : ControllerBase
    {
        private readonly AppDbContext _context;
        private readonly EmailService _emailService;

        public UsersController(AppDbContext context, EmailService emailService)
        {
            _context = context;
            _emailService = emailService;
        }

        [HttpPost("register")]
        public async Task<IActionResult> Register([FromBody] MessengerServer.Models.RegisterRequest request)
        {
            if (await _context.Users.AnyAsync(u => u.Username == request.Username))
                return BadRequest("Username already exists");
            if (await _context.Users.AnyAsync(u => u.Email == request.Email))
                return BadRequest("Email already in use");

            var user = new User
            {
                Username = request.Username,
                PasswordHash = HashPassword(request.Password),
                Email = request.Email,
                CreatedAt = DateTime.UtcNow
            };

            _context.Users.Add(user);
            await _context.SaveChangesAsync();

            return Ok(new { user.Id, user.Username, user.Email });
        }

        [HttpPost("login")]
        public async Task<IActionResult> Login([FromBody] MessengerServer.Models.LoginRequest request)
        {
            var user = await _context.Users.FirstOrDefaultAsync(u => u.Username == request.Username);
            if (user == null || user.PasswordHash != HashPassword(request.Password))
                return Unauthorized("Invalid credentials");

            if (!user.EmailConfirmed)
                return Unauthorized("Please confirm your email before logging in.");

            // В реальном приложении сгенерируй JWT токен здесь
            return Ok(new { message = "Login successful" });
        }

        [HttpGet("{id}")]
        public async Task<IActionResult> GetUser(int id)
        {
            var user = await _context.Users.FindAsync(id);
            if (user == null)
                return NotFound();

            return Ok(new { user.Id, user.Username, user.Email, user.CreatedAt });
        }

        private string HashPassword(string password)
        {
            using var sha256 = SHA256.Create();
            var bytes = sha256.ComputeHash(Encoding.UTF8.GetBytes(password));
            return Convert.ToBase64String(bytes);
        }

        [HttpPost("send-confirmation")]
        public async Task<IActionResult> SendEmailConfirmation([FromBody] string email)
        {
            var user = await _context.Users.FirstOrDefaultAsync(u => u.Email == email);
            if (user == null) return NotFound("User not found");

            var token = Guid.NewGuid().ToString(); // В реальном приложении лучше использовать JWT или другой способ.
            var confirmationLink = $"https://localhost:5106/api/users/confirm-email?token={token}&email={email}";

            // Отправка письма
            await _emailService.SendEmailAsync(email, "Email Confirmation",
                $"<p>Click <a href='{confirmationLink}'>here</a> to confirm your email.</p>");

            return Ok("Confirmation email sent.");
        }

        [HttpGet("confirm-email")]
        public async Task<IActionResult> ConfirmEmail(string email, string token)
        {
            // В реальном приложении нужно проверить токен
            var user = await _context.Users.FirstOrDefaultAsync(u => u.Email == email);
            if (user == null) return NotFound("User not found");

            user.EmailConfirmed = true;
            await _context.SaveChangesAsync();

            return Ok("Email confirmed.");
        }


    }

}
