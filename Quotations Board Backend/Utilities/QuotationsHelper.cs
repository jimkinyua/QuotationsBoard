using Microsoft.EntityFrameworkCore;

public static class QuotationsHelper
{
    public static decimal CalculateRemainingTenor(DateTime maturityDate, DateTime createdAt)
    {
        var remainingTenorInYears = Math.Round((maturityDate - createdAt).TotalDays / 364, 4, MidpointRounding.AwayFromZero);
        return (decimal)Math.Floor(remainingTenorInYears);
    }

    public static decimal CalculateCurrentAverageWeightedYield(decimal buyYield, decimal buyVolume, decimal sellYield, decimal sellVolume)
    {
        decimal currentTotalWeightedYield = (buyYield * buyVolume) + (sellYield * sellVolume);
        decimal currentQuotationVolume = buyVolume + sellVolume;
        return currentTotalWeightedYield / currentQuotationVolume;
    }
    public static async Task<bool> IsValidationEnabledForTenureAsync(decimal tenor)
    {
        using (var context = new QuotationsBoardContext())
        {
            var tenure = await context.Tenures.FirstOrDefaultAsync(x => x.Tenor == tenor);
            if (tenure != null)
            {
                return tenure.IsValidationEnabled;
            }
            return false;
        }
    }

    public static async Task<string> ValidateBond(string bondId)
    {
        using (var context = new QuotationsBoardContext())
        {
            var bond = await context.Bonds.FirstOrDefaultAsync(b => b.Id == bondId);
            if (bond == null)
            {
                return "Invalid bond";
            }

            if (bond.MaturityDate < DateTime.Now)
            {
                return "Bond has matured";
            }

            // Uncomment if bond category validation is required
            // if (bond.BondCategory != BondCategories.FXD)
            // {
            //     return "Only FXD bonds are allowed to be quoted";
            // }

            return null; // No errors
        }

    }

    public static bool IsValidQuotationTime(DateTime createdAt)
    {
        // Uncomment and adjust logic if time-based validation is required
        // return createdAt.Hour < 9;

        return true;
    }

    public static string ValidateYields(decimal buyingYield, decimal sellingYield)
    {
        if (sellingYield > buyingYield)
        {
            return "Selling yield cannot be greater than buying yield";
        }

        if (sellingYield > 100)
        {
            return "Selling yield cannot be greater than 100";
        }

        if (buyingYield > 100)
        {
            return "Buying yield cannot be greater than 100";
        }

        var difference = Math.Abs(buyingYield - sellingYield);
        if (difference > 1)
        {
            return $"The difference between selling and buying yields cannot be greater than 1%. The current difference is {difference}%";
        }

        return null; // No errors
    }





}