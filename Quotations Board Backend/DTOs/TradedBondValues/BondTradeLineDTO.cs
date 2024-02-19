public class BondTradeLineDTO
{
    public string Side { get; set; } = null!;
    public string BondId { get; set; } = null!;
    public double Yield { get; set; }
    public decimal DirtyPrice { get; set; }
    public DateTime TransactionTime { get; set; }
    public string ExecutionID { get; set; } = null!;
    public double ExcecutedPrice { get; set; }
    public double ExecutedSize { get; set; }
}