using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

public class BondTradeLine
{
    [Key]
    [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
    public string Id { get; set; } = null!;
    [ForeignKey("BondTrade")]
    public string BondTradeId { get; set; } = null!;
    [ForeignKey("Bond")]
    public string BondId { get; set; } = null!;
    public string Side { get; set; } = null!;
    public string SecurityId { get; set; } = null!;
    public decimal ExecutedSize { get; set; }
    public decimal ExcecutedPrice { get; set; }
    public string ExecutionID { get; set; } = null!;
    public DateTime TransactionTime { get; set; }
    public decimal DirtyPrice { get; set; }
    public decimal Yield { get; set; }
    public virtual BondTrade BondTrade { get; set; } = null!;

}