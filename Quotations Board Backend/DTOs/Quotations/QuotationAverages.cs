public class QuotationAverages
{
    public List<QuoteAverageData> Averages { get; set; } = null!;
}

public class QuoteAverageData
{
    public string BondId { get; set; } = null!;
    public string BondIsin { get; set; } = null!;
    public string IssueNumber { get; set; } = null!;
    public decimal AverageYield { get; set; }
    public decimal AverageBuyVolume { get; set; }
    public decimal AverageSellVolume { get; set; }
    public decimal AverageSellYield { get; set; }
    public decimal AverageBuyYield { get; set; }
    public decimal AverageVolume { get; set; }
}