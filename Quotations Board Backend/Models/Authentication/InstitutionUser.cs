using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

public class InstitutionUser
{
    [Key]
    [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
    public string Id { get; set; } = null!;
    [ForeignKey("InstitutionId")]
    public string InstitutionId { get; set; } = null!;
    public Institution Institution { get; set; } = null!;
    [ForeignKey("PortalUserId")]
    public string PortalUserId { get; set; } = null!;
    public PortalUser PortalUser { get; set; } = null!;
}