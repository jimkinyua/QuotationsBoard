public static class YieldCurveHelper
{
    public static List<YieldCurve> InterpolateMissingYields(List<YieldCurve> yieldCurveDataList)
    {
        for (int i = 0; i < yieldCurveDataList.Count; i++)
        {
            if (IsYieldMissing(yieldCurveDataList[i]))
            {
                YieldCurve previousData = FindPreviousDataWithYield(yieldCurveDataList, i);
                YieldCurve nextData = FindNextDataWithYield(yieldCurveDataList, i);

                if (previousData != null && nextData != null)
                {
                    yieldCurveDataList[i].Yield = PerformLinearInterpolation(previousData, nextData, yieldCurveDataList[i].BenchMarkTenor);
                    yieldCurveDataList[i].BondUsed = "Interpolated";
                }

            }


        }

        return yieldCurveDataList;
    }
    private static bool IsYieldMissing(YieldCurve data)
    {
        return data.Yield == 0 || string.IsNullOrEmpty(data.BondUsed);
    }


    private static YieldCurve FindPreviousDataWithYield(List<YieldCurve> dataList, int currentIndex)
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

    private static YieldCurve FindNextDataWithYield(List<YieldCurve> dataList, int currentIndex)
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
    private static decimal PerformLinearInterpolation(YieldCurve previousData, YieldCurve nextData, decimal targetTenor)
    {
        // tenorDifference represents (x2 - x1), the difference in tenor between the next and previous data points.
        var tenorDifference = nextData.BenchMarkTenor - previousData.BenchMarkTenor;

        // yieldDifference represents (y2 - y1), the difference in yield between the next and previous data points.
        decimal yieldDifference = nextData.Yield - previousData.Yield;

        // tenorRatio represents ((x - x1) / (x2 - x1)), the proportion of the target tenor between the previous and next tenors.
        decimal tenorRatio = (targetTenor - previousData.BenchMarkTenor) / tenorDifference;

        // Applying the linear interpolation formula: y = y1 + ((x - x1) * (y2 - y1) / (x2 - x1))
        // where y is the interpolated yield, x is the target tenor, x1 and y1 are the tenor and yield of the previous data point,
        // and x2 and y2 are the tenor and yield of the next data point.
        var interpolatedYield = previousData.Yield + (tenorRatio * yieldDifference);
        return Math.Round(interpolatedYield, 4, MidpointRounding.AwayFromZero);
    }



    public static Dictionary<int, (double, double)> GenerateBenchmarkRanges(double maxTenure)
    {
        Dictionary<int, (double, double)> benchmarkRanges = new Dictionary<int, (double, double)>();
        int startYear = 2; // Assuming you want to start from 2 years

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
            var fXdBonds = _unMaturedBonds.Where(b => b.BondCategory == "USD").ToList();

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





}