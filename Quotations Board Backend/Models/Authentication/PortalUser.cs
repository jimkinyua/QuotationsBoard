using Microsoft.AspNetCore.Identity;

public class PortalUser : IdentityUser
{
    // Can have many InstitutionUsers
    public ICollection<InstitutionUser> InstitutionUsers { get; set; } = null!;
}