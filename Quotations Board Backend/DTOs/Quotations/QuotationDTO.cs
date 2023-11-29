public class QuotationDTO
{
    public List<Quoteinfo> Quotes { get; set; } = null!;
    public QuoteStatistic QuoteStatistic { get; set; } = null!;
}

public class Quoteinfo
{
    public string Id { get; set; } = null!;
    public decimal BuyingYield { get; set; }
    public decimal SellingYield { get; set; }
    public decimal BuyVolume { get; set; }
    public decimal SellVolume { get; set; }
    public string InstitutionId { get; set; } = null!;
    public string BondId { get; set; } = null!;
    public DateTime CreatedAt { get; set; }
    public string UserId { get; set; } = null!;
}