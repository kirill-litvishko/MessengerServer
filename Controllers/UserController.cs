namespace MessengerServer.Controllers
{
    using MessengerServer.Data;
    using MessengerServer.Models;
    using MessengerServer.Services;
    using Microsoft.AspNetCore.Identity.Data;
    using Microsoft.AspNetCore.Mvc;
    using Microsoft.EntityFrameworkCore;
    using Org.BouncyCastle.Asn1.Ocsp;
    using System.Security.Cryptography;
    using System.Text;
    using System.IdentityModel.Tokens.Jwt;
    using System.Security.Claims;
    using Microsoft.IdentityModel.Tokens;
    using Microsoft.Extensions.Configuration;
    using Microsoft.AspNetCore.Authorization;

    [ApiController]
    [Route("api/[controller]")]
    public class UsersController : ControllerBase
    {
        private readonly AppDbContext _context;
        private readonly EmailService _emailService;
        private readonly EncryptionService _encryptionService;
        private readonly IConfiguration _configuration;

        public UsersController(AppDbContext context, EmailService emailService, EncryptionService encryptionService, IConfiguration configuration)
        {
            _context = context;
            _emailService = emailService;
            _encryptionService = encryptionService;
            _configuration = configuration;
        }

        private string GenerateJwtToken(User user)
        {
            var securityKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_configuration["Jwt:Key"]));
            var credentials = new SigningCredentials(securityKey, SecurityAlgorithms.HmacSha256);

            var claims = new[]
            {
                new Claim(JwtRegisteredClaimNames.Sub, user.Username),
                new Claim(JwtRegisteredClaimNames.Email, _encryptionService.Decrypt(user.EncryptedEmail)),
                new Claim("UserId", user.Id.ToString()),
                new Claim(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString())
            };

            var token = new JwtSecurityToken(
                issuer: _configuration["Jwt:Issuer"],
                audience: _configuration["Jwt:Audience"],
                claims: claims,
                expires: DateTime.UtcNow.AddDays(30),
                signingCredentials: credentials
            );

            return new JwtSecurityTokenHandler().WriteToken(token);
        }

        [HttpPost("register")]
        public async Task<IActionResult> Register([FromBody] Models.RegisterRequest request)
        {
            if (await _context.Users.AnyAsync(u => u.Username == request.Username))
                return BadRequest("Username already exists");

            var encryptedEmail = _encryptionService.Encrypt(request.Email);

            if (await _context.Users.AnyAsync(u => u.EncryptedEmail == encryptedEmail))
                return BadRequest("Email already in use");

            var user = new User
            {
                Username = request.Username,
                PasswordHash = HashPassword(request.Password),
                EncryptedEmail = encryptedEmail,
                EmailConfirmed = false,
                CreatedAt = DateTime.UtcNow
            };

            _context.Users.Add(user);
            await _context.SaveChangesAsync();

            var token = GenerateJwtToken(user);
            var confirmationLink = $"https://localhost:7100/api/users/confirm-email?token={token}&email={request.Email}";

            await _emailService.SendEmailAsync(
                request.Email,
                "Confirm Your Email",
                $"<p>Click <a href='{confirmationLink}'>here</a> to confirm your email.</p>"
            );

            return Ok(new { user.Id, user.Username });
        }

        [HttpPost("login")]
        public async Task<IActionResult> Login([FromBody] Models.LoginRequest request)
        {
            var user = await _context.Users.FirstOrDefaultAsync(u => u.Username == request.Username);
            if (user == null || user.PasswordHash != HashPassword(request.Password))
                return Unauthorized("Invalid credentials");

            var decryptedEmail = _encryptionService.Decrypt(user.EncryptedEmail);

            if (!user.EmailConfirmed)
                return Unauthorized("Please confirm your email before logging in.");

            return Ok(new { message = "Login successful", email = decryptedEmail });
        }

        [HttpGet("{id}")]
        [Authorize]
        public async Task<IActionResult> GetUser(int id)
        {
            var user = await _context.Users.FindAsync(id);
            if (user == null)
                return NotFound();

            return Ok(new { user.Id, user.Username, user.EncryptedEmail, user.CreatedAt });
        }

        [HttpGet]
        [Authorize]
        public async Task<IActionResult> GetUsers()
        {
            var users = await _context.Users
                .Select(user => new
                {
                    user.Id,
                    user.Username,
                    user.CreatedAt
                })
                .ToListAsync();

            return Ok(users);
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
            var encryptedEmail = _encryptionService.Encrypt(email);
            var user = await _context.Users.FirstOrDefaultAsync(u => u.EncryptedEmail == encryptedEmail);
            if (user == null) return NotFound("User not found");

            var token = GenerateJwtToken(user);
            var confirmationLink = $"https://localhost:7100/api/users/confirm-email?token={token}&email={email}";

            await _emailService.SendEmailAsync(email, "Email Confirmation",
                $"<p>Click <a href='{confirmationLink}'>here</a> to confirm your email.</p>");

            return Ok("Confirmation email sent.");
        }

        [HttpGet("confirm-email")]
        public async Task<IActionResult> ConfirmEmail(string email, string token)
        {
            var encryptedEmail = _encryptionService.Encrypt(email);

            var user = await _context.Users.FirstOrDefaultAsync(u => u.EncryptedEmail == encryptedEmail);
            if (user == null) return NotFound("User not found");

            user.EmailConfirmed = true;
            await _context.SaveChangesAsync();

            return Ok("Email confirmed.");
        }

        
        [HttpPost("send-reset-password")]
        public async Task<IActionResult> SendResetPasswordLink([FromBody] Models.ResetPasswordRequest request)
        {
            var encryptedEmail = _encryptionService.Encrypt(request.Email);
            var user = await _context.Users.FirstOrDefaultAsync(u => u.EncryptedEmail == encryptedEmail);
            if (user == null) return NotFound("User not found");

            var token = GenerateJwtToken(user);
            var resetLink = $"https://localhost:7100/api/users/confirm-reset-password?token={token}&email={request.Email}";

            user.PasswordHash = HashPassword(request.NewPassword);
            await _context.SaveChangesAsync();

            await _emailService.SendEmailAsync(
                request.Email,
                "Reset Your Password",
                $"<p>Click <a href='{resetLink}'>here</a> to confirm your password reset.</p>"
            );

            return Ok("Password reset email sent.");
        }

        [HttpGet("confirm-reset-password")]
        public async Task<IActionResult> ConfirmResetPassword(string email, string token)
        {
            var encryptedEmail = _encryptionService.Encrypt(email);
            var user = await _context.Users.FirstOrDefaultAsync(u => u.EncryptedEmail == encryptedEmail);
            if (user == null) return NotFound("User not found");

            return Ok("Password reset confirmed. You can now log in with your new password.");
        }

        [HttpGet("search")]
        public async Task<IActionResult> SearchUsers(string username)
        {
            if (string.IsNullOrWhiteSpace(username))
                return BadRequest("Username is required for search.");

            var users = await _context.Users
                .Where(u => u.Username.Contains(username)) // Пошук по частковому співпадінню
                .Select(u => new
                {
                    u.Id,
                    u.Username,
                    u.CreatedAt
                })
                .ToListAsync();

            if (!users.Any())
                return NotFound("No users found.");

            return Ok(users);
        }
    }
}
