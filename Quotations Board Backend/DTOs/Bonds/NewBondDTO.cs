public class NewBondDTO
{
    public string Isin { get; set; } = null!;
    public DateTime IssueDate { get; set; }
    public DateTime MaturityDate { get; set; }
    public decimal OutstandingValue { get; set; }
    public string CouponType { get; set; } = null!;
    public decimal CouponRate { get; set; }
    public string BondType { get; set; } = null!;
}