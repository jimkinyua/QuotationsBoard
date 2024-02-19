public class QuotationAverages
{
    public List<QuoteAverageData> Averages { get; set; } = null!;
}

public class QuoteAverageData
{
    public string BondId { get; set; } = null!;
    public string BondIsin { get; set; } = null!;
    public string IssueNumber { get; set; } = null!;
    public double AverageYield { get; set; }
    public double AverageBuyVolume { get; set; }
    public double AverageSellVolume { get; set; }
    public double AverageSellYield { get; set; }
    public double AverageBuyYield { get; set; }
    public double AverageVolume { get; set; }
}