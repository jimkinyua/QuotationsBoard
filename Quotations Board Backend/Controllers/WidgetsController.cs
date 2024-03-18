using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Quotations_Board_Backend.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class WidgetsController : ControllerBase
    {
        // get Yield Curve for a specific date
        [HttpPost("YieldCurve")]
        public ActionResult<IEnumerable<FinalYieldCurveData>> GetYieldCurve([FromBody] APIYieldCurveRequest ApiRequest)
        {
            if (!ModelState.IsValid)
            {
                return BadRequest("Invalid client request. The date provided is not valid. The date should be in the format: yyyy-MM-dd");
            }

            try
            {
                DateTime parsedDate = ApiRequest.Date;
                if (parsedDate == DateTime.MinValue)
                {
                    return BadRequest($"Invalid date provided: {ApiRequest.Date}");
                }

                using (var context = new QuotationsBoardContext())
                {
                    Dictionary<int, (double, double)> benchmarkRanges = YieldCurveHelper.GetBenchmarkRanges(parsedDate);
                    HashSet<double> tenuresThatRequireInterPolation = new HashSet<double>();
                    HashSet<double> tenuresThatDoNotRequireInterpolation = new HashSet<double>();
                    HashSet<string> usedBondIds = new HashSet<string>();
                    List<FinalYieldCurveData> YieldCurveToPlot = new List<FinalYieldCurveData>();
                    List<YieldCurveDataSet> yieldCurveCalculations = new List<YieldCurveDataSet>();
                    List<FinalYieldCurveData> previousCurve = new List<FinalYieldCurveData>();

                    AddOneYearTBillResult addResult = YieldCurveHelper.AddOneYearTBillToYieldCurve(parsedDate, tenuresThatDoNotRequireInterpolation, yieldCurveCalculations);
                    if (addResult.Success == false)
                    {
                        return BadRequest(addResult.ErrorMessage);
                    }
                    ProcessBenchmarkResult Mnaoes = YieldCurveHelper.ProcessYieldCurve(parsedDate, context, yieldCurveCalculations, benchmarkRanges, tenuresThatRequireInterPolation, tenuresThatDoNotRequireInterpolation, usedBondIds);
                    if (Mnaoes.Success == false)
                    {
                        return BadRequest(Mnaoes.ErrorMessage);
                    }
                    yieldCurveCalculations.AddRange(Mnaoes.YieldCurveCalculations);
                    YieldCurveHelper.InterpolateWhereNecessary(yieldCurveCalculations, tenuresThatRequireInterPolation, previousCurve);
                    YieldCurveToPlot = YieldCurveHelper.GenerateYieldCurves(tenuresThatRequireInterPolation, tenuresThatDoNotRequireInterpolation, yieldCurveCalculations);
                    return Ok(YieldCurveToPlot);

                }
            }
            catch (Exception Ex)
            {
                UtilityService.LogException(Ex);
                return StatusCode(500, UtilityService.HandleException(Ex));
            }
        }

        // Quoted Yield Curve
        [HttpPost("QuotedYieldCurve")]
        public async Task<ActionResult<List<FinalYieldCurveData>>> QuotedYieldCurve()
        {
            try
            {
                DateTime fromDate = DateTime.Now.AddDays(-10);
                using (var context = new QuotationsBoardContext())
                {

                    List<BondAndAverageQuotedYield> bondAndAverageQuotedYields = new List<BondAndAverageQuotedYield>();
                    List<FinalYieldCurveData> yieldCurveToPlot = new List<FinalYieldCurveData>();
                    Dictionary<int, (double, double)> benchmarkRanges = YieldCurveHelper.GetBenchmarkRanges(fromDate);
                    HashSet<double> tenuresThatRequireInterPolation = new HashSet<double>();
                    HashSet<double> tenuresThatDoNotRequireInterpolation = new HashSet<double>();
                    HashSet<string> usedBondIds = new HashSet<string>();
                    List<YieldCurveDataSet> yieldCurveCalculations = new List<YieldCurveDataSet>();
                    var (startofCycle, endOfCycle) = TBillHelper.GetCurrentTBillCycle(fromDate);
                    var bondsNotMatured = context.Bonds.Where(b => b.BondCategory == "FXD" && b.MaturityDate.Date > fromDate.Date).ToList();

                    var quotationsForSelectedDate = await QuotationsHelper.GetQuotationsForDate(fromDate);
                    var mostRecentDateWithQuotations = await QuotationsHelper.GetMostRecentDateWithQuotationsBeforeDateInQuestion(fromDate);
                    var quotationsForMostRecentDate = await QuotationsHelper.GetQuotationsForDate(mostRecentDateWithQuotations);
                    var res = YieldCurveHelper.AddOneYearTBillToYieldCurve(mostRecentDateWithQuotations, tenuresThatDoNotRequireInterpolation, yieldCurveCalculations, true);
                    if (res.Success == false)
                    {
                        return BadRequest(res.ErrorMessage);
                    }
                    var previousYieldCurveData = await QuotationsHelper.InterpolateValuesForLastQuotedDayAsync(mostRecentDateWithQuotations, quotationsForMostRecentDate);

                    // check if there are any quotations for the selected date
                    if (quotationsForSelectedDate.Count == 0)
                    {
                        if (mostRecentDateWithQuotations == default(DateTime))
                        {
                            return BadRequest("There are no quotations available.");
                        }

                        quotationsForSelectedDate = quotationsForMostRecentDate;
                        yieldCurveToPlot = previousYieldCurveData;
                        return StatusCode(200, yieldCurveToPlot);
                    }

                    // Check if previousYieldCurveData exists (Wierd but Shit Happens). 
                    if (previousYieldCurveData == null || previousYieldCurveData.Count == 0)
                    {

                    }

                    var groupedQuotations = quotationsForSelectedDate.GroupBy(x => x.BondId);

                    foreach (var bondQuotes in groupedQuotations)
                    {

                        var bondDetails = await context.Bonds.FirstOrDefaultAsync(b => b.Id == bondQuotes.Key);
                        if (bondDetails == null)
                        {
                            continue;
                        }
                        var RemainingTenor = (bondDetails.MaturityDate - fromDate.Date).TotalDays / 364;

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

                        var bondsWithinThisTenure = YieldCurveHelper.GetBondsInTenorRange(bondsNotMatured, benchmarkRange, usedBondIds, fromDate);

                        // Find newly quoted bonds within the range
                        var newlyQuotedBonds = bondsWithinThisTenure
                            .Where(b => quotationsForSelectedDate.Any(q => q.BondId == b.Id))
                            .ToList();

                        // Select the bond closest to the benchmark tenure
                        var newlyQuotedBond = newlyQuotedBonds
                            .OrderBy(b => Math.Abs(benchmarkRange.Key - Math.Round((b.MaturityDate.Date - fromDate.Date).TotalDays / 364, 4, MidpointRounding.AwayFromZero)))
                            .FirstOrDefault();

                        // If a newly quoted bond is found, use it; otherwise, find the closest bond within the tenure range
                        Bond? bondToUse = newlyQuotedBond?.Id != null ? context.Bonds.Find(newlyQuotedBond.Id) : YieldCurveHelper.GetBondWithExactTenure(bondsWithinThisTenure, benchmarkRange.Value.Item1, fromDate);

                        if (bondsWithinThisTenure.Count() == 0 && benchmarkRange.Key != 1)
                        {
                            tenuresThatRequireInterPolation.Add(benchmarkRange.Key);
                            continue;
                        }
                        else
                        {
                            BondWithExactTenure = YieldCurveHelper.GetBondWithExactTenure(bondsWithinThisTenure, benchmarkRange.Value.Item1, fromDate);
                        }

                        if (BondWithExactTenure != null)
                        {
                            // was this bond quoted? some may have excat tenure but not quoted
                            var bondAndAverageQuotedYield = bondAndAverageQuotedYields.FirstOrDefault(b => b.BondId == BondWithExactTenure.Id);
                            if (bondAndAverageQuotedYield != null)
                            {
                                var BondTenure = Math.Round((BondWithExactTenure.MaturityDate.Date - fromDate.Date).TotalDays / 364, 4, MidpointRounding.AwayFromZero);

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

                        // No bond with exact tenure was found
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
                                    var BondTenure = Math.Round((bond.MaturityDate.Date - fromDate.Date).TotalDays / 364, 4, MidpointRounding.AwayFromZero);

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
                    var interpolatedYieldCurve = YieldCurveHelper.InterpolateWhereNecessary(yieldCurveCalculations, tenuresThatRequireInterPolation, previousYieldCurveData);
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
                                yieldCurveToPlot.Add(new FinalYieldCurveData
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
                    return StatusCode(200, yieldCurveToPlot);

                }

            }
            catch (Exception Ex)
            {
                UtilityService.LogException(Ex);
                return StatusCode(500, UtilityService.HandleException(Ex));
            }
        }

    }
}
