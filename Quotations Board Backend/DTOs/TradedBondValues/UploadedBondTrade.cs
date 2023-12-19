public class UploadedBondTrade
{
    public string Id { get; set; } = null!;
    public DateTime UploadedAt { get; set; }
    public string UploadedBy { get; set; } = null!;
    public List<UploadedBondTradeLineDTO> UploadedBondTradeLineDTO { get; set; } = null!;
}