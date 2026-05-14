using System.ComponentModel.DataAnnotations;

namespace PVPBack.Domain.Dtos;

public class AddCreditsDto
{
    [Range(1, 100000)]
    public int Credits { get; set; }
}