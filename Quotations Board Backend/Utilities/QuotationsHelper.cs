using Microsoft.EntityFrameworkCore;

public static class QuotationsHelper
{
    public static decimal CalculateRemainingTenor(DateTime maturityDate, DateTime createdAt)
    {
        var remainingTenorInYears = Math.Round((maturityDate.Date - createdAt.Date).TotalDays / 364, 4, MidpointRounding.AwayFromZero);
        return (decimal)Math.Floor(remainingTenorInYears);
    }

    public static double CalculateBondAndAverageQuotedYield(List<Quotation> quotationsForBond)
    {
        var totalBuyVolume = quotationsForBond.Sum(x => x.BuyVolume);
        var weightedBuyingYield = quotationsForBond.Sum(x => x.BuyingYield * x.BuyVolume) / totalBuyVolume;
        var totalSellVolume = quotationsForBond.Sum(x => x.SellVolume);
        var weightedSellingYield = quotationsForBond.Sum(x => x.SellingYield * x.SellVolume) / totalSellVolume;
        var totalQuotes = quotationsForBond.Count;
        double averageWeightedYield = (weightedBuyingYield + weightedSellingYield) / 2;
        return averageWeightedYield;
    }


    public static double CalculateCurrentAverageWeightedYield(double buyYield, double buyVolume, double sellYield, double sellVolume)
    {
        double currentTotalWeightedYield = (buyYield * buyVolume) + (sellYield * sellVolume);
        double currentQuotationVolume = buyVolume + sellVolume;
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

    public static string ValidateYields(double buyingYield, double sellingYield)
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

    // Calculates the Average Weighted Traded Yield for a Bond
    public static double CalculateAverageWeightedTradedYield(List<BondTradeLine> bondTradeLines)
    {
        double averageWeightedTradedYield = 0;
        double totalWeightedBuyYield = bondTradeLines.Where(x => x.Side == "BUY" && x.ExecutedSize >= 50000000).Sum(x => x.Yield * x.ExecutedSize);
        double totalWeightedSellYield = bondTradeLines.Where(x => x.Side == "SELL" && x.ExecutedSize >= 50000000).Sum(x => x.Yield * x.ExecutedSize);
        double totalBuyVolume = bondTradeLines.Where(x => x.Side == "BUY" && x.ExecutedSize >= 50000000).Sum(x => x.ExecutedSize);
        double totalSellVolume = bondTradeLines.Where(x => x.Side == "SELL" && x.ExecutedSize >= 50000000).Sum(x => x.ExecutedSize);

        double averageBuyYield = totalBuyVolume > 0 ? totalWeightedBuyYield / totalBuyVolume : 0;
        double averageSellYield = totalSellVolume > 0 ? totalWeightedSellYield / totalSellVolume : 0;

        /*if (totalBuyVolume > 0 || totalSellVolume > 0)
        {
            averageWeightedTradedYield = (averageBuyYield + averageSellYield) ;
        }*/

        return Math.Round(averageBuyYield, 4, MidpointRounding.AwayFromZero);
    }



    public static double CalculateAverageWeightedQuotedBuyYield(List<Quotation> quotations)
    {
        double totalWeightedBuyYield = quotations.Where(x => x.BuyVolume >= 50000000).Sum(x => x.BuyingYield * x.BuyVolume);
        double totalBuyVolume = quotations.Where(x => x.BuyVolume >= 50000000).Sum(x => x.BuyVolume);
        return totalBuyVolume > 0 ? Math.Round(totalWeightedBuyYield / totalBuyVolume, 4, MidpointRounding.AwayFromZero) : 0;
    }

    public static double CalculateAverageWeightedQuotedSellYield(List<Quotation> quotations)
    {
        double totalWeightedSellYield = quotations.Where(x => x.SellVolume >= 50000000).Sum(x => x.SellingYield * x.SellVolume);
        double totalSellVolume = quotations.Where(x => x.SellVolume >= 50000000).Sum(x => x.SellVolume);
        return totalSellVolume > 0 ? Math.Round(totalWeightedSellYield / totalSellVolume, 4, MidpointRounding.AwayFromZero) : 0;
    }

    public static double CalculateAverageWeightedQuotedYield(List<Quotation> quotations)
    {
        double averageBuyYield = CalculateAverageWeightedQuotedBuyYield(quotations);
        double averageSellYield = CalculateAverageWeightedQuotedSellYield(quotations);
        double averageWeightedQuotedYield = (averageBuyYield + averageSellYield) / 2;
        return Math.Round(averageWeightedQuotedYield, 4, MidpointRounding.AwayFromZero);
    }



    public static bool IsWithinMargin(double possibleImpliedYield, double previousYiedld, double maxAllowwdDiffrence)
    {
        double epsilon = 1e-10;
        double difference =Math.Round( Math.Abs(possibleImpliedYield - previousYiedld),4,MidpointRounding.AwayFromZero);

        if (difference <= maxAllowwdDiffrence)
        {
            return true;
        }

        return false;

    }

    public static decimal DetermineClosestYield(decimal quoted, decimal traded, decimal variance)
    {
        return Math.Abs(quoted - variance) < Math.Abs(traded - variance) ? quoted : traded;
    }

    // fetehces all Quotations for a Bond (Private)
    public static List<Quotation> GetQuotationsForBond(string bondId, DateTime filterDate)
    {
        using (var db = new QuotationsBoardContext())
        {
            var quotations = db.Quotations.Where(q => q.BondId == bondId && q.CreatedAt.Date == filterDate.Date).ToList();
            return quotations;
        }
    }

    public static List<BondTradeLine> GetBondTradeLinesForBond(string bondId, DateTime filterDate)
    {
        using (var db = new QuotationsBoardContext())
        {
            var bondTradeLines = db.BondTradeLines
            .Include(b => b.BondTrade)
            .Where(b => b.BondId == bondId && b.BondTrade.TradeDate == filterDate.Date).ToList();
            return bondTradeLines;
        }
    }
    private static Bond? GetClosestBondL(IEnumerable<Bond> bonds, double lowerBound, double upperBound)
    {
        List<Bond> bondsWithinRange = new List<Bond>();

        foreach (var bond in bonds)
        {
            var m = bond.MaturityDate.Date.Subtract(DateTime.Now.Date).TotalDays / 364;
            var YearsToMaturity = Math.Round(m, 2, MidpointRounding.AwayFromZero);

            // within the range?
            if (YearsToMaturity >= lowerBound && YearsToMaturity <= upperBound)
            {
                bondsWithinRange.Add(bond);
            }
        }

        if (bondsWithinRange.Any())
        {
            // Sort the bonds by maturity score
            bondsWithinRange = bondsWithinRange.OrderBy(b => CalculateMaturityScore(b, upperBound)).ToList();

            // Return the bond with the lowest maturity score
            return bondsWithinRange.First();
        }

        return null;

    }
    private static Bond? GetClosestBond(IEnumerable<Bond> bonds, KeyValuePair<int, (double, double)> benchmark, HashSet<string> usedBondIds, DateTime dateInQuestion)
    {
        // Define the benchmark range
        var lowerBound = benchmark.Value.Item1;
        var upperBound = benchmark.Value.Item2;
        var midpoint = (lowerBound + upperBound) / 2;


        List<(Bond bond, double difference, double OutstandingValue)> bondComparisons = new List<(Bond, double, double)>();
        foreach (var bond in bonds)
        {
            // is bond maturiity within the range?
            var m = bond.MaturityDate.Date.Subtract(DateTime.Now.Date).TotalDays / 364;
            var YearsToMaturity = Math.Round(m, 2, MidpointRounding.AwayFromZero);

            // if not within the range, skip
            if (YearsToMaturity < lowerBound || YearsToMaturity > upperBound)
            {
                continue;
            }

            if (usedBondIds.Contains(bond.Id))
            {
                continue; // Skip bonds that have already been used
            }
            var yearsToMaturity = bond.MaturityDate.Date.Subtract(dateInQuestion.Date).TotalDays / 364;
            yearsToMaturity = Math.Round(yearsToMaturity, 2, MidpointRounding.AwayFromZero);

            var difference = Math.Abs(yearsToMaturity - midpoint); // Difference from midpoint
                                                                   //var maturityScore = CalculateMaturityScore(bond, midpoint); // Calculate maturity score

            bondComparisons.Add((bond, difference, bond.OutstandingValue));
        }
        if (bondComparisons.Any())
        {
            // First, order by difference to find the closest bonds to the midpoint
            // Then, order by OutstandingValue to break ties among those with similar differences
            var orderedBonds = bondComparisons
                .OrderBy(x => x.difference)
                .ThenBy(x => x.OutstandingValue)
                .Select(x => x.bond)
                .ToList();

            return orderedBonds.First(); // Return the bond with the lowest difference and maturity score
        }
        return null;
    }






    private static int SelectBenchmarkForBondBasedOnRTM(double RemainingTimeToMaturityForBond, Dictionary<int, (double, double)> benchmarkRanges)
    {
        int closestBenchmark = -1;
        double minDifference = double.MaxValue;

        foreach (var benchmark in benchmarkRanges)
        {
            if (RemainingTimeToMaturityForBond >= benchmark.Value.Item1 && RemainingTimeToMaturityForBond <= benchmark.Value.Item2)
            {
                double benchmarkTenor = benchmark.Key;
                double difference = Math.Abs(benchmarkTenor - RemainingTimeToMaturityForBond);

                if (difference < minDifference)
                {
                    minDifference = difference;
                    closestBenchmark = benchmark.Key;
                }
            }
        }

        return closestBenchmark;
    }



    private static double CalculateMaturityScore(Bond bond, double upperBound)
    {
        // Extract the year and month from the bond's maturity date
        int maturityYear = bond.MaturityDate.Year;
        int maturityMonth = bond.MaturityDate.Month;

        // Calculate the year including the month as a fraction
        double maturityFractionalYear = maturityYear + (maturityMonth / 12.0);

        // Calculate the difference between the maturity fractional year and the upper bound
        double maturityDifference = maturityFractionalYear - upperBound;

        // Return the absolute value of the maturity difference
        double maturityScore = Math.Abs(maturityDifference);

        return maturityScore;
    }

    public static double GetTotalVolume(IEnumerable<BondTradeLine> bondTradeLines)
    {
        return bondTradeLines.Sum(x => x.ExecutedSize);
    }

    public static double GetTotalVolumeBySide(IEnumerable<BondTradeLine> bondTradeLines, string side)
    {
        return bondTradeLines.Where(x => x.Side == side).Sum(x => x.ExecutedSize);
    }

    public static double GetTotalWeightedYield(IEnumerable<BondTradeLine> bondTradeLines, string side)
    {
        return bondTradeLines.Where(x => x.Side == side).Sum(x => x.Yield * x.ExecutedSize);
    }

    public static double CalculateAverage(double total, double count)
    {
        return count > 0 ? total / count : 0;
    }




}