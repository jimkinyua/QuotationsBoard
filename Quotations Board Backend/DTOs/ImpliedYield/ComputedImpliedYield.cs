public class ComputedImpliedYield
{
    public string BondId { get; set; } = null!;
    public DateTime YieldDate { get; set; }
    public double Yield { get; set; }
    public string ReasonForSelection { get; set; } = null!;
    public decimal SelectedYield { get; set; }
    public double TradedYield { get; set; }
    public double QuotedYield { get; set; }
    public double PreviousYield { get; set; }
}