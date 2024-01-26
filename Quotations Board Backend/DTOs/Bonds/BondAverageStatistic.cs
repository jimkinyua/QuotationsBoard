public class BondAverageStatistic
{
    public string BondName { get; set; } = null!;
    public string BondId { get; set; } = null!;
    public decimal AverageWeightedTradeYield { get; set; }
    public decimal AverageWeightedQuotedYield { get; set; }
    public decimal TradedVolume { get; set; }
    public decimal QuotedVolume { get; set; }
    public decimal NumberofTrades { get; set; }
    public decimal NumberofQuotes { get; set; }
    public decimal WeightedTradeBuyYield { get; set; }
    public decimal WeightedTradeSellYield { get; set; }
    public decimal WeightedQuotedBuyYield { get; set; }
    public decimal WeightedQuotedSellYield { get; set; }
    public double YearsToMaturity { get; set; }
    public string BondCategory { get; set; } = null!;
    public string BondType { get; set; } = null!;

}