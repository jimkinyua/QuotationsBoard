using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

public class TBillImpliedYield
{
    [Key]
    [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
    public string Id { get; set; } = null!;
    public DateTime Date { get; set; }
    public double Yield { get; set; }
    public double Tenor { get; set; }
    [ForeignKey("TBill")]
    public string TBillId { get; set; } = null!;
    public virtual TBill TBill { get; set; } = null!;
}