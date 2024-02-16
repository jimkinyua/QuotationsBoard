public class HistoricalTbill
{
    public string Id { get; set; } = null!;
    // public string IssueNumber { get; set; } = null!;
    public DateTime IssueDate { get; set; }
    public DateTime MaturityDate { get; set; }
    public double Tenor { get; set; }
    public DateTime CreatedOn { get; set; }
    public double Yield { get; set; }
    public double Variance { get; set; }
    public double LastAuction { get; set; }
}

