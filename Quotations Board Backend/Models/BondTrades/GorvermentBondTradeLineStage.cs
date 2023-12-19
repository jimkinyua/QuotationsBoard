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
    public decimal ExecutedSize { get; set; }
    public decimal ExcecutedPrice { get; set; }
    public string ExecutionID { get; set; } = null!;
    public DateTime TransactionTime { get; set; }
    public decimal DirtyPrice { get; set; }
    public decimal Yield { get; set; }
    public DateTime TradeDate { get; set; }

}