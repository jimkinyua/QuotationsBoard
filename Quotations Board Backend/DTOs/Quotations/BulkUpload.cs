using System.ComponentModel.DataAnnotations;

public class BulkUpload
{
    [Required]
    [DataType(DataType.Upload)]
    public IFormFile ExcelFile { get; set; } = null!;
}
