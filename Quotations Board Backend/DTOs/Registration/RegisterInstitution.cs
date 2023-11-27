using System.ComponentModel.DataAnnotations;

public class RegisterInstitution
{
    [Required]
    public string Name { get; set; } = null!;
    [Required]
    public string InstitutionEmail { get; set; } = null!;
    [Required]
    public string Address { get; set; } = null!;
    [Required]
    public string ContactPerson { get; set; } = null!;
    [Required]
    public string ContactEmail { get; set; } = null!;
    [Required]
    public string ContactPhone { get; set; } = null!;
    [Required]
    public string InstitutionType { get; set; } = null!;


}