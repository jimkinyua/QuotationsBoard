using System.ComponentModel.DataAnnotations;

public class TwoFactorLoginDTO
{
    [Required]
    public string TwoFactorCode { get; set; } = null!;
    [Required]
    public string Email { get; set; } = null!;

}
