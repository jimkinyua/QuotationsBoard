using System.ComponentModel.DataAnnotations;

public class ConfirmImpliedYieldDTO
{
    [Required]
    public DateTime YieldDate { get; set; } = DateTime.Now;
    // public List<ComputedImpliedYield> ImpliedYields { get; set; } = null!;
}