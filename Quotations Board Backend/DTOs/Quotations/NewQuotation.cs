using System.ComponentModel.DataAnnotations;

public class NewQuotation
{
    [Required]
    public string BondId { get; set; } = null!;
    [Required]
    [Range(0, 100)]
    [ExactDecimalPlaces(4)]
    public double BuyYield { get; set; }
    [Required]
    [Range(0, 100)]
    [ExactDecimalPlaces(4)]
    public double SellYield { get; set; }
    [Required]
    public double BuyVolume { get; set; } = 50000;
    [Required]
    public double SellVolume { get; set; } = 50000;

}

public class ExactDecimalPlacesAttribute : ValidationAttribute
{
    private readonly int _decimalPlaces;

    public ExactDecimalPlacesAttribute(int decimalPlaces)
    {
        _decimalPlaces = decimalPlaces;
    }

    protected override ValidationResult IsValid(object value, ValidationContext validationContext)
    {
        if (value != null)
        {
            decimal number;
            if (Decimal.TryParse(value.ToString(), out number))
            {
                if (Decimal.Round(number, _decimalPlaces) != number)
                {
                    return new ValidationResult($"The field {validationContext.DisplayName} must have exactly {_decimalPlaces} decimal places.");
                }
            }
            else
            {
                return new ValidationResult($"The field {validationContext.DisplayName} is not a valid decimal.");
            }
        }

        return ValidationResult.Success;
    }
}