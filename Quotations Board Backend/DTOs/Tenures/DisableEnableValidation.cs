using System.ComponentModel.DataAnnotations;

public class DisableEnableValidation
{
    [Required]
    public Boolean IsValidationEnabled { get; set; }
    [Required]
    public string Id { get; set; } = null!;
}