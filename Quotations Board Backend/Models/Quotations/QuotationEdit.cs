using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

public class QuotationEdit
{
    [Key]
    [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
    public string Id { get; set; } = null!;
    public decimal BuyingYield { get; set; }
    public decimal SellingYield { get; set; }
    public decimal BuyVolume { get; set; }
    public decimal SellVolume { get; set; }
    public string InstitutionId { get; set; } = null!;
    public string BondId { get; set; } = null!;
    public DateTime CreatedAt { get; set; }
    public string UserId { get; set; } = null!;
    public string? Comment { get; set; }
    public string Status { get; set; } = null!;
    public string? RejectionReason { get; set; }

    [ForeignKey("Quotation")]
    public string QuotationId { get; set; } = null!;
    public virtual Quotation Quotation { get; set; } = null!;
}