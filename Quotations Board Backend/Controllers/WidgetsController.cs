using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace Quotations_Board_Backend.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class WidgetsController : ControllerBase
    {
        // get Yield Curve for a specific date
        [HttpPost("QuotedYieldCurve")]
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
    }
}
