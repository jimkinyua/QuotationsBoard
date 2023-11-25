using System.ComponentModel.DataAnnotations;

public class ForgotPasswordDTO
{
    [Required]
    [EmailAddress]
    public string Email { get; set; } = null!;
}