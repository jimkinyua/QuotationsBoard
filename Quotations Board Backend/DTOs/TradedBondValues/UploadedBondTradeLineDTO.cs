public class UploadedBondTradeLineDTO
{
    public string Id { get; set; } = null!;
    public string GorvermentBondTradeStageId { get; set; } = null!;
    public string Side { get; set; } = null!;
    public string SecurityId { get; set; } = null!;
    public double ExecutedSize { get; set; }
    public double ExcecutedPrice { get; set; }
    public string ExecutionID { get; set; } = null!;
    public DateTime TransactionTime { get; set; }
    public decimal DirtyPrice { get; set; }
    public double Yield { get; set; }
    public DateTime TradeDate { get; set; }
    public string TradeReportID { get; set; } = null!;

}