using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

public class AuditTrail
{
    [Key]
    [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
    public string Id { get; set; } = null!;
    public string EntityName { get; set; } = null!;
    public string EntityId { get; set; } = null!;
    public string Action { get; set; } = null!;
    public string ActionBy { get; set; } = null!;
    public string ActionDate { get; set; } = null!;
    public string ActionDetails { get; set; } = null!;
    public string InstitutionId { get; set; } = null!;
    public DateTime ActionTime { get; set; } = DateTime.Now;


}