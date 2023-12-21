using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

public class ImpliedYield
{
    [Key]
    [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
    public string Id { get; set; } = null!;
    public DateTime YieldDate { get; set; }
    public decimal Yield { get; set; }
    [ForeignKey("BondId")]
    public Bond Bond { get; set; } = null!;
    public string BondId { get; set; } = null!;
}