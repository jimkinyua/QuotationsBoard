public class QuotationEditDTO : EditQuotation
{
    public String QuotationId { get; set; } = null!;
    public DateTime CreatedAt { get; set; }
    public string OrganizationName { get; set; } = null!;
    public string Status { get; set; } = null!;
    public string EditSubmittedBy { get; set; } = null!;
    public string ISIN { get; set; } = null!;
}