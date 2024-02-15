using System.ComponentModel.DataAnnotations;

public class EditTenure : AddTenure
{
    [Required]
    public string Id { get; set; } = null!;
}