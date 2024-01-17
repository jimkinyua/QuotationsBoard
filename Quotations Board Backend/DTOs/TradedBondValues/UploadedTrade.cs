public class UploadedTrade
{
    public DateTime TradeDate { get; set; }
    public decimal ExecutedSize { get; set; }
    public decimal ExecutedPrice { get; set; }
    public string ExecutionID { get; set; } = null!;
    public decimal DirtyPrice { get; set; }
    public decimal Yield { get; set; }
    public string SecurityId { get; set; } = null!;
    public string Side { get; set; } = null!;
    public string BondId { get; set; } = null!;
}