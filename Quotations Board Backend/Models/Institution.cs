using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

public class Institution
{
    [Key]
    [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
    public string Id { get; set; } = null!;
    public DateTime CreatedAt { get; set; }
    public string OrganizationName { get; set; } = null!;
    public string OrganizationAddress { get; set; } = null!;
    public string OrganizationEmail { get; set; } = null!;
    public string InstitutionType { get; set; } = null!;
    // can have many InstitutionUsers
    public ICollection<InstitutionUser> InstitutionUsers { get; set; } = null!;
}