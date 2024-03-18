using System.ComponentModel.DataAnnotations;

public class APIYieldCurveRequest
{
    [Required]
    public DateTime Date { get; set; }
}