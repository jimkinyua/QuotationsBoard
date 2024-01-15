public class TBillDTO
{
    public string Id { get; set; } = null!;
    // public string IssueNumber { get; set; } = null!;
    public DateTime IssueDate { get; set; }
    public DateTime MaturityDate { get; set; }
    public decimal Tenor { get; set; }
    public DateTime CreatedOn { get; set; }
    public decimal Yield { get; set; }
    public decimal Variance { get; set; }
}