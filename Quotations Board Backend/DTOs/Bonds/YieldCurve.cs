public class YieldCurve
{
    public decimal BenchMarkTenor { get; set; }
    public decimal Yield { get; set; }
    public Boolean CanBeUsedForYieldCurve { get; set; } = true;
    public string BondUsed { get; set; } = string.Empty;
    public DateTime IssueDate { get; set; }
    public DateTime MaturityDate { get; set; }
    public decimal Coupon { get; set; }
    public Boolean BenchMarkFound { get; set; } = false;
    public double Tenure { get; set; }

}