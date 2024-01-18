public class ComputedImpliedYield
{
    public string BondId { get; set; } = null!;
    public DateTime YieldDate { get; set; }
    public decimal Yield { get; set; }
    public string ReasonForSelection { get; set; } = null!;
    public decimal SelectedYield { get; set; }
    public decimal TradedYield { get; set; }
    public decimal QuotedYield { get; set; }
    public decimal PreviousYield { get; set; }
}