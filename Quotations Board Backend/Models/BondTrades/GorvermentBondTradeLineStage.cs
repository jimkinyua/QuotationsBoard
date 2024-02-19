using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

public class GorvermentBondTradeLineStage
{
    [Key]
    [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
    public string Id { get; set; } = null!;
    [ForeignKey("GorvermentBondTradeStage")]
    public string GorvermentBondTradeStageId { get; set; } = null!;
    public GorvermentBondTradeStage GorvermentBondTradeStage { get; set; } = null!;
    public string Side { get; set; } = null!;
    public string SecurityId { get; set; } = null!;
    public double ExecutedSize { get; set; }
    public double ExcecutedPrice { get; set; }
    public string ExecutionID { get; set; } = null!;
    public DateTime TransactionTime { get; set; }
    public decimal DirtyPrice { get; set; }
    public double Yield { get; set; }
    public DateTime TradeDate { get; set; }
    public string? TransactionID { get; set; }

}