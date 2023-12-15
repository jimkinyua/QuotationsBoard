using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

public class GorvermentBondTradeStage
{
    [Key]
    [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
    public string Id { get; set; } = null!;
    public DateTime UploadedAt { get; set; }
    public string UploadedBy { get; set; } = null!;

}