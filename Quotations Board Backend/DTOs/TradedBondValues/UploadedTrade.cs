public class UploadedTrade
{
    public DateTime TradeDate { get; set; }
    public double ExecutedSize { get; set; }
    public double ExecutedPrice { get; set; }
    public string ExecutionID { get; set; } = null!;
    public decimal DirtyPrice { get; set; }
    public double Yield { get; set; }
    public string SecurityId { get; set; } = null!;
    public string Side { get; set; } = null!;
    public string BondId { get; set; } = null!;
    public string TradeReportID { get; set; } = null!;
}