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
    public decimal TotalWeightedTradeBuyYield { get; set; }
    public decimal TotalWeightedTradeSellYield { get; set; }
    public decimal TotalWeightedQuotedBuyYield { get; set; }
    public decimal TotalWeightedQuotedSellYield { get; set; }

}