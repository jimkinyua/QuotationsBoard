public class PortalUserDTO
{
    public string Id { get; set; } = null!;
    public string FirstName { get; set; } = null!;
    public string LastName { get; set; } = null!;
    public string Email { get; set; } = null!;
    public string InstitutionId { get; set; } = null!;
    public string Role { get; set; } = null!;
    public DateTime CreatedAt { get; set; } = DateTime.Now;
}