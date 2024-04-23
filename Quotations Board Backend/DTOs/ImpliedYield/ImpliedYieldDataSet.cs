public class ImpliedYieldDataSet
{
    public string BondId { get; set; } = null!;
    public Boolean IsBenchMarkBond { get; set; }
    public Boolean IsTbill { get; set; }
    public double Yield { get; set; }
    public double SelectedYield { get; set; }
    public DateTime YieldDate { get; set; }
    public double Tenor { get; set; }
    public string BondCategory { get; set; } = null!;
    public DateTime IssueDate { get; set; }
    public DateTime MaturityDate { get; set; }
}