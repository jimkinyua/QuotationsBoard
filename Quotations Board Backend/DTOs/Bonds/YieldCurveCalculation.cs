public class YieldCurveDataSet
{
    public double Yield { get; set; }
    public DateTime IssueDate { get; set; }
    public DateTime MaturityDate { get; set; }
    public double Tenure { get; set; }
    public string BondUsed { get; set; } = string.Empty;
    public Boolean isInterpolated { get; set; } = false;
    public Boolean isTbill { get; set; } = false;
}