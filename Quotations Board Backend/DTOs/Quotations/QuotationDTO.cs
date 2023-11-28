public class QuotationDTO
{
    public string Id { get; set; } = null!;
    public decimal BuyingYield { get; set; }
    public decimal SellingYield { get; set; }
    public decimal Volume { get; set; }
    public string InstitutionId { get; set; } = null!;
    public string BondId { get; set; } = null!;
    public DateTime CreatedAt { get; set; }
    public string UserId { get; set; } = null!;

}