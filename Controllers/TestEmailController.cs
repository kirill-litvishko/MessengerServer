using MessengerServer.Services;
using Microsoft.AspNetCore.Mvc;

namespace MessengerServer.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class TestEmailController : ControllerBase
    {
        private readonly EmailService _emailService;

        public TestEmailController(EmailService emailService)
        {
            _emailService = emailService;
        }

        [HttpPost("send-test")]
        public async Task<IActionResult> SendTestEmail([FromBody] string recipientEmail)
        {
            try
            {
                await _emailService.SendEmailAsync(
                    recipientEmail,
                    "Test Email",
                    "<p>This is a test email from our application.</p>"
                );
                return Ok("Test email sent successfully.");
            }
            catch (Exception ex)
            {
                return BadRequest($"Error sending email: {ex.Message}");
            }
        }
    }

}
