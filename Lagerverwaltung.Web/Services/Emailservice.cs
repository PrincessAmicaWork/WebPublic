using System.Net;
using System.Net.Mail;

namespace Lagerverwaltung.Web.Services
{
    public interface IEmailService
    {
        Task SendAsync(string to, string subject, string htmlBody);
    }

    public class EmailService : IEmailService
    {
        private const string DoNotReplyText = "DO NOT REPLY TO THIS EMAIL";
        private const string DoNotReplyMarker = "data-mail-notice='do-not-reply'";

        private readonly IConfiguration _cfg;
        private readonly ILogger<EmailService> _log;

        public EmailService(IConfiguration cfg, ILogger<EmailService> log)
        {
            _cfg = cfg;
            _log = log;
        }

        public async Task SendAsync(string to, string subject, string htmlBody)
        {
            var host = _cfg["Email:SmtpHost"] ?? "";
            var portText = _cfg["Email:SmtpPort"];
            var sender = _cfg["Email:SmtpSender"] ?? "";
            var username = _cfg["Email:SmtpUsername"] ?? "";
            var password = _cfg["Email:SmtpPassword"] ?? "";
            var body = EnsureDoNotReplyNotice(htmlBody);

            int port = 587;
            if (!string.IsNullOrWhiteSpace(portText) && int.TryParse(portText, out var parsedPort))
                port = parsedPort;

            if (string.IsNullOrWhiteSpace(host))
                throw new InvalidOperationException("Email:SmtpHost is missing.");

            if (string.IsNullOrWhiteSpace(sender))
                throw new InvalidOperationException("Email:SmtpSender is missing.");

            if (string.IsNullOrWhiteSpace(username))
                throw new InvalidOperationException("Email:SmtpUsername is missing.");

            if (string.IsNullOrWhiteSpace(password))
                throw new InvalidOperationException("Email:SmtpPassword is missing.");

            using var client = new SmtpClient(host, port)
            {
                EnableSsl = true,
                DeliveryMethod = SmtpDeliveryMethod.Network,
                UseDefaultCredentials = false,
                Credentials = new NetworkCredential(username, password)
            };

            using var msg = new MailMessage
            {
                From = new MailAddress(sender),
                Subject = subject,
                Body = body,
                IsBodyHtml = true
            };

            var recipients = ParseRecipients(to);

            foreach (var recipient in recipients)
                msg.To.Add(recipient);

            try
            {
                await client.SendMailAsync(msg);
                _log.LogInformation("Email sent to {RecipientCount} recipients: {Subject}", recipients.Count, subject);
            }
            catch (Exception ex)
            {
                _log.LogError(ex, "Failed to send email to {RecipientCount} recipients", recipients.Count);
                throw;
            }
        }

        private static List<string> ParseRecipients(string to)
        {
            var recipients = (to ?? "")
                .Split(',', StringSplitOptions.TrimEntries)
                .Where(x => !string.IsNullOrWhiteSpace(x))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();

            if (recipients.Count == 0)
                throw new InvalidOperationException("Email recipient is missing.");

            foreach (var recipient in recipients)
            {
                _ = new MailAddress(recipient);
            }

            return recipients;
        }

        private static string EnsureDoNotReplyNotice(string htmlBody)
        {
            var body = htmlBody ?? "";

            if (ContainsDoNotReplyNotice(body))
                return body;

            var notice = BuildDoNotReplyNotice();
            var bodyTagIndex = body.IndexOf("<body", StringComparison.OrdinalIgnoreCase);

            if (bodyTagIndex >= 0)
            {
                var bodyTagEndIndex = body.IndexOf('>', bodyTagIndex);

                if (bodyTagEndIndex >= 0)
                    return body.Insert(bodyTagEndIndex + 1, Environment.NewLine + notice + Environment.NewLine);
            }

            return notice + Environment.NewLine + body;
        }

        private static bool ContainsDoNotReplyNotice(string htmlBody)
        {
            return htmlBody.Contains(DoNotReplyMarker, StringComparison.OrdinalIgnoreCase)
                || htmlBody.Contains(DoNotReplyText, StringComparison.OrdinalIgnoreCase)
                || htmlBody.Contains("DO NOT REPLY", StringComparison.OrdinalIgnoreCase);
        }

        private static string BuildDoNotReplyNotice()
        {
            return $@"
<div {DoNotReplyMarker} style='font-family:Arial,Helvetica,sans-serif;max-width:780px;margin:0 auto 14px auto;padding:10px 14px;border:1px solid #ffd1d6;background:#fff7f8;color:#EA0016;font-size:13px;font-weight:bold;text-align:center;text-transform:uppercase;letter-spacing:.04em;border-radius:12px;'>
  {DoNotReplyText}
</div>".Trim();
        }
    }
}