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
    public decimal TotalBuyYield { get; set; }
    public decimal TotalSellYield { get; set; }
    public decimal AverageYield { get; set; }
    public decimal TotalBuyVolume { get; set; }
    public decimal TotalSellVolume { get; set; }
    public decimal AverageVolume { get; set; }
    public string InstitutionId { get; set; } = null!;
    public string BondId { get; set; } = null!;
    public DateTime CreatedAt { get; set; }
    public string UserId { get; set; } = null!;
}