using System.ComponentModel.DataAnnotations;

public class NewTbillYield
{
    [Required]
    public string TBillId { get; set; } = null!;
    [Required]
    public decimal Yield { get; set; }
}