using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using DocumentFormat.OpenXml.Office.CoverPageProps;

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
    public string Status { get; set; } = InstitutionStatus.Active;
    public DateTime DeactivatedAt { get; set; }
    public DateTime ModifiedAt { get; set; }
    public Boolean IsApiAccessEnabled { get; set; }
    // can have many PortalUsers
    public ICollection<PortalUser> PortalUsers { get; set; } = null!;
    // can have many quotations
    public ICollection<Quotation> Quotations { get; set; } = null!;
}