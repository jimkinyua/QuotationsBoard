using System.ComponentModel.DataAnnotations;

public class NewBondDTO
{
    [Required]
    public string Isin { get; set; } = null!;
    [Required]
    public DateTime IssueDate { get; set; }
    [Required]
    public DateTime MaturityDate { get; set; }
    [Required]
    public decimal OutstandingValue { get; set; }
    [Required]
    public string CouponType { get; set; } = null!;
    [Required]
    public decimal CouponRate { get; set; }
    [Required]
    public string BondType { get; set; } = null!;
    [Required]
    public string IssueNumber { get; set; } = null!;
    [Required]
    public string BondCategory { get; set; } = null!;
}