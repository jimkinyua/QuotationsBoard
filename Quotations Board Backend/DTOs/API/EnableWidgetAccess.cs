using System.ComponentModel.DataAnnotations;

public class EnableWidgetAccess
{
    [Required]
    public string InstitutionId { get; set; } = null!;
    [Required]
    public bool IsApiAccessEnabled { get; set; }
}