using System.ComponentModel.DataAnnotations;

public class NewBondDTO
{
    [Required]
    public string Isin { get; set; } = null!;
    [Required]
    public DateTime IssueDate { get; set; }
    [Required]
    public DateTime MaturityDate { get; set; }
    [Required]
    public double OutstandingValue { get; set; }
    [Required]
    public string CouponType { get; set; } = null!;
    [Required]
    public double CouponRate { get; set; }
    [Required]
    public string BondType { get; set; } = null!;
    [Required]
    public string IssueNumber { get; set; } = null!;
    public string BondCategory { get; set; } = "";
    public Boolean IsBenchMarkBond { get; set; } = false;
    [RequiredIfCouponTypeVariable]
    public string? VariableCouponRate { get; set; }

    public class RequiredIfCouponTypeVariableAttribute : ValidationAttribute
    {
        protected override ValidationResult? IsValid(object? value, ValidationContext validationContext)
        {
            var newBondDTO = (NewBondDTO)validationContext.ObjectInstance;
            if (newBondDTO.CouponType == "Variable" && string.IsNullOrWhiteSpace(newBondDTO.VariableCouponRate))
            {
                return new ValidationResult("VariableCouponRate is required when CouponType is Variable.");
            }
            return ValidationResult.Success;
        }
    }

}

