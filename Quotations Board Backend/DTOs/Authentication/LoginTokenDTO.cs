using System.Runtime.CompilerServices;

public class LoginTokenDTO
{
    public string Name { get; set; } = null!;
    public string Email { get; set; } = null!;
    public string token { get; set; } = null!;
    public Boolean IsSuperAdmin { get; set; } = false;
    public string Role { get; set; } = null!;
    public string InstitutionId { get; set; } = null!;
    public string InstitutionName { get; set; } = null!;
}