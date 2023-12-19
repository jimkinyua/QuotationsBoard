public class UploadedTradeBond
{
    public string Id { get; set; } = null!;
    public string UploadedBy { get; set; } = null!;
    public DateTime UploadedOn { get; set; }
    public DateTime TradeDate { get; set; }
}