using System.ComponentModel.DataAnnotations;

namespace PVPBack.Domain.Dtos;

public class RegisterDto
{
    [Required]
    [EmailAddress]
    public string Email { get; set; } = null!;

    [Required]
    [MinLength(3)]
    public string DisplayName { get; set; } = null!;

    [Required]
    [MinLength(8)]
    [RegularExpression(@"^(?=.*[A-Z])(?=.*\d).+$",
        ErrorMessage = "Password must contain at least one uppercase letter and one number.")]
    public string Password { get; set; } = null!;
}