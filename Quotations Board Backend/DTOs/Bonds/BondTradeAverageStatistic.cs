public class BondTradeAverageStatistic
{
    public string BondName { get; set; } = null!;
    public string IssueNo { get; set; } = null!;
    public string BondId { get; set; } = null!;
    public decimal AverageWeightedTradeYield { get; set; }
    public decimal TradedVolume { get; set; }
    public decimal NumberofTrades { get; set; }
    public decimal WeightedTradeBuyYield { get; set; }
    public decimal WeightedTradeSellYield { get; set; }
    public double YearsToMaturity { get; set; }
}