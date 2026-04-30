namespace PVPBack.Core.Interfaces;

public interface IEmailService
{
    Task SendSessionInvite(string toEmail, string sessionCode);
}