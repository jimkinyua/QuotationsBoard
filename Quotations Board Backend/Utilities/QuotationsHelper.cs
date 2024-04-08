using System.Globalization;
using Microsoft.EntityFrameworkCore;

public static class QuotationsHelper
{

    public static async Task<List<Quotation>> GetQuotationsForDate(DateTime date)
    {
        using (var context = new QuotationsBoardContext())
        {
            var quotations = await context.Quotations.Where(q => q.CreatedAt.Date == date.Date).ToListAsync();
            return quotations;
        }
    }

    // get implied yields for a date
    public static async Task<List<ImpliedYield>> GetImpliedYieldsForDate(DateTime date)
    {
        using (var context = new QuotationsBoardContext())
        {
            var impliedYields = await context.ImpliedYields.Where(q => q.YieldDate.Date == date.Date).ToListAsync();
            return impliedYields;
        }
    }

    public static List<BondAndYield> LoadBondCurrentValues(List<Quotation> quotations, List<ImpliedYield> impliedYields, List<Bond> bonds, DateTime dateInQuestion)
    {
        List<BondAndYield> bondAndYields = new List<BondAndYield>();
        foreach (var bond in bonds)
        {
            // Does bond have implied yield?
            var impliedYield = impliedYields.FirstOrDefault(i => i.BondId == bond.Id);
            // Does Bond have quotations?
            var bondQuotations = quotations.Where(q => q.BondId == bond.Id).ToList();

            // quotes give priority if no quote use the implied yield else calculate the average quoted yield of quotes
            if (bondQuotations.Any())
            {
                var averageQuotedYield = CalculateAverageWeightedQuotedYield(bondQuotations);
                bondAndYields.Add(new BondAndYield
                {
                    BondId = bond.Id,
                    BondTenor = CalculateRemainingTenor(bond.MaturityDate, dateInQuestion),
                    YieldToUse = averageQuotedYield
                });
            }
            else if (impliedYield != null)
            {
                bondAndYields.Add(new BondAndYield
                {
                    BondId = bond.Id,
                    BondTenor = CalculateRemainingTenor(bond.MaturityDate, dateInQuestion),
                    YieldToUse = impliedYield.Yield
                });
            }
            else
            {
                continue; // if no quote or implied yield, skip this useleless bond
            }

        }
        return bondAndYields;
    }

    public static async Task<List<FinalYieldCurveData>> InterpolateValuesForLastQuotedDayAsync(DateTime LastDateWithQuotes, List<Quotation> Quotes)
    {
        List<BondAndAverageQuotedYield> bondAndAverageQuotedYields = new List<BondAndAverageQuotedYield>();
        Dictionary<int, (double, double)> benchmarkRanges = YieldCurveHelper.GetBenchmarkRanges(LastDateWithQuotes);
        HashSet<double> tenuresThatRequireInterPolation = new HashSet<double>();
        HashSet<double> tenuresThatDoNotRequireInterpolation = new HashSet<double>();
        HashSet<string> usedBondIds = new HashSet<string>();
        List<YieldCurveDataSet> yieldCurveCalculations = new List<YieldCurveDataSet>();
        List<FinalYieldCurveData> yieldCurves = new List<FinalYieldCurveData>();
        List<FinalYieldCurveData> previousYieldCurveData = new List<FinalYieldCurveData>();

        using (var context = new QuotationsBoardContext())
        {
            var bondsNotMatured = context.Bonds.Where(b => b.BondCategory == "FXD" && b.MaturityDate.Date > LastDateWithQuotes.Date).ToList();
            var groupedQuotations = Quotes.GroupBy(x => x.BondId);
            foreach (var bondQuotes in groupedQuotations)
            {
                var bondDetails = await context.Bonds.FirstOrDefaultAsync(b => b.Id == bondQuotes.Key);
                if (bondDetails == null)
                {
                    continue;
                }
                var RemainingTenor = (bondDetails.MaturityDate - LastDateWithQuotes.Date).TotalDays / 364;

                var quotationsForBond = bondQuotes.ToList();
                double averageWeightedYield = QuotationsHelper.CalculateBondAndAverageQuotedYield(quotationsForBond);

                BondAndAverageQuotedYield bondAndAverageQuotedYield = new BondAndAverageQuotedYield
                {
                    BondId = bondQuotes.Key,
                    AverageQuotedYield = averageWeightedYield,
                    BondTenor = RemainingTenor,
                };
                bondAndAverageQuotedYields.Add(bondAndAverageQuotedYield);

            }

            foreach (var benchmarkRange in benchmarkRanges)
            {
                Bond? BondWithExactTenure = null;

                var bondsWithinThisTenure = YieldCurveHelper.GetBondsInTenorRange(bondsNotMatured, benchmarkRange, usedBondIds, LastDateWithQuotes);
                if (bondsWithinThisTenure.Count() == 0 && benchmarkRange.Key != 1)
                {
                    tenuresThatRequireInterPolation.Add(benchmarkRange.Key);
                    continue;
                }
                else
                {
                    BondWithExactTenure = YieldCurveHelper.GetBondWithExactTenure(bondsWithinThisTenure, benchmarkRange.Value.Item1, LastDateWithQuotes);
                }

                if (BondWithExactTenure != null)
                {
                    // was this bond quoted? some may have excat tenure but not quoted
                    var bondAndAverageQuotedYield = bondAndAverageQuotedYields.FirstOrDefault(b => b.BondId == BondWithExactTenure.Id);
                    if (bondAndAverageQuotedYield != null)
                    {
                        var BondTenure = Math.Round((BondWithExactTenure.MaturityDate.Date - LastDateWithQuotes.Date).TotalDays / 364, 4, MidpointRounding.AwayFromZero);

                        YieldCurveDataSet yieldCurve = new YieldCurveDataSet
                        {
                            Tenure = BondTenure,
                            Yield = bondAndAverageQuotedYield.AverageQuotedYield,
                            IssueDate = BondWithExactTenure.IssueDate,
                            MaturityDate = BondWithExactTenure.MaturityDate,
                            BondUsed = BondWithExactTenure.Isin
                        };
                        yieldCurveCalculations.Add(yieldCurve);
                        usedBondIds.Add(BondWithExactTenure.Id);
                        tenuresThatDoNotRequireInterpolation.Add(BondTenure);
                    }
                    else
                    {
                        // we need to interpolate
                        tenuresThatRequireInterPolation.Add(benchmarkRange.Key);
                    }
                }
                else
                {
                    tenuresThatRequireInterPolation.Add(benchmarkRange.Key);

                    foreach (var bond in bondsWithinThisTenure)
                    {
                        if (usedBondIds.Contains(bond.Id))
                        {
                            continue; // Skip bonds that have already been used
                        }

                        var bondAndAverageQuotedYield = bondAndAverageQuotedYields.FirstOrDefault(b => b.BondId == bond.Id);
                        if (bondAndAverageQuotedYield != null)
                        {
                            var BondTenure = Math.Round((bond.MaturityDate.Date - LastDateWithQuotes.Date).TotalDays / 364, 4, MidpointRounding.AwayFromZero);

                            YieldCurveDataSet yieldCurve = new YieldCurveDataSet
                            {
                                Tenure = BondTenure,
                                Yield = bondAndAverageQuotedYield.AverageQuotedYield,
                                IssueDate = bond.IssueDate,
                                MaturityDate = bond.MaturityDate,
                                BondUsed = bond.Isin
                            };
                            yieldCurveCalculations.Add(yieldCurve);
                            usedBondIds.Add(bond.Id);
                        }
                    }
                }


            }

            // interpolate the yield curve
            var interpolatedYieldCurve = YieldCurveHelper.InterpolateWhereNecessary(yieldCurveCalculations, tenuresThatRequireInterPolation);
            HashSet<double> tenuresToPlot = new HashSet<double>();
            foreach (var interpolatedTenure in tenuresThatRequireInterPolation)
            {
                tenuresToPlot.Add(interpolatedTenure);
            }
            foreach (var notInterpolated in tenuresThatDoNotRequireInterpolation)
            {
                tenuresToPlot.Add(notInterpolated);
            }

            foreach (var tenureToPlot in tenuresToPlot)
            {
                foreach (var yieldCurveCalculation in yieldCurveCalculations)
                {
                    var _BondUsed = "Interpolated";
                    if (tenuresThatDoNotRequireInterpolation.Contains(yieldCurveCalculation.Tenure))
                    {
                        _BondUsed = yieldCurveCalculation.BondUsed;
                    }

                    if (yieldCurveCalculation.Tenure == tenureToPlot)
                    {
                        yieldCurves.Add(new FinalYieldCurveData
                        {
                            Tenure = tenureToPlot,
                            Yield = yieldCurveCalculation.Yield,
                            // CanBeUsedForYieldCurve = true,
                            BondUsed = _BondUsed,
                            BenchMarkTenor = tenureToPlot,
                        });
                    }
                }
            }

            return yieldCurves;
        }


    }

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
            var tenure = await context.Tenures.FirstOrDefaultAsync(x => x.Tenor == tenor && x.IsDeleted == false);
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
        double difference = Math.Round(Math.Abs(possibleImpliedYield - previousYiedld), 4, MidpointRounding.AwayFromZero);

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

    internal static async Task<DateTime> GetMostRecentDateWithQuotationsBeforeDateInQuestion(DateTime fromDate)
    {
        using (var context = new QuotationsBoardContext())
        {
            var date = await context.Quotations
                .Where(q => q.CreatedAt.Date < fromDate.Date)
                .OrderByDescending(q => q.CreatedAt)
                .Select(q => q.CreatedAt)
                .FirstOrDefaultAsync();

            return date;
        }
    }

    // Gets most recent day with Implie Yields
    internal static async Task<DateTime> GetMostRecentDateWithImpliedYieldsBeforeDateInQuestion(DateTime fromDate)
    {
        using (var context = new QuotationsBoardContext())
        {
            var date = await context.ImpliedYields
                .Where(q => q.YieldDate.Date < fromDate.Date)
                .OrderByDescending(q => q.YieldDate)
                .Select(q => q.YieldDate)
                .FirstOrDefaultAsync();
            return date;
        }
    }

    public static DateTime ParseDate(string dateInput)
    {
        if (string.IsNullOrWhiteSpace(dateInput) || dateInput == "default")
        {
            return DateTime.Now;
        }
        else
        {
            string[] formats = { "dd/MM/yyyy", "yyyy-MM-dd", "MM/dd/yyyy", "dd-MM-yyyy", "dd/MM/yyyy HH:mm:ss", "yyyy-MM-dd HH:mm:ss", "MM/dd/yyyy HH:mm:ss", "dd-MM-yyyy HH:mm:ss" };
            DateTime targetTradeDate;
            bool success = DateTime.TryParseExact(dateInput, formats, CultureInfo.InvariantCulture, DateTimeStyles.None, out targetTradeDate);
            if (!success)
            {
                // return default date
                return DateTime.MinValue;
            }
            return targetTradeDate.Date;
        }
    }

}