using System.ComponentModel.DataAnnotations;

public class NewPortalUser
{
    [Required]
    public string FirstName { get; set; } = null!;
    [Required]
    public string LastName { get; set; } = null!;
    [Required]
    [EmailAddress]
    public string Email { get; set; } = null!;
    [Required]
    public string InstitutionId { get; set; } = null!;
    public string Role { get; set; } = CustomRoles.Dealer;
}