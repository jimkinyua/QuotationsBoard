public class YieldCurveDTO
{
    public string BondId { get; set; }
    public DateTime Date { get; set; }
    public decimal Yield { get; set; }
    public decimal TotalQuotationsUsed { get; set; }
    public decimal AverageBuyYield { get; set; }
    public decimal AverageSellYield { get; set; }
}