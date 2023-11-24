using Microsoft.AspNetCore.Identity;

public class PortalUser : IdentityUser
{
    public string OrganizationName { get; set; } = null!;
    public string OrganizationAddress { get; set; } = null!;
    public string AdministationEmail { get; set; } = null!;
    public string AdministrationPhoneNumber { get; set; } = null!;
    public string AdministationName { get; set; } = null!;

}