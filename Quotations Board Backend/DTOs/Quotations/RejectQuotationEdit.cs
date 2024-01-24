using System.ComponentModel.DataAnnotations;

public class RejectQuotationEdit
{
    [Required]
    public string Id { get; set; } = null!;
    [Required]
    public string RejectionReason { get; set; } = null!;
}