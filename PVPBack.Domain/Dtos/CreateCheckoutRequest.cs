using System.ComponentModel.DataAnnotations;

namespace PVPBack.Domain.Dtos;

public class CreateCheckoutRequest
{
    public Guid UserId { get; set; }
    [Range(1, 400)] public int Credits { get; set; }
}