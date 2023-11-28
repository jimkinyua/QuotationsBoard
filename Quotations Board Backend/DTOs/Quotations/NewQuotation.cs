public class NewQuotation
{
    public string BondId { get; set; } = null!;
    public decimal BuyYield { get; set; }
    public decimal SellYield { get; set; }
    public decimal Volume { get; set; }
    public string IssueNumber { get; set; } = null!;
}