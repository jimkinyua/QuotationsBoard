using System.ComponentModel.DataAnnotations;

public class APIYieldCurveRequest
{
    [Required]
    [DataType(DataType.Date)]
    [DisplayFormat(DataFormatString = "{0:yyyy-MM-dd}", ApplyFormatInEditMode = true)]

    public DateOnly Date { get; set; }
}