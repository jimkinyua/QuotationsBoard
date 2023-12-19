using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using DocumentFormat.OpenXml.Wordprocessing;

public class BondTrade
{
    [Key]
    [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
    public string Id { get; set; } = null!;
    public string BondId { get; set; } = null!;
    public string UploadedBy { get; set; } = null!;
    public DateTime UploadedOn { get; set; }
    public DateTime TradeDate { get; set; }
    public virtual Bond Bond { get; set; } = null!;
    public virtual ICollection<BondTradeLine> BondTradeLines { get; set; } = null!;
}