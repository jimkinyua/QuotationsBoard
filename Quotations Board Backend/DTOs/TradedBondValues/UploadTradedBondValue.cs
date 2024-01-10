using System.ComponentModel.DataAnnotations;

public class UploadTradedBondValue
{
    [Required]
    [DataType(DataType.Upload)]
    public IFormFile ExcelFile { get; set; } = null!;
}