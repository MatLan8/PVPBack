namespace PVPBack.Domain.Dtos;

public class InviteRequestDto
{
    public string SessionCode { get; set; }
    public List<string> Emails { get; set; }
}