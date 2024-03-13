using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

public static class YieldCurveHelper
{
    public static List<YieldCurveDataSet> InterpolateWhereNecessary(List<YieldCurveDataSet> yieldCurveDataList, HashSet<double> tenuresThatRequireInterPolation, List<FinalYieldCurveData> PreviousYieldCurve = null)
    {
        yieldCurveDataList.Sort((x, y) => x.Tenure.CompareTo(y.Tenure));
        foreach (var tenureToInterpolate in tenuresThatRequireInterPolation)
        {
            // Find the closest tenures before and after the tenure we want to interpolate
            var previousData = FindPreviousDataWithYield(yieldCurveDataList, tenureToInterpolate, PreviousYieldCurve);
            var nextData = FindNextDataWithYield(yieldCurveDataList, tenureToInterpolate, PreviousYieldCurve);
            if (previousData != null && nextData != null)
            {
                // Perform the interpolation
                double interpolatedYield = PerformLinearInterpolation(previousData, nextData, tenureToInterpolate);

                // Create a new YieldCurve object for the interpolated yield
                var interpolatedYieldCurve = new YieldCurveDataSet
                {
                    Yield = interpolatedYield,
                    BondUsed = "Interpolated",
                    Tenure = tenureToInterpolate
                };

                // Find the correct index to insert the new interpolatedYieldCurve
                int insertIndex = yieldCurveDataList.FindIndex(y => y.Tenure > tenureToInterpolate);
                if (insertIndex < 0) insertIndex = yieldCurveDataList.Count; // If not found, add to the end

                // Insert the interpolatedYieldCurve into the yieldCurveDataList
                yieldCurveDataList.Insert(insertIndex, interpolatedYieldCurve);
            }
        }

        return yieldCurveDataList;
    }

    public static YieldCurveDataSet? GetCurrentTBillYield(DateTime fromDate)
    {
        var (startofCycle, endOfCycle) = TBillHelper.GetCurrentTBillCycle(fromDate);
        using (var context = new QuotationsBoardContext())
        {
            var currentOneYearTBill = context.TBills
                                .Where(t => t.IssueDate.Date >= startofCycle.Date && t.IssueDate.Date <= endOfCycle.Date && t.Tenor == 364)
                                .OrderByDescending(t => t.IssueDate)
                                .FirstOrDefault();
            if (currentOneYearTBill != null)
            {
                YieldCurveDataSet tBillYieldCurve = new YieldCurveDataSet
                {
                    Tenure = 1,
                    Yield = currentOneYearTBill.Yield,
                    IssueDate = currentOneYearTBill.IssueDate,
                    MaturityDate = currentOneYearTBill.MaturityDate,
                    BondUsed = "1 Year TBill"
                };
                return tBillYieldCurve;
            }

            return null;
        }
    }



    public static IEnumerable<Bond> GetBondsInTenorRange(IEnumerable<Bond> bonds, KeyValuePair<int, (double, double)> benchmark, HashSet<string> usedBondIds, DateTime dateInQuestion)
    {
        // Define the benchmark range
        double lowerBound = benchmark.Value.Item1;
        double upperBound = benchmark.Value.Item2;

        // Filter bonds within the tenor range
        return bonds.Where(bond => IsBondInTenorRange(bond, dateInQuestion, lowerBound, upperBound)).ToList();
    }

    // This separate method calculates if a bond is within the tenor range
    private static bool IsBondInTenorRange(Bond bond, DateTime dateInQuestion, double lowerBound, double upperBound)
    {
        double yearsToMaturity = CalculateYearsToMaturity(bond.MaturityDate, dateInQuestion);
        // Use Math.Floor to truncate the decimal part of yearsToMaturity
        double truncatedYearsToMaturity = Math.Floor(yearsToMaturity);
        if (truncatedYearsToMaturity >= lowerBound && truncatedYearsToMaturity <= upperBound)
        {
            return true;
        }
        return false;
    }

    // This method calculates the years to maturity for a bond
    private static double CalculateYearsToMaturity(DateTime maturityDate, DateTime currentDate)
    {
        return (maturityDate.Date - currentDate.Date).TotalDays / 364;
    }


    public static Bond? GetBondWithExactTenure(IEnumerable<Bond> bondsWithinRange, double tenure, DateTime dateInQuestion)
    {
        var bonds = bondsWithinRange
         .Where(bond =>
         {
             var yearsToMaturity = Math.Round((bond.MaturityDate.Date - dateInQuestion.Date).TotalDays / 364, 4, MidpointRounding.AwayFromZero);
             return yearsToMaturity == tenure;
         })
         .ToList();

        // break a tie if may are there
        if (bonds.Any())
        {
            // Then, order by OutstandingValue to break ties among those with similar differences
            bonds.OrderBy(x => x.OutstandingValue).ToList();
            return bonds.First();


        }
        return null;
    }
    private static bool IsYieldMissing(YieldCurveDataSet data)
    {
        return data.Yield == 0 || string.IsNullOrEmpty(data.BondUsed);
    }

    // receives a list of bonds and tenure then filters out that are within the tenure
    public static List<Bond> GetBondsWithinTenure(List<Bond> bonds, double tenure, DateTime DateInQuestion)
    {
        List<Bond> bondsWithinTenure = new List<Bond>();
        foreach (var bond in bonds)
        {
            var YearsToMaturity = (bond.MaturityDate - DateInQuestion).TotalDays / 364;
            if (YearsToMaturity > tenure || YearsToMaturity < tenure)
            {
                bondsWithinTenure.Add(bond);
            }
        }
        return bondsWithinTenure;
    }




    private static YieldCurveDataSet FindPreviousDataWithYield(List<YieldCurveDataSet> dataList, int currentIndex)
    {
        for (int i = currentIndex - 1; i >= 0; i--)
        {
            if (!IsYieldMissing(dataList[i]))
            {
                return dataList[i];
            }
        }
        return null; // No previous data found
    }
    public static YieldCurveDataSet FindPreviousDataWithYield(List<YieldCurveDataSet> yieldCurveDataList, double tenureToInterpolate, List<FinalYieldCurveData> PreviousYieldCurve)
    {
        YieldCurveDataSet previousData = null;

        // Loop backwards through the sorted list to find the previous data point with a yield
        for (int i = yieldCurveDataList.Count - 1; i >= 0; i--)
        {
            // Check if the current data point has a yield and a tenure less than the tenure to interpolate
            if (!IsYieldMissing(yieldCurveDataList[i]) && yieldCurveDataList[i].Tenure < tenureToInterpolate)
            {
                previousData = yieldCurveDataList[i];
                break; // Exit the loop once the previous data point is found
            }
        }

        // If no previous data is found, previousData remains null
        //We will not get it from the previous yield curve
        if (previousData == null && PreviousYieldCurve != null && tenureToInterpolate != 1)
        {
            var _previousYieldCurve = PreviousYieldCurve.OrderByDescending(y => y.Tenure).ToList();
            foreach (var yieldCurve in _previousYieldCurve)
            {
                if (yieldCurve.Tenure < tenureToInterpolate)
                {
                    previousData = new YieldCurveDataSet
                    {
                        Tenure = yieldCurve.Tenure,
                        Yield = yieldCurve.Yield,
                        BondUsed = yieldCurve.BondUsed
                    };
                    break;
                }
            }
        }
        // is it still null? Give up
        if (previousData == null)
        {
            return null;
        }
        return previousData;
    }

    public static YieldCurveDataSet FindNextDataWithYield(List<YieldCurveDataSet> yieldCurveDataList, double tenureToInterpolate, List<FinalYieldCurveData> PreviousYieldCurve)
    {
        YieldCurveDataSet nextData = null;

        // Loop through the sorted list to find the next data point with a yield
        foreach (var yieldCurve in yieldCurveDataList)
        {
            // Check if the current data point has a yield and a tenure greater than the tenure to interpolate
            if (!IsYieldMissing(yieldCurve) && yieldCurve.Tenure > tenureToInterpolate)
            {
                nextData = yieldCurve;
                break; // Exit the loop once the next data point is found
            }
        }

        // If no next data is found, nextData remains null
        // go back to the previous yield curve
        if (nextData == null)
        {
            var _previousYieldCurve = PreviousYieldCurve.OrderBy(y => y.Tenure).ToList();
            foreach (var yieldCurve in _previousYieldCurve)
            {
                if (yieldCurve.Tenure > tenureToInterpolate)
                {
                    nextData = new YieldCurveDataSet
                    {
                        Tenure = yieldCurve.Tenure,
                        Yield = yieldCurve.Yield,
                        BondUsed = yieldCurve.BondUsed
                    };
                    break;
                }
            }
        }
        if (nextData == null)
        {
            return null;
        }
        return nextData;
    }


    private static YieldCurveDataSet FindNextDataWithYield(List<YieldCurveDataSet> dataList, int currentIndex, List<FinalYieldCurveData> PreviousYieldCurve)
    {
        for (int i = currentIndex + 1; i < dataList.Count; i++)
        {
            if (!IsYieldMissing(dataList[i]))
            {
                return dataList[i];
            }
        }
        return null; // No next data found
    }


    /// <summary>
    /// Performs linear interpolation to estimate the yield at a target tenor.
    /// </summary>
    /// <param name="previousData">The data point before the target tenor.</param>
    /// <param name="nextData">The data point after the target tenor.</param>
    /// <param name="targetTenor">The target tenor for which the yield is to be interpolated.</param>
    /// <returns>The interpolated yield at the target tenor.</returns>
    private static double PerformLinearInterpolation(YieldCurveDataSet previousData, YieldCurveDataSet nextData, double targetTenor)
    {
        // tenorDifference represents (x2 - x1), the difference in tenor between the next and previous data points.
        var tenorDifference = nextData.Tenure - previousData.Tenure;

        // yieldDifference represents (y2 - y1), the difference in yield between the next and previous data points.
        double yieldDifference = nextData.Yield - previousData.Yield;

        // tenorRatio represents ((x - x1) / (x2 - x1)), the proportion of the target tenor between the previous and next tenors.
        double tenorRatio = (targetTenor - previousData.Tenure) / tenorDifference;

        // Applying the linear interpolation formula: y = y1 + ((x - x1) * (y2 - y1) / (x2 - x1))
        // where y is the interpolated yield, x is the target tenor, x1 and y1 are the tenor and yield of the previous data point,
        // and x2 and y2 are the tenor and yield of the next data point.
        var interpolatedYield = previousData.Yield + (tenorRatio * yieldDifference);
        return Math.Round(interpolatedYield, 4, MidpointRounding.AwayFromZero);
    }



    public static Dictionary<int, (double, double)> GenerateBenchmarkRanges(double maxTenure)
    {
        Dictionary<int, (double, double)> benchmarkRanges = new Dictionary<int, (double, double)>();
        int startYear = 1; // Assuming you want to start from 2 years

        for (int year = startYear; year <= Math.Ceiling(maxTenure); year++)
        {
            double rangeStart = year;
            double rangeEnd;

            // Check if this is the last range
            if (year == Math.Ceiling(maxTenure))
            {
                // For the last range, extend rangeEnd to the maximum tenure
                rangeEnd = maxTenure + 0.9;
            }
            else
            {
                // For other ranges, end at .9 of the current year
                rangeEnd = year + 0.9;
            }
            benchmarkRanges.Add(year, (rangeStart, rangeEnd));
        }

        return benchmarkRanges;
    }

    public static Dictionary<int, (double, double)> GetBenchmarkRanges(DateTime DateInQuestion)
    {
        using (var context = new QuotationsBoardContext())
        {
            var _unMaturedBonds = context.Bonds.Where(b => b.MaturityDate > DateTime.Now).ToList();
            var fXdBonds = _unMaturedBonds.Where(b => b.BondCategory == "FXD").ToList();

            var bondDates = fXdBonds
                           .Select(b => new { b.MaturityDate, b.IssueDate })
                           .ToList();

            var maxTenure = bondDates.Max(b => (b.MaturityDate.Date - DateInQuestion.Date).TotalDays / 364);
            // var _roundedMaxTenure = Math.Floor(maxTenure);
            var _floorMaxTenure = Math.Floor(maxTenure);
            var _ceilMaxTenure = Math.Ceiling(maxTenure);

            return GenerateBenchmarkRanges(_floorMaxTenure);
        }
    }

    public static List<Bond> GetFXDBonds(DateTime parsedDate)
    {
        using (var _db = new QuotationsBoardContext())
        {
            var fXdBonds = _db.Bonds.Where(b => b.BondCategory == "FXD" && b.MaturityDate.Date > parsedDate.Date).ToList();
            return fXdBonds;
        }
    }

    public static TBillImpliedYield? GetCurrentOneYearTBill(DateTime parsedDate)
    {
        using (var _db = new QuotationsBoardContext())
        {
            return _db.TBillImpliedYields
                      .Include(t => t.TBill)
                      .Where(t => t.Tenor == 364 && t.Date.Date == parsedDate.Date).FirstOrDefault();
        }
    }

    public static (double, double) CalculateTenureExtremes(IEnumerable<Bond> bonds, DateTime parsedDate)
    {
        var bondDates = bonds
            .Select(b => new { b.MaturityDate, b.IssueDate })
            .ToList();

        var maxTenure = bondDates.Max(b => (b.MaturityDate.Date - parsedDate.Date).TotalDays / 364);
        var _floorMaxTenure = Math.Floor(maxTenure);
        var _ceilMaxTenure = Math.Ceiling(maxTenure);

        return (_floorMaxTenure, _ceilMaxTenure);
    }


    public static ProcessBenchmarkResult ProcessBenchmarkRanges(Dictionary<int, (double, double)> benchmarkRanges, HashSet<double> tenuresThatRequireInterPolation, HashSet<double> tenuresThatDoNotRequireInterpolation, HashSet<string> usedBondIds, DateTime parsedDate, QuotationsBoardContext _db, List<Bond> fXdBonds)
    {
        ProcessBenchmarkResult result = new ProcessBenchmarkResult();
        foreach (var benchmark in benchmarkRanges)
        {
            Bond? BondWithExactTenure = null;
            var bondsWithinThisTenure = YieldCurveHelper.GetBondsInTenorRange(fXdBonds, benchmark, usedBondIds, parsedDate);

            if (bondsWithinThisTenure.Count() == 0 && benchmark.Key != 1)
            {
                tenuresThatRequireInterPolation.Add(benchmark.Key);
                continue;
            }
            else
            {
                BondWithExactTenure = YieldCurveHelper.GetBondWithExactTenure(bondsWithinThisTenure, benchmark.Value.Item1, parsedDate);
            }

            if (BondWithExactTenure != null)
            {
                // get implied yield of this Bond
                var impliedYield = _db.ImpliedYields.Where(i => i.BondId == BondWithExactTenure.Id && i.YieldDate.Date == parsedDate.Date).FirstOrDefault();
                if (impliedYield == null)
                {
                    result.Success = false;
                    result.ErrorMessage = $"The Bond {BondWithExactTenure.IssueNumber} seems not to have an Implied Yield for the date {parsedDate}";
                    return result;
                }
                var BondTenure = Math.Round((BondWithExactTenure.MaturityDate.Date - parsedDate.Date).TotalDays / 364, 4, MidpointRounding.AwayFromZero);
                result.YieldCurveCalculations.Add(new YieldCurveDataSet
                {
                    Yield = (double)Math.Round(impliedYield.Yield, 4, MidpointRounding.AwayFromZero),
                    BondUsed = BondWithExactTenure.IssueNumber,
                    IssueDate = BondWithExactTenure.IssueDate,
                    MaturityDate = BondWithExactTenure.MaturityDate,
                    Tenure = BondTenure
                });

                usedBondIds.Add(BondWithExactTenure.Id);
                tenuresThatDoNotRequireInterpolation.Add(BondTenure);
            }
            else
            {
                tenuresThatRequireInterPolation.Add(benchmark.Key);
                // FOR EACH OF THE BONDS WITHIN THE TENURE, Create a Yield Curve (We will interpolate the missing ones later)
                foreach (var bond in bondsWithinThisTenure)
                {
                    if (usedBondIds.Contains(bond.Id))
                    {
                        continue; // Skip bonds that have already been used
                    }
                    var impliedYield = _db.ImpliedYields.Where(i => i.BondId == bond.Id && i.YieldDate.Date == parsedDate.Date).FirstOrDefault();
                    if (impliedYield == null)
                    {
                        result.Success = false;
                        result.ErrorMessage = $"The Bond {bond.IssueNumber} seems not to have an Implied Yield for the date {parsedDate}";
                        return result;
                    }
                    var BondTenure = Math.Round((bond.MaturityDate.Date - parsedDate.Date).TotalDays / 364, 4, MidpointRounding.AwayFromZero);
                    result.YieldCurveCalculations.Add(new YieldCurveDataSet
                    {
                        Yield = (double)Math.Round(impliedYield.Yield, 4, MidpointRounding.AwayFromZero),
                        BondUsed = bond.IssueNumber,
                        IssueDate = bond.IssueDate,
                        MaturityDate = bond.MaturityDate,
                        Tenure = BondTenure
                    });
                    usedBondIds.Add(bond.Id);
                }
            }
        }
        result.Success = true;
        return result;
    }

    public static List<FinalYieldCurveData> GenerateYieldCurves(HashSet<double> tenuresThatRequireInterPolation, HashSet<double> tenuresThatDoNotRequireInterpolation, List<YieldCurveDataSet> yieldCurveCalculations)
    {
        HashSet<double> tenuresToPlot = new HashSet<double>();
        List<FinalYieldCurveData> yieldCurves = new List<FinalYieldCurveData>();

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
                        BondUsed = _BondUsed,
                        BenchMarkTenor = tenureToPlot,
                    });
                }
            }
        }

        return yieldCurves;
    }


    public static void AddOneYearTBillToYieldCurve(DateTime parsedDate, HashSet<double> tenuresThatDoNotRequireInterpolation, List<YieldCurveDataSet> yieldCurveCalculations)
    {
        var currentOneYearTBill = GetCurrentOneYearTBill(parsedDate);

        if (currentOneYearTBill == null)
        {
            throw new Exception("Seems the implied yield for the 1 year TBill for the date " + parsedDate.Date + " does not exist.");
        }

        // tadd the 1 year TBill to the yield curve
        yieldCurveCalculations.Add(new YieldCurveDataSet
        {
            Yield = (double)Math.Round(currentOneYearTBill.Yield, 4, MidpointRounding.AwayFromZero),
            BondUsed = "1 Year TBill",
            IssueDate = currentOneYearTBill.TBill.IssueDate,
            MaturityDate = currentOneYearTBill.TBill.MaturityDate,
            Tenure = 1
        });
        tenuresThatDoNotRequireInterpolation.Add(1);
    }

    public static List<FinalYieldCurveData> ProcessYieldCurve(DateTime parsedDate, QuotationsBoardContext _db, List<YieldCurveDataSet> yieldCurveCalculations, Dictionary<int, (double, double)> benchmarkRanges, HashSet<double> tenuresThatRequireInterPolation, HashSet<double> tenuresThatDoNotRequireInterpolation, HashSet<string> usedBondIds)
    {
        var fXdBonds = YieldCurveHelper.GetFXDBonds(parsedDate);
        ProcessBenchmarkResult processBenchmarkResult = YieldCurveHelper.ProcessBenchmarkRanges(benchmarkRanges, tenuresThatRequireInterPolation, tenuresThatDoNotRequireInterpolation, usedBondIds, parsedDate, _db, fXdBonds);
        if (!processBenchmarkResult.Success)
        {
            throw new Exception(processBenchmarkResult.ErrorMessage);
        }
        yieldCurveCalculations.AddRange(processBenchmarkResult.YieldCurveCalculations);
        var interpolatedYieldCurve = YieldCurveHelper.InterpolateWhereNecessary(yieldCurveCalculations, tenuresThatRequireInterPolation);
        return YieldCurveHelper.GenerateYieldCurves(tenuresThatRequireInterPolation, tenuresThatDoNotRequireInterpolation, yieldCurveCalculations);
    }






}










