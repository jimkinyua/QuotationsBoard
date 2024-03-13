public class BondAverageStatistic
{
    public string BondName { get; set; } = null!;
    public string BondId { get; set; } = null!;
    public double AverageWeightedTradeYield { get; set; }
    public double AverageWeightedQuotedYield { get; set; }
    public double TradedVolume { get; set; }
    public double QuotedVolume { get; set; }
    public decimal NumberofTrades { get; set; }
    public decimal NumberofQuotes { get; set; }
    public double WeightedTradeBuyYield { get; set; }
    public decimal WeightedTradeSellYield { get; set; }
    public double WeightedQuotedBuyYield { get; set; }
    public double WeightedQuotedSellYield { get; set; }
    public double YearsToMaturity { get; set; }
    public string BondCategory { get; set; } = null!;
    public string BondType { get; set; } = null!;
    public double PreviousImpliedYield { get; set; }
    public string ISIN { get; set; } = null!;
    public double DaysImpliedYield { get; set; }
    public DateTime IssueDate { get; set; }

}