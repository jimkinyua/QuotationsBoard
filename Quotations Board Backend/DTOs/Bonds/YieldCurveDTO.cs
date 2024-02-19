public class YieldCurveDTO
{
    public string BondId { get; set; }
    public DateTime Date { get; set; }
    public double Yield { get; set; }
    public decimal TotalQuotationsUsed { get; set; }
    public double AverageBuyYield { get; set; }
    public double AverageSellYield { get; set; }
}