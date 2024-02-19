using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

public class QuotationEdit
{
    [Key]
    [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
    public string Id { get; set; } = null!;
    public double BuyingYield { get; set; }
    public double SellingYield { get; set; }
    public double BuyVolume { get; set; }
    public double SellVolume { get; set; }
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