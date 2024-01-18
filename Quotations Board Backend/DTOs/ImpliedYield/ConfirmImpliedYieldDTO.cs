using System.ComponentModel.DataAnnotations;

public class ConfirmImpliedYieldDTO
{
    [Required]
    public List<ComputedImpliedYield> ImpliedYields { get; set; } = null!;
}