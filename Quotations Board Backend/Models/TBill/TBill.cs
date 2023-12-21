using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

public class TBill
{
    [Key]
    [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
    public string Id { get; set; } = null!;
    public string IssueNumber { get; set; } = null!;
    public DateTime IssueDate { get; set; }
    public DateTime MaturityDate { get; set; }
    public decimal Tenor { get; set; }
    public string CreatedBy { get; set; } = null!;
    public DateTime CreatedOn { get; set; }
    public virtual ICollection<TBillYield> TBillYields { get; set; } = null!;
}