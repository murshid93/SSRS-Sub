using System;
using System.Net.Mail;
using System.Threading.Tasks;

namespace SSRS_Subscription.Utils
{
    public static class EmailHelper
    {
        public static async Task SendFileReadyNotificationAsync(string toEmail, string subject, string filePath, string smtpServer, int port, string senderEmail, string smtpUser, string smtpPass)
        {
            try
            {
                using var mailMessage = new MailMessage
                {
                    From = new MailAddress(senderEmail),
                    Subject = $"Report Ready: {subject}",
                    Body = $"Dear User,\n\nYour requested report has been generated and saved successfully.\n\nYou can access it at the following location:\n{filePath}\n\nRegards,\nBI Team",
                    IsBodyHtml = false
                };

                if (toEmail.Contains(','))
                {
                    var emails = toEmail.Split(',');
                    foreach (var email in emails)
                    {
                        if (!string.IsNullOrWhiteSpace(email))
                            mailMessage.To.Add(email.Trim());
                    }
                }
                else
                {
                    mailMessage.To.Add(toEmail);
                }

                using var smtpClient = new SmtpClient(smtpServer, port);
                
                // Explicit credentials and SSL enabled
                smtpClient.UseDefaultCredentials = false;
                smtpClient.Credentials = new System.Net.NetworkCredential(smtpUser, smtpPass);
                smtpClient.EnableSsl = true; 

                await smtpClient.SendMailAsync(mailMessage);
                Console.WriteLine($"[SUCCESS] Notification email sent to {toEmail}");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[WARNING] File was created, but failed to send email notification: {ex.Message}");
            }
        }
    }
}