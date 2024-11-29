namespace MessengerServer.Services
{
    using MailKit.Net.Smtp;
    using MimeKit;

    public class EmailService
    {
        private readonly IConfiguration _configuration;

        public EmailService(IConfiguration configuration)
        {
            _configuration = configuration;
        }

        public async Task SendEmailAsync(string toEmail, string subject, string body)
        {
            var smtpSettings = _configuration.GetSection("SmtpSettings");

            var message = new MimeMessage();
            message.From.Add(new MailboxAddress(
                smtpSettings["FromName"],
                smtpSettings["FromEmail"]
            ));
            message.To.Add(new MailboxAddress("", toEmail));
            message.Subject = subject;
            message.Body = new TextPart("html") { Text = body };

            using var client = new SmtpClient();
            try
            {
                await client.ConnectAsync(smtpSettings["Host"], int.Parse(smtpSettings["Port"]), MailKit.Security.SecureSocketOptions.StartTls);
                await client.AuthenticateAsync(smtpSettings["Username"], smtpSettings["Password"]);
                await client.SendAsync(message);
            }
            finally
            {
                await client.DisconnectAsync(true);
            }
        }
    }

}
