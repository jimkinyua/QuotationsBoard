using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

public class GorvermentBondTradeStage
{
    [Key]
    [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
    public string Id { get; set; } = null!;
    public DateTime TargetDate { get; set; }
    public string UploadedBy { get; set; } = null!;
    public virtual ICollection<GorvermentBondTradeLineStage> GorvermentBondTradeLineStage { get; set; } = null!;

}