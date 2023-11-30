using System.ComponentModel.DataAnnotations;

public class NewQuotation
{
    [Required]
    public string BondId { get; set; } = null!;
    [Required]
    [Range(0, 100)]
    public decimal BuyYield { get; set; }
    [Required]
    [Range(0, 100)]
    public decimal SellYield { get; set; }
    [Required]
    public decimal BuyVolume { get; set; } = 50000;
    [Required]
    public decimal SellVolume { get; set; } = 50000;

}