using System.Net;
using System.Net.Mail;
using Microsoft.Extensions.Options;
using Loopin.Models; // EmailSettings modelini kullanmak için

namespace Loopin.Services
{
    public class EmailService
    {
        private readonly EmailSettings _settings;

        // Constructor: appsettings.json'daki EmailSettings değerlerini alır
        public EmailService(IOptions<EmailSettings> settings)
        {
            _settings = settings.Value;
        }

        // Asenkron mail gönderme metodu
        public async Task SendEmailAsync(string toEmail, string subject, string body)
        {
            using var client = new SmtpClient(_settings.SmtpServer, _settings.Port)
            {
                Credentials = new NetworkCredential(_settings.Username, _settings.Password),
                EnableSsl = true
            };

            var mailMessage = new MailMessage
            {
                From = new MailAddress(_settings.SenderEmail, _settings.SenderName),
                Subject = subject,
                Body = body,
                IsBodyHtml = true
            };

            mailMessage.To.Add(toEmail);

            await client.SendMailAsync(mailMessage);
        }
    }
}
