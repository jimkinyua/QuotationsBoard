using System.ComponentModel.DataAnnotations;

public class DraftImpliedYieldDTO
{
    [Required]
    public DateTime YieldDate { get; set; } = DateTime.Now;
    [Required]
    public List<ComputedImpliedYield> ImpliedYields { get; set; } = null!;
}