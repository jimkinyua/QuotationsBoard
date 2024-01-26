public class UploadedBondTradeLineDTO
{
    public string Id { get; set; } = null!;
    public string GorvermentBondTradeStageId { get; set; } = null!;
    public string Side { get; set; } = null!;
    public string SecurityId { get; set; } = null!;
    public decimal ExecutedSize { get; set; }
    public decimal ExcecutedPrice { get; set; }
    public string ExecutionID { get; set; } = null!;
    public DateTime TransactionTime { get; set; }
    public decimal DirtyPrice { get; set; }
    public decimal Yield { get; set; }
    public DateTime TradeDate { get; set; }
    public string TradeReportID { get; set; } = null!;

}