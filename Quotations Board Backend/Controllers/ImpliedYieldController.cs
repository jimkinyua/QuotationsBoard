using System.Globalization;
using ClosedXML.Excel;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Quotations_Board_Backend.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class ImpliedYieldController : ControllerBase
    {
        // Add Implied Yield Curve Manually
        [HttpPost]
        [Route("AddImpliedYieldManually")]
        [Authorize(Roles = CustomRoles.SuperAdmin, AuthenticationSchemes = "Bearer")]
        public IActionResult AddImpliedYieldManually([FromBody] AddImpliedYieldDTO addImpliedYieldDTO)
        {
            if (!ModelState.IsValid)
            {
                return BadRequest(ModelState);
            }

            try
            {
                using (var db = new QuotationsBoardContext())
                {
                    var bond = db.Bonds.Find(addImpliedYieldDTO.BondId);
                    if (bond == null)
                    {
                        return BadRequest("Bond not found");
                    }

                    // Ensure  that No Implied Yield Exists for this Bond on this Date
                    var impliedYiedExists = db.ImpliedYields.Any(i => i.BondId == addImpliedYieldDTO.BondId && i.YieldDate.Date == addImpliedYieldDTO.YieldDate.Date);
                    if (impliedYiedExists)
                    {
                        return BadRequest("Implied Yield already exists for this Bond on this Date");
                    }

                    var impliedYield = new ImpliedYield
                    {
                        BondId = addImpliedYieldDTO.BondId,
                        Yield = addImpliedYieldDTO.Yield,
                        YieldDate = addImpliedYieldDTO.YieldDate
                    };

                    db.ImpliedYields.Add(impliedYield);
                    db.SaveChanges();

                    return Ok();

                }
            }
            catch (Exception Ex)
            {
                UtilityService.LogException(Ex);
                return StatusCode(500, UtilityService.HandleException(Ex));

            }
        }

        // Upload Implied Yield Curve
        [HttpPost]
        [Route("UploadImpliedYield")]
        [Authorize(Roles = CustomRoles.SuperAdmin, AuthenticationSchemes = "Bearer")]
        public async Task<IActionResult> UploadImpliedYield([FromForm] UploadImpliedYield uploadImpliedYield)
        {
            if (!ModelState.IsValid)
            {
                return BadRequest(ModelState);
            }

            try
            {
                var UploadFile = uploadImpliedYield.ExcelFile;
                using (var db = new QuotationsBoardContext())
                {

                    using (var stream = new MemoryStream())
                    {
                        await UploadFile.CopyToAsync(stream);
                        using (var workbook = new XLWorkbook(stream))
                        {
                            var sheetWhereDataIsLocated = workbook.Worksheet(1);
                            var validationResults = ValidateExcelData(sheetWhereDataIsLocated);
                            if (validationResults.Any())
                            {
                                return BadRequest(validationResults);
                            }
                            var impliedYields = ReadBondExcelData(sheetWhereDataIsLocated);
                            db.ImpliedYields.AddRange(impliedYields);
                            db.SaveChanges();
                            return Ok();
                        }
                    }
                }
            }
            catch (Exception Ex)
            {
                UtilityService.LogException(Ex);
                return StatusCode(500, UtilityService.HandleException(Ex));

            }
        }

        private List<string> ValidateExcelData(IXLWorksheet worksheet)
        {
            var errors = new List<string>();
            int rowCount = CountNonEmptyRows(worksheet);
            int maxColumnCount = worksheet.ColumnsUsed().Count();


            for (int rowToBeginAt = maxColumnCount; rowToBeginAt <= rowCount; rowToBeginAt++)
            {
                // Check if the row is empty
                bool isEmptyRow = true;
                for (int col = 1; col <= maxColumnCount; col++)
                {
                    if (!string.IsNullOrWhiteSpace(worksheet.Cell(rowToBeginAt, col).Value.ToString()))
                    {
                        isEmptyRow = false;
                        break; // Exit the loop as soon as a non-empty cell is found
                    }
                }

                string excelIssueNo = worksheet.Cell(rowToBeginAt, 1).Value.ToString();

                if (string.IsNullOrWhiteSpace(excelIssueNo))
                {
                    errors.Add($"Row {rowToBeginAt}: 'Issue No' is required.");
                    continue;
                }

                using (var dbContext = new QuotationsBoardContext())
                {
                    dbContext.Database.EnsureCreated();
                    // Check if transformedSecurityId exists in the database
                    var bondExists = dbContext.Bonds.Any(b => b.IssueNumber == excelIssueNo);
                    if (!bondExists)
                    {
                        errors.Add($"Row {rowToBeginAt}: Security ID '{excelIssueNo}' does not exist in the system.");
                    }
                }


                if (isEmptyRow) continue; // Skip this row if it's empty

                var yieldValue = worksheet.Cell(rowToBeginAt, 2).Value.ToString();

                if (string.IsNullOrWhiteSpace(yieldValue))
                {
                    errors.Add($"Row {rowToBeginAt}: 'Issue No' is required.");
                    continue;
                }
                if (!decimal.TryParse(yieldValue, out _))
                {
                    errors.Add($"Row {rowToBeginAt} Cell D: 'Executed Size' is not a valid decimal number.");
                }

                var yieldDate = worksheet.Cell(rowToBeginAt, 3).Value.ToString();

                if (string.IsNullOrWhiteSpace(yieldDate))
                {
                    errors.Add($"Row {rowToBeginAt}: 'Yield Date' is required.");
                    continue;
                }
                if (!DateTime.TryParse(yieldDate, out _))
                {
                    errors.Add($"Row {rowToBeginAt} Cell A: 'Yield Date' is not a valid date.");
                }

            }

            return errors;
        }

        private int CountNonEmptyRows(IXLWorksheet worksheet)
        {
            int nonEmptyRowCount = 0;
            int maxColumnCount = worksheet.ColumnsUsed().Count(); // Count only the used columns

            foreach (var row in worksheet.RowsUsed())
            {
                for (int col = 1; col <= maxColumnCount; col++)
                {
                    if (!string.IsNullOrWhiteSpace(row.Cell(col).Value.ToString()))
                    {
                        nonEmptyRowCount++;
                        break; // Move to the next row once a non-empty cell is found
                    }
                }
            }

            return nonEmptyRowCount;
        }

        private List<ImpliedYield> ReadBondExcelData(IXLWorksheet worksheet)
        {

            var impliedYields = new List<ImpliedYield>();
            int rowCount = CountNonEmptyRows(worksheet); // You need to implement this method.

            for (int row = 2; row <= rowCount; row++)
            {
                // Check if the row is empty (assuming Bond is in column 1 and Yield is in column 2)
                if (string.IsNullOrWhiteSpace(worksheet.Cell(row, 1).Value.ToString()) &&
                    string.IsNullOrWhiteSpace(worksheet.Cell(row, 2).Value.ToString()))
                {
                    continue; // Skip the empty row
                }

                var excelIssueNo = worksheet.Cell(row, 1).Value.ToString();
                var yieldValue = worksheet.Cell(row, 2).Value.ToString();
                var yieldDate = worksheet.Cell(row, 3).Value.ToString();
                var yiedlValueAsFloat = float.Parse(yieldValue);

                using (var dbContext = new QuotationsBoardContext())
                {
                    dbContext.Database.EnsureCreated();
                    // Check if transformedSecurityId exists in the database
                    var bondExists = dbContext.Bonds.Where(b => b.IssueNumber == excelIssueNo).FirstOrDefault();
                    if (bondExists == null)
                    {
                        throw new Exception($"Security ID '{excelIssueNo}' does not exist in the system.");
                    }

                    // ensure no implied with same tenor within same week exists more than once
                    var impliedYieldExists = dbContext.ImpliedYields.Any(i => i.BondId == bondExists.Id && i.YieldDate.Date == DateTime.Parse(yieldDate).Date);
                    if (impliedYieldExists)
                    {
                        throw new Exception($"Implied Yield for the Bond '{excelIssueNo}' on '{yieldDate}' already exists in the system.");
                    }

                    // Assuming data is already validated and can be directly parsed
                    var impliedYield = new ImpliedYield
                    {
                        BondId = bondExists.Id,
                        Yield = Math.Round(yiedlValueAsFloat, 4, MidpointRounding.AwayFromZero),
                        YieldDate = DateTime.Parse(yieldDate)
                    };

                    impliedYields.Add(impliedYield);
                }


            }

            return impliedYields;

        }

        // Calculate Implied Yield for each Bond
        [HttpPost]
        [Route("CalculateImpliedYield")]
        [Authorize(Roles = CustomRoles.SuperAdmin, AuthenticationSchemes = "Bearer")]
        public ActionResult<IEnumerable<ComputedImpliedYield>> CalculateImpliedYield([FromBody] CalculateImpliedYield calculateImpliedYield)
        {
            if (!ModelState.IsValid)
            {
                return BadRequest(ModelState);
            }


            try
            {
                using (var db = new QuotationsBoardContext())
                {
                    var DateInQuestion = calculateImpliedYield.YieldDate;

                    var (startOfCycle, endOfCycle) = TBillHelper.GetCurrentTBillCycle(DateInQuestion);
                    var (startOfLastWeek, endOfLastWeek) = TBillHelper.GetPreviousTBillCycle(DateInQuestion);


                    List<ComputedImpliedYield> computedImpliedYields = new List<ComputedImpliedYield>();
                    var bonds = db.Bonds.ToList();
                    var TBills = db.TBills.ToList();
                    var bondsNotMatured = bonds.Where(b => b.MaturityDate.Date > DateInQuestion.Date).ToList();
                    var tBillsNotMature = TBills.Where(t => t.MaturityDate.Date > DateInQuestion.Date).ToList();

                    var currentTbills = tBillsNotMature
                        .Where(t => t.IssueDate.Date >= startOfCycle.Date
                                    && t.IssueDate.Date <= endOfCycle.Date)
                        .ToList();

                    var lastWeekTbills = tBillsNotMature
                    .Where(t => t.IssueDate.Date >= startOfLastWeek.Date
                                && t.IssueDate.Date <= endOfLastWeek.Date)
                    .ToList();



                    double AllowedMarginOfError = 1;
                    var curentIneYearTBill = currentTbills.Where(t => t.Tenor >= 364).FirstOrDefault();
                    var oneYearTBillForLastWeek = lastWeekTbills.Where(t => t.Tenor >= 364).FirstOrDefault();
                    if (curentIneYearTBill == null)
                    {
                        return BadRequest("One year Tbill for the current week starting from " + startOfCycle + " to " + endOfCycle + " does not exist. This is required to calculate the variance betwwen the current and previous One Year TBill");
                    }
                    if (oneYearTBillForLastWeek == null)
                    {
                        return BadRequest("One year Tbill fro teh week starting from " + startOfLastWeek + " to " + endOfLastWeek + " does not exist. This is required to calculate the variance betwwen the current and previous One Year TBill");
                    }

                    foreach (var bond in bondsNotMatured)
                    {
                        var bondTradeLines = QuotationsHelper.GetBondTradeLinesForBond(bond.Id, DateInQuestion);
                        var quotations = QuotationsHelper.GetQuotationsForBond(bond.Id, DateInQuestion);
                        var averageWeightedTradedYield = QuotationsHelper.CalculateAverageWeightedTradedYield(bondTradeLines);
                        var averageWeightedQuotedYield = QuotationsHelper.CalculateAverageWeightedQuotedYield(quotations);

                        var previousImpliedYield = db.ImpliedYields
                        .Where(i => i.BondId == bond.Id
                            && i.YieldDate.Date < DateInQuestion.Date)
                        .OrderByDescending(i => i.YieldDate)
                        .FirstOrDefault();

                        double _preImpYield = 0;
                        if (previousImpliedYield == null)
                        {
                            // continue;
                            return BadRequest($"Seems the previous Implied Yield for the Bond {bond.IssueNumber} does not exist. Could this be the first time the Implied Yield is being calculated for this Bond? If so, please add the Implied Yield manually via the template provided.");
                        }
                        else
                        {
                            _preImpYield = previousImpliedYield.Yield;
                        }
                        var QuotedAndPrevious = averageWeightedQuotedYield - _preImpYield; //previousImpliedYield.Yield;
                        var TradedAndPrevious = averageWeightedTradedYield - _preImpYield; //previousImpliedYield.Yield;
                        var VarianceinTBills = (curentIneYearTBill.Yield - oneYearTBillForLastWeek.Yield);

                        bool isQuotedWithinMargin = QuotationsHelper.IsWithinMargin(averageWeightedQuotedYield, _preImpYield, AllowedMarginOfError);
                        bool isTradedWithinMargin = QuotationsHelper.IsWithinMargin(averageWeightedTradedYield, _preImpYield, AllowedMarginOfError);

                        double impliedYield;
                        int selectedYield;
                        var reasonForSelection = string.Empty;

                        if (isQuotedWithinMargin && isTradedWithinMargin)
                        {
                            // If both are within margin, the tradedMargin takes precedence
                            impliedYield = averageWeightedTradedYield;
                            selectedYield = SelectedYield.TradedYield;
                            reasonForSelection = $"Both Quoted and Traded are within margin of error. Traded Yield is selected because it takes precedence over Quoted Yield: {averageWeightedQuotedYield}, Traded Yield: {averageWeightedTradedYield}, Previous Implied Yield: {previousImpliedYield.Yield}";
                        }
                        else if (isQuotedWithinMargin)
                        {
                            impliedYield = averageWeightedQuotedYield;
                            selectedYield = SelectedYield.QuotedYield;
                            reasonForSelection = $"Selected quoted yield ({averageWeightedQuotedYield}%). Average Traded yield is  {averageWeightedTradedYield}%, Previous Implied Yield is {_preImpYield}%)";
                        }
                        else if (isTradedWithinMargin)
                        {
                            impliedYield = averageWeightedTradedYield;
                            selectedYield = SelectedYield.TradedYield;
                            reasonForSelection = $"Selected Traded yield ({averageWeightedTradedYield}%). Average Quoted yield is  {averageWeightedQuotedYield}%, Previous Implied Yield is {_preImpYield}%)";
                        }
                        else
                        {
                            // None meets Condition so we stick with the previous Implied Yield
                            impliedYield = previousImpliedYield.Yield;
                            selectedYield = SelectedYield.PreviousYield;
                            reasonForSelection = $"Previous Implied Yield is selected: {previousImpliedYield.Yield} None of the Quoted and Traded are within the 1% margin. Quoted Yield is {averageWeightedQuotedYield}%, Traded Yield is {averageWeightedTradedYield}%";

                        }
                        computedImpliedYields.Add(new ComputedImpliedYield
                        {
                            BondId = bond.IssueNumber,
                            Yield = impliedYield,
                            YieldDate = DateInQuestion,
                            ReasonForSelection = reasonForSelection,
                            SelectedYield = selectedYield,
                            TradedYield = averageWeightedTradedYield,
                            QuotedYield = averageWeightedQuotedYield,
                            PreviousYield = _preImpYield //previousImpliedYield.Yield
                        });
                    }

                    return Ok(computedImpliedYields);
                }
            }
            catch (Exception Ex)
            {
                UtilityService.LogException(Ex);
                return StatusCode(500, UtilityService.HandleException(Ex));

            }
        }

        // Confrim Implied Yield
        [HttpPost]
        [Route("DraftImpliedYield")]
        [Authorize(Roles = CustomRoles.SuperAdmin, AuthenticationSchemes = "Bearer")]

        public IActionResult DraftImpliedYield([FromBody] DraftImpliedYieldDTO _draftImpliedYieldDTO)
        {
            if (!ModelState.IsValid)
            {
                return BadRequest(ModelState);
            }

            // limit calculation of future implied yields
            if (_draftImpliedYieldDTO.YieldDate.Date > DateTime.Now.Date)
            {
                return BadRequest("You cannot calculate Implied Yield for a future date");
            }

            try
            {
                using (var db = new QuotationsBoardContext())
                {
                    var bonds = db.Bonds.ToList();
                    var bondsNotMatured = bonds.Where(b => b.MaturityDate.Date > _draftImpliedYieldDTO.YieldDate.Date).ToList();

                    foreach (var impliedYield in _draftImpliedYieldDTO.ImpliedYields)
                    {
                        var bondDetails = bondsNotMatured.Where(b => b.IssueNumber == impliedYield.BondId).FirstOrDefault();
                        if (bondDetails == null)
                        {
                            return BadRequest($"Bond with Id {impliedYield.BondId} does not exist or has matured");
                        }
                        double YieldToSave = 0;
                        var selectedImpliedYield = impliedYield.SelectedYield;
                        if (selectedImpliedYield == SelectedYield.PreviousYield)
                        {
                            YieldToSave = impliedYield.PreviousYield;
                        }
                        else if (selectedImpliedYield == SelectedYield.QuotedYield)
                        {
                            YieldToSave = impliedYield.QuotedYield;
                        }
                        else if (selectedImpliedYield == SelectedYield.TradedYield)
                        {
                            YieldToSave = impliedYield.TradedYield;
                        }
                        else
                        {
                            return BadRequest("Seems you have selected an invalid Implied Yield");
                        }
                        var existingImpliedYield = db.DraftImpliedYields.Where(i => i.YieldDate.Date == _draftImpliedYieldDTO.YieldDate.Date && i.BondId == bondDetails.Id).FirstOrDefault();
                        if (existingImpliedYield != null)
                        {
                            existingImpliedYield.Yield = YieldToSave;
                            db.Entry(existingImpliedYield).State = EntityState.Modified;
                        }
                        else
                        {
                            var impliedYieldToSave = new DraftImpliedYield
                            {
                                BondId = bondDetails.Id,
                                Yield = YieldToSave,
                                YieldDate = impliedYield.YieldDate
                            };
                            db.DraftImpliedYields.Add(impliedYieldToSave);
                        }
                    }

                    db.SaveChanges();

                    return Ok();

                }
            }
            catch (Exception Ex)
            {
                UtilityService.LogException(Ex);
                return StatusCode(500, UtilityService.HandleException(Ex));

            }
        }

        // gets the preliminary Yield Curve based on the Draft Implied Yields
        [HttpGet]
        [Route("GetPreliminaryYieldCurve")]
        [Authorize(Roles = CustomRoles.SuperAdmin, AuthenticationSchemes = "Bearer")]

        public ActionResult<IEnumerable<FinalYieldCurveData>> CalculatePreliminaryYieldCurve(string? For = "default")
        {
            var m = DateTime.Now;
            var parsedDate = DateTime.Now;

            using (var _db = new QuotationsBoardContext())
            {
                if (For == "default" || For == null || string.IsNullOrWhiteSpace(For))
                {
                    // parsedDate = DateTime.Now;
                }
                else
                {
                    string[] formats = { "dd/MM/yyyy", "yyyy-MM-dd", "MM/dd/yyyy", "dd-MM-yyyy", "dd/MM/yyyy HH:mm:ss", "yyyy-MM-dd HH:mm:ss", "MM/dd/yyyy HH:mm:ss", "dd-MM-yyyy HH:mm:ss" };
                    DateTime targetTradeDate;
                    bool success = DateTime.TryParseExact(For, formats, CultureInfo.InvariantCulture, DateTimeStyles.None, out targetTradeDate);
                    if (!success)
                    {
                        return BadRequest("The date format is invalid");
                    }
                    parsedDate = targetTradeDate.Date;
                }
                var (startOfCycle, endOfCycle) = TBillHelper.GetCurrentTBillCycle(parsedDate);

                // Fetch all Bonds  under FXD Category that are not matured
                var fXdBonds = _db.Bonds.Where(b => b.BondCategory == "FXD" && b.MaturityDate.Date > parsedDate.Date).ToList();
                // var fXdBonds = _db.Bonds.Where(b => b.MaturityDate.Date > DateTime.Now.Date).ToList();

                var currentOneYearTBill = _db.TBills
                .Where(t => t.Tenor >= 364 && t.IssueDate.Date >= startOfCycle.Date && t.IssueDate.Date <= endOfCycle.Date)
                .FirstOrDefault();


                if (currentOneYearTBill == null)
                {
                    return BadRequest("Seems the 1 year TBill for the current week starting from " + startOfCycle + " to " + endOfCycle + " does not exist.");
                }

                var bondDates = fXdBonds
                .Select(b => new { b.MaturityDate, b.IssueDate })
                .ToList();

                var maxTenure = bondDates.Max(b => (b.MaturityDate.Date - parsedDate.Date).TotalDays / 364);
                // var _roundedMaxTenure = Math.Floor(maxTenure);
                var _floorMaxTenure = Math.Floor(maxTenure);
                var _ceilMaxTenure = Math.Ceiling(maxTenure);


                // Generate benchmark ranges dynamically
                Dictionary<int, (double, double)> benchmarkRanges = YieldCurveHelper.GetBenchmarkRanges(parsedDate);

                Dictionary<int, bool> benchmarksFound = new Dictionary<int, bool>();

                List<int> benchMarkTenorsForYiedCurve = new List<int> { 2, 5, 10, 15, 20, 25 };
                HashSet<double> tenuresThatRequireInterPolation = new HashSet<double>();
                HashSet<double> tenuresThatDoNotRequireInterpolation = new HashSet<double>();
                HashSet<string> usedBondIds = new HashSet<string>();

                // for each benchmark range, fetch the bond that is closest to the benchmark range
                List<FinalYieldCurveData> yieldCurves = new List<FinalYieldCurveData>();
                List<YieldCurveDataSet> yieldCurveCalculations = new List<YieldCurveDataSet>();
                // tadd the 1 year TBill to the yield curve
                yieldCurveCalculations.Add(new YieldCurveDataSet
                {
                    Yield = (double)Math.Round(currentOneYearTBill.Yield, 4, MidpointRounding.AwayFromZero),
                    BondUsed = "1 Year TBill",
                    IssueDate = currentOneYearTBill.IssueDate,
                    MaturityDate = currentOneYearTBill.MaturityDate,
                    Tenure = 1
                });
                tenuresThatDoNotRequireInterpolation.Add(1);

                foreach (var benchmark in benchmarkRanges)
                {
                    Bond? BondWithExactTenure = null;
                    //var closestBond = GetClosestBond(fXdBonds, benchmark, usedBondIds, parsedDate);
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
                        var draftImpliedYield = _db.DraftImpliedYields.Where(i => i.BondId == BondWithExactTenure.Id && i.YieldDate.Date == parsedDate.Date).FirstOrDefault();
                        if (draftImpliedYield == null)
                        {
                            return BadRequest($"The Bond {BondWithExactTenure.IssueNumber} seems not to have an Implied Yield.");
                        }
                        var BondTenure = Math.Round((BondWithExactTenure.MaturityDate.Date - parsedDate.Date).TotalDays / 364, 4, MidpointRounding.AwayFromZero);
                        yieldCurveCalculations.Add(new YieldCurveDataSet
                        {
                            Yield = (double)Math.Round(draftImpliedYield.Yield, 4, MidpointRounding.AwayFromZero),
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
                            var _Possible_impliedYield = _db.DraftImpliedYields.Where(i => i.BondId == bond.Id && i.YieldDate.Date == parsedDate.Date).FirstOrDefault();
                            if (_Possible_impliedYield == null)
                            {
                                return BadRequest($"The Bond {bond.IssueNumber} seems not to have an Implied Yield for the date {parsedDate}");
                            }
                            var BondTenure = Math.Round((bond.MaturityDate.Date - parsedDate.Date).TotalDays / 364, 4, MidpointRounding.AwayFromZero);
                            yieldCurveCalculations.Add(new YieldCurveDataSet
                            {
                                Yield = (double)Math.Round(_Possible_impliedYield.Yield, 4, MidpointRounding.AwayFromZero),
                                BondUsed = bond.IssueNumber,
                                IssueDate = bond.IssueDate,
                                MaturityDate = bond.MaturityDate,
                                Tenure = BondTenure
                            });
                            usedBondIds.Add(bond.Id);
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
                                BondUsed = _BondUsed,
                                BenchMarkTenor = tenureToPlot,
                            });
                        }
                    }
                }

                return Ok(yieldCurves);

            }
        }




        // Confrim Implied Yield
        [HttpPost]
        [Route("ConfirmImpliedYield")]
        [Authorize(Roles = CustomRoles.SuperAdmin, AuthenticationSchemes = "Bearer")]

        public IActionResult ConfirmImpliedYield([FromBody] ConfirmImpliedYieldDTO confirmImpliedYieldDTO)
        {
            if (!ModelState.IsValid)
            {
                return BadRequest(ModelState);
            }
            // ensure yield date is not in the future
            if (confirmImpliedYieldDTO.YieldDate.Date > DateTime.Now.Date)
            {
                return BadRequest("You cannot confirm Implied Yield for a future date");
            }

            try
            {
                using (var db = new QuotationsBoardContext())
                {
                    var bonds = db.Bonds.ToList();
                    var impliedYields = db.ImpliedYields.ToList();
                    var bondsNotMatured = bonds.Where(b => b.MaturityDate.Date > confirmImpliedYieldDTO.YieldDate.Date).ToList();

                    // get the draft implied yields for the date provided
                    var existingDraftImpliedYield = db.DraftImpliedYields.Where(i => i.YieldDate.Date == confirmImpliedYieldDTO.YieldDate.Date).ToList();
                    if (!existingDraftImpliedYield.Any())
                    {
                        return BadRequest($"The Draft Implied Yield for the date {confirmImpliedYieldDTO.YieldDate} does not exist");
                    }

                    var (startOfCycle, endOfCycle) = TBillHelper.GetCurrentTBillCycle(confirmImpliedYieldDTO.YieldDate);
                    var (startOfLastWeek, endOfLastWeek) = TBillHelper.GetPreviousTBillCycle(confirmImpliedYieldDTO.YieldDate);

                    var currentTbills = db.TBills
                        .Where(t => t.IssueDate.Date >= startOfCycle.Date
                                    && t.IssueDate.Date <= endOfCycle.Date)
                        .ToList();

                    var TbillTenors = new Dictionary<int, string>
                                {
                                    { 91, "3 month" },
                                    { 182, "6 month" },
                                    { 364, "1 year" }
                                };

                    foreach (var tbillTenure in TbillTenors)
                    {
                        var currentTBill = currentTbills.Where(t => t.Tenor == tbillTenure.Key).FirstOrDefault();
                        if (currentTBill == null)
                        {
                            return BadRequest($"{tbillTenure.Value} Tbill for the current week starting from {startOfCycle} to {endOfCycle} does not exist. This is required to calculate the variance betwwen the current and previous {tbillTenure.Value} TBill");
                        }

                        var TbillImpliedYield = db.TBillImpliedYields
                          .FirstOrDefault(y => y.TBillId == currentTBill.Id && y.Date == confirmImpliedYieldDTO.YieldDate);

                        if (TbillImpliedYield == null)
                        {
                            var TBillImpliedYieldToSave = new TBillImpliedYield
                            {
                                Date = confirmImpliedYieldDTO.YieldDate,
                                Yield = currentTBill.Yield,
                                TBillId = currentTBill.Id,
                                Tenor = currentTBill.Tenor,
                            };
                            db.TBillImpliedYields.Add(TBillImpliedYieldToSave);
                        }
                        else
                        {
                            TbillImpliedYield.Yield = currentTBill.Yield;
                            db.Entry(TbillImpliedYield).State = EntityState.Modified;
                        }

                    }

                    foreach (var existingDraft in existingDraftImpliedYield)
                    {
                        var bondDetails = bondsNotMatured.Where(b => b.Id == existingDraft.BondId).FirstOrDefault();
                        if (bondDetails == null)
                        {
                            continue;
                            // return BadRequest($"Bond with Id {existingDraft.BondId} does not exist or has matured");
                        }
                        double YieldToSave = existingDraft.Yield;

                        var existingImpliedYield = impliedYields.Where(i => i.YieldDate.Date == confirmImpliedYieldDTO.YieldDate.Date && i.BondId == bondDetails.Id).FirstOrDefault();
                        if (existingImpliedYield != null)
                        {
                            existingImpliedYield.Yield = YieldToSave;
                            db.Entry(existingImpliedYield).State = EntityState.Modified;
                        }
                        else
                        {
                            var impliedYieldToSave = new ImpliedYield
                            {
                                BondId = bondDetails.Id,
                                Yield = YieldToSave,
                                YieldDate = existingDraft.YieldDate
                            };
                            db.ImpliedYields.Add(impliedYieldToSave);
                        }
                    }

                    db.SaveChanges();

                    return Ok();

                }
            }
            catch (Exception Ex)
            {
                UtilityService.LogException(Ex);
                return StatusCode(500, UtilityService.HandleException(Ex));

            }
        }

        // returns all Implied Yields given a date dafault value is default(DateTime)
        [HttpGet]
        [Route("GetImpliedYields")]
        [Authorize(AuthenticationSchemes = "Bearer")]

        public ActionResult<IEnumerable<ImpliedYieldDTO>> GetImpliedYields(string? For = "default")
        {
            var parsedDate = DateTime.Now;
            var impliedYieldDTOs = new List<ImpliedYieldDTO>();

            try
            {
                using (var db = new QuotationsBoardContext())
                {
                    if (For == "default" || For == null || string.IsNullOrWhiteSpace(For))
                    {
                        parsedDate = DateTime.Now;
                    }
                    else
                    {
                        string[] formats = { "dd/MM/yyyy", "yyyy-MM-dd", "MM/dd/yyyy", "dd-MM-yyyy", "dd/MM/yyyy HH:mm:ss", "yyyy-MM-dd HH:mm:ss", "MM/dd/yyyy HH:mm:ss", "dd-MM-yyyy HH:mm:ss" };
                        DateTime targetTradeDate;
                        bool success = DateTime.TryParseExact(For, formats, CultureInfo.InvariantCulture, DateTimeStyles.None, out targetTradeDate);
                        if (!success)
                        {
                            return BadRequest("The date format is invalid");
                        }
                        parsedDate = targetTradeDate.Date;
                    }

                    var impliedYields = db.ImpliedYields.ToList();
                    var bonds = db.Bonds.ToList();
                    var tBills = db.TBills.ToList();
                    var tBillsNotMatured = tBills.Where(t => t.MaturityDate.Date > DateTime.Now.Date).ToList();
                    var bondsNotMatured = bonds.Where(b => b.MaturityDate.Date > DateTime.Now.Date).ToList();

                    foreach (var bond in bondsNotMatured)
                    {
                        var bondImpliedYield = impliedYields.Where(i => i.BondId == bond.Id && i.YieldDate.Date == parsedDate.Date).FirstOrDefault();
                        if (bondImpliedYield == null)
                        {
                            continue;
                        }
                        var diff = bond.MaturityDate.Date - DateTime.Now.Date;
                        var yearsToMaturity = diff.TotalDays / 364;
                        var impliedYieldDTO = new ImpliedYieldDTO
                        {
                            BondName = bond.IssueNumber,
                            YearsToMaturity = yearsToMaturity,
                            Yield = Math.Round(bondImpliedYield.Yield, 4, MidpointRounding.AwayFromZero),
                            YieldDate = bondImpliedYield.YieldDate,
                        };
                        impliedYieldDTOs.Add(impliedYieldDTO);
                    }
                    return Ok(impliedYieldDTOs);
                }

            }
            catch (Exception Ex)
            {
                UtilityService.LogException(Ex);
                return StatusCode(500, UtilityService.HandleException(Ex));

            }

        }

        // Yield Curve
        [HttpGet]
        [Route("GetYieldCurve")]
        [Authorize(AuthenticationSchemes = "Bearer")]
        public ActionResult<IEnumerable<FinalYieldCurveData>> GetYieldCurve(string? For = "default")
        {
            // var parsedDate = DateTime.Now;

            using (var _db = new QuotationsBoardContext())
            {

                var parsedDate = QuotationsHelper.ParseDate(For);
                // IS PARSED DATE VALID?
                if (parsedDate == DateTime.MinValue)
                {
                    return BadRequest("The date format is invalid");
                }

                Dictionary<int, (double, double)> benchmarkRanges = YieldCurveHelper.GetBenchmarkRanges(parsedDate);
                Dictionary<int, bool> benchmarksFound = new Dictionary<int, bool>();
                List<int> benchMarkTenorsForYiedCurve = new List<int> { 2, 5, 10, 15, 20, 25 };
                HashSet<double> tenuresThatRequireInterPolation = new HashSet<double>();
                HashSet<double> tenuresThatDoNotRequireInterpolation = new HashSet<double>();
                HashSet<string> usedBondIds = new HashSet<string>();
                List<FinalYieldCurveData> YieldCurveToPlot = new List<FinalYieldCurveData>();
                List<YieldCurveDataSet> yieldCurveCalculations = new List<YieldCurveDataSet>();


                YieldCurveHelper.AddOneYearTBillToYieldCurve(parsedDate, tenuresThatDoNotRequireInterpolation, yieldCurveCalculations);

                YieldCurveToPlot = YieldCurveHelper.ProcessYieldCurve(parsedDate, _db, yieldCurveCalculations, benchmarkRanges, tenuresThatRequireInterPolation, tenuresThatDoNotRequireInterpolation, usedBondIds);
                return Ok(YieldCurveToPlot);

            }

        }




    }
}
