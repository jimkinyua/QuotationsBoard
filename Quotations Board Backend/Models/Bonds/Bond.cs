using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

public class Bond
{
    [Key]
    [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
    public string Id { get; set; } = null!;
    public string Isin { get; set; } = null!;
    public DateTime IssueDate { get; set; }
    public DateTime MaturityDate { get; set; }
    public double OutstandingValue { get; set; }
    public string CouponType { get; set; } = null!;
    public double CouponRate { get; set; }
    public string BondType { get; set; } = null!;
    public string IssueNumber { get; set; } = null!;
    public string BondCategory { get; set; } = "IFB";
    public Boolean IsBenchMarkBond { get; set; } = false;
    public virtual ICollection<Quotation> Quotations { get; set; } = null!;
    public virtual ICollection<ImpliedYield> ImpliedYields { get; set; } = null!;
}