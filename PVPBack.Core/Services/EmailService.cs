using MailKit.Net.Smtp;
using MailKit.Security;
using MimeKit;
using PVPBack.Core.Interfaces;
using Microsoft.Extensions.Configuration;

public class EmailService : IEmailService
{
    private readonly IConfiguration _config;

    public EmailService(IConfiguration config)
    {
        _config = config;
    }

    public async Task SendSessionInvite(string toEmail, string sessionCode)
    {
        var email = _config["Email:User"];
        var password = _config["Email:Pass"];

        var message = new MimeMessage();
        message.From.Add(new MailboxAddress("TeamLens", email));
        message.To.Add(MailboxAddress.Parse(toEmail));
        message.Subject = "You're invited to a TeamLens session";

        message.Body = new TextPart("html")
        {
            Text = $@"
        <div style='font-family: Arial; padding: 20px;'>
            <h2 style='color:#10b981;'>TeamLens</h2>
            
            <p>You’ve been invited to join a session.</p>

            <p><strong>Session Code:</strong></p>
            <div style='font-size:24px; font-weight:bold; margin:10px 0;'>
                {sessionCode}
            </div>

            <p>Enter this code in the app to join the game.</p>

            <hr/>
            <p style='font-size:12px; color:#888;'>
                This is an automated message. Please do not reply.
            </p>
        </div>
    "
        };
        

        using var client = new SmtpClient();
        await client.ConnectAsync("smtp.gmail.com", 587, SecureSocketOptions.StartTls);
        await client.AuthenticateAsync(email, password);
        await client.SendAsync(message);
        await client.DisconnectAsync(true);
    }
}