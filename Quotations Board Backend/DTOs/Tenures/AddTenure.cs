using System.ComponentModel.DataAnnotations;

public class AddTenure
{
    [Required]
    public string Name { get; set; } = null!;
    [Required]
    public decimal Tenor { get; set; }
}