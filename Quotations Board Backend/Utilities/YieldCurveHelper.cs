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


    private static decimal PerformLinearInterpolation(YieldCurve previousData, YieldCurve nextData, decimal targetTenor)
    {

        var tenorDifference = nextData.BenchMarkTenor - previousData.BenchMarkTenor;
        decimal yieldDifference = nextData.Yield - previousData.Yield;
        decimal tenorRatio = (targetTenor - previousData.BenchMarkTenor) / tenorDifference;

        return previousData.Yield + tenorRatio * yieldDifference;
    }






}