using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.AspNetCore.Identity;

public class PortalUser : IdentityUser
{
    public string FirstName { get; set; } = null!;
    public string LastName { get; set; } = null!;
    [ForeignKey("InstitutionId")]
    public string InstitutionId { get; set; } = null!;
    public string Email { get; set; } = null!;
    public Institution Institution { get; set; } = null!;
}