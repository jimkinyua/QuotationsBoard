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
                        Yield = decimal.Parse(yieldValue),
                        YieldDate = DateTime.Parse(yieldDate)
                    };

                    impliedYields.Add(impliedYield);
                }


            }

            return impliedYields;

        }

        // Calculate Implied Yield for each Bond
        [HttpGet]
        [Route("CalculateImpliedYield")]
        [Authorize(Roles = CustomRoles.SuperAdmin, AuthenticationSchemes = "Bearer")]
        public ActionResult<IEnumerable<ComputedImpliedYield>> CalculateImpliedYield()
        {
            try
            {
                using (var db = new QuotationsBoardContext())
                {
                    var DateInQuestion = DateTime.Now.Date;
                    // var DateInQuestion = DateInQuestion_.AddDays(-1);
                    var LastWeek = DateInQuestion.AddDays(-7);

                    DateTime startOfLastWeek = LastWeek.AddDays(-(int)LastWeek.DayOfWeek + (int)DayOfWeek.Monday);
                    DateTime endOfLastWeek = LastWeek.AddDays(+(int)LastWeek.DayOfWeek + (int)DayOfWeek.Sunday);

                    List<ComputedImpliedYield> computedImpliedYields = new List<ComputedImpliedYield>();
                    var bonds = db.Bonds.ToList();
                    var TBills = db.TBills.ToList();
                    var bondsNotMatured = bonds.Where(b => b.MaturityDate.Date > DateTime.Now.Date).ToList();
                    var tBillsNotMature = TBills.Where(t => t.MaturityDate.Date > DateTime.Now.Date).ToList();

                    var LastWeeksTBill = tBillsNotMature
                        .Where(t => t.IssueDate.Date >= startOfLastWeek.Date
                                    && t.IssueDate.Date <= endOfLastWeek.Date
                                    && t.Tenor >= 364)
                        .ToList();

                    DateTime startOfLastWeekButOne = LastWeek.AddDays(-7).AddDays(-(int)LastWeek.DayOfWeek + (int)DayOfWeek.Monday);
                    DateTime endOfLastWeekButOne = LastWeek.AddDays(-7).AddDays(+(int)LastWeek.DayOfWeek + (int)DayOfWeek.Sunday);

                    var lastWeekButOneTBill = tBillsNotMature
                        .Where(t => t.IssueDate.Date >= startOfLastWeekButOne.Date
                                    && t.IssueDate.Date <= endOfLastWeekButOne.Date
                                    && t.Tenor >= 364)
                        .ToList();

                    decimal AllowedMarginOfError = 1M;
                    var oneYearTBillForLastWeek = LastWeeksTBill.Where(t => t.Tenor >= 364).FirstOrDefault();
                    var oneYearTBillForLastWeekButOne = lastWeekButOneTBill.Where(t => t.Tenor >= 364).FirstOrDefault();
                    if (oneYearTBillForLastWeekButOne == null)
                    {
                        return BadRequest("No 1 Year TBill for Last Week But One");
                    }
                    if (oneYearTBillForLastWeek == null)
                    {
                        return BadRequest("No 1 Year TBill for Last Week");
                    }

                    foreach (var bond in bondsNotMatured)
                    {
                        var bondTradeLines = GetBondTradeLinesForBond(bond.Id, DateInQuestion);
                        var quotations = GetQuotationsForBond(bond.Id, DateInQuestion);
                        var averageWeightedTradedYield = CalculateAverageWeightedTradedYield(bondTradeLines);
                        var averageWeightedQuotedYield = CalculateAverageWeightedQuotedYield(quotations);

                        var previousImpliedYield = db.ImpliedYields
                        .Where(i => i.BondId == bond.Id
                            && i.YieldDate.Date < DateInQuestion.Date)
                        .OrderByDescending(i => i.YieldDate)
                        .FirstOrDefault();

                        // var previousImpliedYield = db.ImpliedYields
                        // .Where(
                        //         i => i.BondId == bond.Id
                        //         && i.YieldDate.Date >= startOfLastWeekButOne.Date
                        //         && i.YieldDate.Date <= endOfLastWeekButOne.Date)
                        // .OrderByDescending(i => i.YieldDate).FirstOrDefault();
                        decimal _preImpYield = 0;
                        if (previousImpliedYield == null)
                        {
                            continue;
                            // return BadRequest($"No Previous Implied Yield for Bond {bond.IssueNumber}");
                        }
                        else
                        {
                            _preImpYield = previousImpliedYield.Yield;
                        }
                        var QuotedAndPrevious = averageWeightedQuotedYield - _preImpYield; //previousImpliedYield.Yield;
                        var TradedAndPrevious = averageWeightedTradedYield - _preImpYield; //previousImpliedYield.Yield;
                        var VarianceinTBills = oneYearTBillForLastWeek.Yield - oneYearTBillForLastWeekButOne.Yield;

                        bool isQuotedWithinMargin = IsWithinMargin(averageWeightedQuotedYield, _preImpYield, AllowedMarginOfError);
                        bool isTradedWithinMargin = IsWithinMargin(averageWeightedTradedYield, _preImpYield, AllowedMarginOfError);

                        decimal impliedYield;
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
                            reasonForSelection = $"Selected Traded yield ({averageWeightedTradedYield}%). AveraQuoted yield is  {averageWeightedQuotedYield}%, Previous Implied Yield is {_preImpYield}%)";
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

        private bool IsWithinMargin(decimal value, decimal previousYiedld, decimal maxAllowwdDiffrence)
        {
            var diffrence = Math.Abs(value - previousYiedld);
            if (diffrence <= maxAllowwdDiffrence)
            {
                return true;
            }
            return false;
        }

        private decimal DetermineClosestYield(decimal quoted, decimal traded, decimal variance)
        {
            return Math.Abs(quoted - variance) < Math.Abs(traded - variance) ? quoted : traded;
        }

        // fetehces all Quotations for a Bond (Private)
        private List<Quotation> GetQuotationsForBond(string bondId, DateTime filterDate)
        {
            using (var db = new QuotationsBoardContext())
            {
                var quotations = db.Quotations.Where(q => q.BondId == bondId && q.CreatedAt.Date == filterDate.Date).ToList();
                return quotations;
            }
        }

        private List<BondTradeLine> GetBondTradeLinesForBond(string bondId, DateTime filterDate)
        {
            using (var db = new QuotationsBoardContext())
            {
                var bondTradeLines = db.BondTradeLines
                .Include(b => b.BondTrade)
                .Where(b => b.BondId == bondId && b.BondTrade.TradeDate == filterDate.Date).ToList();
                return bondTradeLines;
            }
        }

        // Calculates the Average Weighted Traded Yield for a Bond
        private decimal CalculateAverageWeightedTradedYield(List<BondTradeLine> bondTradeLines)
        {
            decimal averageWeightedTradedYield = 0;
            decimal totalWeightedBuyYield = bondTradeLines.Where(x => x.Side == "BUY" && x.ExecutedSize >= 50000000).Sum(x => x.Yield * x.ExecutedSize);
            decimal totalWeightedSellYield = bondTradeLines.Where(x => x.Side == "SELL" && x.ExecutedSize >= 50000000).Sum(x => x.Yield * x.ExecutedSize);
            decimal totalBuyVolume = bondTradeLines.Where(x => x.Side == "BUY" && x.ExecutedSize >= 50000000).Sum(x => x.ExecutedSize);
            decimal totalSellVolume = bondTradeLines.Where(x => x.Side == "SELL" && x.ExecutedSize >= 50000000).Sum(x => x.ExecutedSize);

            decimal averageBuyYield = totalBuyVolume > 0 ? totalWeightedBuyYield / totalBuyVolume : 0;
            decimal averageSellYield = totalSellVolume > 0 ? totalWeightedSellYield / totalSellVolume : 0;

            if (totalBuyVolume > 0 || totalSellVolume > 0)
            {
                averageWeightedTradedYield = (averageBuyYield + averageSellYield) / 2;
            }

            return averageWeightedTradedYield;
        }

        private decimal CalculateAverageWeightedQuotedYield(List<Quotation> quotations)
        {
            decimal averageWeightedQuotedYield = 0;
            decimal totalWeightedBuyYield = quotations.Where(x => x.BuyVolume >= 50000000).Sum(x => x.BuyingYield * x.BuyVolume);
            decimal totalWeightedSellYield = quotations.Where(x => x.SellVolume >= 50000000).Sum(x => x.SellingYield * x.SellVolume);
            decimal totalBuyVolume = quotations.Where(x => x.BuyVolume >= 50000000).Sum(x => x.BuyVolume);
            decimal totalSellVolume = quotations.Where(x => x.SellVolume >= 50000000).Sum(x => x.SellVolume);

            decimal averageBuyYield = totalBuyVolume > 0 ? totalWeightedBuyYield / totalBuyVolume : 0;
            decimal averageSellYield = totalSellVolume > 0 ? totalWeightedSellYield / totalSellVolume : 0;

            if (totalBuyVolume > 0 || totalSellVolume > 0)
            {
                averageWeightedQuotedYield = (averageBuyYield + averageSellYield) / 2;
            }

            return averageWeightedQuotedYield;
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

            try
            {
                using (var db = new QuotationsBoardContext())
                {
                    var bonds = db.Bonds.ToList();
                    var impliedYields = db.ImpliedYields.ToList();
                    var bondsNotMatured = bonds.Where(b => b.MaturityDate.Date > DateTime.Now.Date).ToList();

                    // Enusure that no Imlpied Yiled for today  exists in th eImpliedYield Table
                    var existingImpliedYield = impliedYields.Where(i => i.YieldDate.Date == DateTime.Now.Date).ToList();
                    if (existingImpliedYield.Any())
                    {
                        return BadRequest("Implied Yield for today already exists");
                    }

                    foreach (var impliedYield in confirmImpliedYieldDTO.ImpliedYields)
                    {
                        var bondDetails = bondsNotMatured.Where(b => b.IssueNumber == impliedYield.BondId).FirstOrDefault();
                        if (bondDetails == null)
                        {
                            return BadRequest($"Bond with Id {impliedYield.BondId} does not exist or has matured");
                        }
                        decimal YieldToSave = 0;
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
                            return BadRequest("Invalid Selected Yield");
                        }

                        var impliedYieldToSave = new ImpliedYield
                        {
                            BondId = bondDetails.Id,
                            Yield = YieldToSave,
                            YieldDate = impliedYield.YieldDate
                        };

                        db.ImpliedYields.Add(impliedYieldToSave);
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
                            Yield = bondImpliedYield.Yield,
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
        public ActionResult<IEnumerable<YieldCurveDTO>> GetYieldCurve(string? For = "default")
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

                var LastWeek = parsedDate.AddDays(-7);
                DateTime startOfLastWeek = LastWeek.AddDays(-(int)LastWeek.DayOfWeek + (int)DayOfWeek.Monday);
                DateTime endOfLastWeek = LastWeek.AddDays(+(int)LastWeek.DayOfWeek + (int)DayOfWeek.Sunday);
                // Fetch all Bonds  under FXD Category that are not matured
                var fXdBonds = _db.Bonds.Where(b => b.BondCategory == "FXD" && b.MaturityDate.Date > parsedDate.Date).ToList();
                // var fXdBonds = _db.Bonds.Where(b => b.MaturityDate.Date > DateTime.Now.Date).ToList();

                var currentOneYearTBill = _db.TBills
                .Where(t => t.Tenor >= 364
                && t.IssueDate.Date >= startOfLastWeek.Date
                && t.IssueDate.Date <= endOfLastWeek.Date
                )
                .OrderByDescending(t => t.IssueDate)
                .FirstOrDefault();

                if (currentOneYearTBill == null)
                {
                    return BadRequest("It Seems there is no 1 Year TBill for the last week");
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
                HashSet<string> usedBondIds = new HashSet<string>();

                // for each benchmark range, fetch the bond that is closest to the benchmark range
                List<YieldCurve> yieldCurves = new List<YieldCurve>();
                // tadd the 1 year TBill to the yield curve
                yieldCurves.Add(new YieldCurve
                {
                    BenchMarkTenor = 1,
                    Yield = Math.Round(currentOneYearTBill.Yield, 4, MidpointRounding.AwayFromZero),
                    CanBeUsedForYieldCurve = true,
                    BondUsed = "1 Year TBill",
                    IssueDate = currentOneYearTBill.IssueDate,
                    MaturityDate = currentOneYearTBill.MaturityDate,
                    BenchMarkFound = true,
                });

                foreach (var benchmark in benchmarkRanges)
                {
                    Bond? BondWithExactTenure = null;
                    //var closestBond = GetClosestBond(fXdBonds, benchmark, usedBondIds, parsedDate);
                    var bondsWithinThisTenure = YieldCurveHelper.GetBondsInTenorRange(fXdBonds, benchmark, usedBondIds, parsedDate);

                    if (bondsWithinThisTenure.Count() == 0)
                    {
                        yieldCurves.Add(new YieldCurve
                        {
                            BenchMarkTenor = benchmark.Key,
                            BenchMarkFound = false,
                            Yield = 0,
                        });
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
                            return BadRequest($"The Bond {BondWithExactTenure.IssueNumber} seems not to have an Implied Yield.");
                        }
                        yieldCurves.Add(new YieldCurve
                        {
                            BenchMarkTenor = benchmark.Key,
                            Yield = Math.Round(impliedYield.Yield, 4, MidpointRounding.AwayFromZero),
                            BondUsed = BondWithExactTenure.IssueNumber,
                            IssueDate = BondWithExactTenure.IssueDate,
                            MaturityDate = BondWithExactTenure.MaturityDate,
                            Coupon = BondWithExactTenure.CouponRate,
                            BenchMarkFound = true,
                        });
                        usedBondIds.Add(BondWithExactTenure.Id);
                    }
                    else
                    {
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
                                return BadRequest($"The Bond {bond.IssueNumber} seems not to have an Implied Yield. This is required for Yield Curve Calculation especiliy for interpolation");
                            }
                            yieldCurves.Add(new YieldCurve
                            {
                                BenchMarkTenor = benchmark.Key,
                                Yield = Math.Round(impliedYield.Yield, 4, MidpointRounding.AwayFromZero),
                                BondUsed = bond.IssueNumber,
                                IssueDate = bond.IssueDate,
                                MaturityDate = bond.MaturityDate,
                                Coupon = bond.CouponRate,
                                BenchMarkFound = true,
                            });
                            usedBondIds.Add(bond.Id);
                        }
                    }

                }


                // interpolate the yield curve
                var interpolatedYieldCurve = YieldCurveHelper.InterpolateMissingYields(yieldCurves);

                return Ok(interpolatedYieldCurve);

            }

        }

        private double CalculateMaturityScore(Bond bond, double upperBound)
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


        private Bond? GetClosestBondL(IEnumerable<Bond> bonds, double lowerBound, double upperBound)
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
        private Bond? GetClosestBond(IEnumerable<Bond> bonds, KeyValuePair<int, (double, double)> benchmark, HashSet<string> usedBondIds, DateTime dateInQuestion)
        {
            // Define the benchmark range
            var lowerBound = benchmark.Value.Item1;
            var upperBound = benchmark.Value.Item2;
            var midpoint = (lowerBound + upperBound) / 2;


            List<(Bond bond, double difference, decimal OutstandingValue)> bondComparisons = new List<(Bond, double, decimal)>();
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






        private int SelectBenchmarkForBondBasedOnRTM(double RemainingTimeToMaturityForBond, Dictionary<int, (double, double)> benchmarkRanges)
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

    }
}
