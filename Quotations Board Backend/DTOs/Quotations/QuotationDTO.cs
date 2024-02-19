public class QuotationDTO
{
    public List<Quoteinfo> Quotes { get; set; } = null!;
    public QuoteStatistic QuoteStatistic { get; set; } = null!;
}

public class Quoteinfo
{
    public string Id { get; set; } = null!;
    public string BondIsin { get; set; } = null!;
    public string IssueNumber { get; set; } = null!;
    public double TotalBuyYield { get; set; }
    public double TotalSellYield { get; set; }
    public double AverageYield { get; set; }
    public double TotalBuyVolume { get; set; }
    public double TotalSellVolume { get; set; }
    public double AverageVolume { get; set; }
    public string InstitutionId { get; set; } = null!;
    public string BondId { get; set; } = null!;
    public DateTime CreatedAt { get; set; }
    public string UserId { get; set; } = null!;
}