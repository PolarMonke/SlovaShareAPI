using System.Net;
using System.Net.Mail;

public class EmailService
{
    private readonly IConfiguration _configuration;
    public EmailService(IConfiguration configuration)
    {
        _configuration = configuration;
    }
    
    public async Task SendEmailAsync(string email, string subject, string message)
    {
        var emailSettings = _configuration.GetSection("EmailSettings");
        
        var client = new SmtpClient(emailSettings["Host"], int.Parse(emailSettings["Port"]))
        {
            Credentials = new NetworkCredential(
                emailSettings["Username"], 
                emailSettings["Password"]),
            EnableSsl = true,
            DeliveryMethod = SmtpDeliveryMethod.Network,
            UseDefaultCredentials = false
        };

        var mailMessage = new MailMessage
        {
            From = new MailAddress(
                emailSettings["SenderEmail"], 
                emailSettings["SenderName"]),
            Subject = subject,
            Body = message,
            IsBodyHtml = true
        };

        mailMessage.To.Add(email);

        try
        {
            await client.SendMailAsync(mailMessage);
        }
        catch (SmtpException ex)
        {
            throw new ApplicationException($"SMTP Error: {ex.StatusCode} - {ex.Message}");
        }
        catch (Exception ex)
        {
            throw new ApplicationException($"Email sending failed: {ex.Message}");
        }
        finally
        {
            client.Dispose();
            mailMessage.Dispose();
        }
    }
}