using System.ComponentModel.DataAnnotations;

public class UploadTradedBondValue
{
    [Required]
    public string TradeDate { get; set; } = null!;
    [Required]
    [DataType(DataType.Upload)]
    public IFormFile ExcelFile { get; set; } = null!;
}