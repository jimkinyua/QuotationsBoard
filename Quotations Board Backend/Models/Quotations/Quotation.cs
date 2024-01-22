using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

public class Quotation
{
    [Key]
    [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
    public string Id { get; set; } = null!;
    public decimal BuyingYield { get; set; }
    public decimal SellingYield { get; set; }
    public decimal BuyVolume { get; set; }
    public decimal SellVolume { get; set; }
    [ForeignKey("Institution")]
    public string InstitutionId { get; set; } = null!;
    public Institution Institution { get; set; } = null!;
    [ForeignKey("Bond")]
    public string BondId { get; set; } = null!;
    public Bond Bond { get; set; } = null!;
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
    [ForeignKey("User")]
    public string UserId { get; set; } = null!;
    public virtual ICollection<QuotationEdit> QuotationEdits { get; set; } = null!;

}