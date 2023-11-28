using System.ComponentModel.DataAnnotations;

public class NewQuotation
{
    [Required]
    public string BondId { get; set; } = null!;
    [Required]
    public decimal BuyYield { get; set; }
    [Required]
    public decimal SellYield { get; set; }
    [Required]
    public decimal Volume { get; set; }

}