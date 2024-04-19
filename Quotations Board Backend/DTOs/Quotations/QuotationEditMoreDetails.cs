using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

public class QuotationEditMoreDetails : QuotationEdit
{
    public string Id { get; set; } = null!;
    public double PresentBuyingYield { get; set; }
    public double PresentSellingYield { get; set; }
    public double PresentBuyVolume { get; set; }
    public double PresentSellVolume { get; set; }
    public string InstitutionId { get; set; } = null!;
    public string BondId { get; set; } = null!;
    public string BondName { get; set; } = null!;
    public DateTime CreatedAt { get; set; }
    public string UserId { get; set; } = null!;
    public string? Comment { get; set; }
    public string Status { get; set; } = null!;
    public string? OrganizationName { get; set; }
    public string? EditSubmittedBy { get; set; }
    public string? RejectionReason { get; set; }

    [ForeignKey("Quotation")]
    public string QuotationId { get; set; } = null!;
    public virtual Quotation Quotation { get; set; } = null!;
}