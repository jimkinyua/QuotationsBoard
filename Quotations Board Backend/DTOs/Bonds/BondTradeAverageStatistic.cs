public class BondTradeAverageStatistic
{
    public string BondName { get; set; } = null!;
    public string IssueNo { get; set; } = null!;
    public string BondId { get; set; } = null!;
    public double AverageWeightedTradeYield { get; set; }
    public double TradedVolume { get; set; }
    public decimal NumberofTrades { get; set; }
    public double WeightedTradeBuyYield { get; set; }
    public double WeightedTradeSellYield { get; set; }
    public double YearsToMaturity { get; set; }
}