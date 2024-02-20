using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

public class TBillYield
{
    [Key]
    [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
    public string Id { get; set; } = null!;
    public DateTime YieldDate { get; set; }
    public double Yield { get; set; }
    [ForeignKey("TBill")]
    public string TBillId { get; set; } = null!;
    public TBill TBill { get; set; } = null!;
}