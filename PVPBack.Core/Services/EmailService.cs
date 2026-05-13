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
        
        var joinLink = $"http://localhost:5173/?join=true&code={sessionCode}";

        var message = new MimeMessage();
        message.From.Add(new MailboxAddress("TeamLens", email));
        message.To.Add(MailboxAddress.Parse(toEmail));
        message.Subject = "You're invited to a TeamLens session";
        
message.Body = new TextPart("html")
{
    Text = $@"
    <div style='
        margin:0;
        padding:40px 20px;
        background-color:#f3f4f6;
        font-family:Arial, Helvetica, sans-serif;
    '>

        <table width='100%' cellpadding='0' cellspacing='0'>
            <tr>
                <td align='center'>

                    <div style='
                        max-width:560px;
                        background:white;
                        border-radius:18px;
                        overflow:hidden;
                        box-shadow:0 8px 24px rgba(0,0,0,0.08);
                    '>

                        <!-- Header -->
                        <div style='
                            background:linear-gradient(135deg, #10b981, #059669);
                            padding:32px 24px;
                            text-align:center;
                        '>
                            <h1 style='
                                margin:0;
                                color:white;
                                font-size:32px;
                                font-weight:700;
                                letter-spacing:0.5px;
                            '>
                                TeamLens
                            </h1>
                        </div>

                        <!-- Content -->
                        <div style='padding:40px 32px;'>

                            <p style='
                                margin:0 0 28px;
                                color:#4b5563;
                                font-size:16px;
                                line-height:1.6;
                            '>
                                Someone invited you to join a TeamLens game session.
                                Use the code below or click the button to join instantly.
                            </p>

                            <!-- Session Code -->
                            <div style='text-align:center; margin:32px 0;'>

                                <p style='
                                    margin:0 0 12px;
                                    color:#6b7280;
                                    font-size:14px;
                                    text-transform:uppercase;
                                    letter-spacing:1px;
                                '>
                                    Game Session Code
                                </p>

                                <div style='
                                    display:inline-block;
                                    background:#ecfdf5;
                                    color:#059669;
                                    padding:18px 32px;
                                    border-radius:14px;
                                    font-size:32px;
                                    font-weight:700;
                                    letter-spacing:6px;
                                    border:2px dashed #10b981;
                                '>
                                    {sessionCode}
                                </div>
                            </div>

                            <!-- Button -->
                            <div style='text-align:center; margin-top:36px;'>
                                <a href='{joinLink}'
                                   style='
                                       display:inline-block;
                                       background:#10b981;
                                       color:white;
                                       text-decoration:none;
                                       padding:14px 28px;
                                       border-radius:12px;
                                       font-size:16px;
                                       font-weight:700;
                                       box-shadow:0 4px 12px rgba(16,185,129,0.3);
                                   '>
                                    Join Session
                                </a>
                            </div>

                            <!-- Link -->
                            <div style='margin-top:40px;'>

                                <p style='
                                    margin:0 0 10px;
                                    color:#6b7280;
                                    font-size:14px;
                                '>
                                    Or copy and paste this link:
                                </p>

                                <div style='
                                    background:#f9fafb;
                                    border:1px solid #e5e7eb;
                                    border-radius:10px;
                                    padding:14px;
                                    word-break:break-all;
                                    color:#2563eb;
                                    font-size:14px;
                                    line-height:1.5;
                                '>
                                    {joinLink}
                                </div>
                            </div>

                        </div>

                        <!-- Footer -->
                        <div style='
                            border-top:1px solid #e5e7eb;
                            padding:20px 32px;
                            background:#fafafa;
                            text-align:center;
                        '>

                            <p style='
                                margin:0;
                                color:#9ca3af;
                                font-size:12px;
                                line-height:1.6;
                            '>
                                This is an automated message from TeamLens.<br/>
                                Please do not reply to this email.
                            </p>

                        </div>

                    </div>

                </td>
            </tr>
        </table>

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