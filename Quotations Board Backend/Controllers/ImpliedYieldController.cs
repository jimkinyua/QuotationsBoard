using ClosedXML.Excel;
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
                    // Ensure that no Implied Yield exists for this date
                    var impliedYieldExists = db.ImpliedYields.Any(i => i.YieldDate.Date == uploadImpliedYield.TargetDate.Date);
                    if (impliedYieldExists)
                    {
                        return BadRequest("Implied Yield already exists for this date");
                    }

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


            for (int rowToBeginAt = 2; rowToBeginAt <= rowCount; rowToBeginAt++)
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

                string excelIssueNo = worksheet.Cell(rowToBeginAt, 3).Value.ToString();

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


                if (!decimal.TryParse(yieldValue, out _))
                    errors.Add($"Row {rowToBeginAt} Cell D: 'Executed Size' is not a valid decimal number.");

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

                var bondId = worksheet.Cell(row, 1).Value.ToString();
                var yieldValue = worksheet.Cell(row, 2).Value.ToString();

                // Assuming data is already validated and can be directly parsed
                var impliedYield = new ImpliedYield
                {
                    BondId = bondId,
                    Yield = decimal.Parse(yieldValue),
                    YieldDate = DateTime.Now // Assuming you want to set the YieldDate to the current date/time
                };

                impliedYields.Add(impliedYield);
            }

            return impliedYields;

        }

        // Calculate Implied Yield for each Bond
        [HttpGet]
        [Route("CalculateImpliedYield")]
        public IActionResult CalculateImpliedYield()
        {
            try
            {
                using (var db = new QuotationsBoardContext())
                {
                    var DateInQuestion = DateTime.Now.Date;
                    var LastWeek = DateInQuestion.AddDays(-7);
                    List<ComputedImpliedYield> computedImpliedYields = new List<ComputedImpliedYield>();
                    var bonds = db.Bonds.ToList();
                    var TBills = db.TBills.ToList();
                    var bondsNotMatured = bonds.Where(b => b.MaturityDate.Date > DateTime.Now.Date).ToList();
                    var tBillsNotMature = TBills.Where(t => t.MaturityDate.Date > DateTime.Now.Date).ToList();
                    var LastWeeksTBill = tBillsNotMature.Where(t => t.MaturityDate.Date == LastWeek.Date).ToList();
                    var lastWeekButOneTBill = tBillsNotMature.Where(t => t.MaturityDate.Date == LastWeek.AddDays(-7).Date).ToList();
                    decimal AllowedMarginOfError = 0.05m;

                    foreach (var bond in bondsNotMatured)
                    {
                        var bondTradeLines = GetBondTradeLinesForBond(bond.Id, DateInQuestion);
                        var quotations = GetQuotationsForBond(bond.Id, DateInQuestion);
                        var averageWeightedTradedYield = CalculateAverageWeightedTradedYield(bondTradeLines);
                        var averageWeightedQuotedYield = CalculateAverageWeightedQuotedYield(quotations);
                        var previousImpliedYield = db.ImpliedYields.Where(i => i.BondId == bond.Id && i.YieldDate.Date == LastWeek.Date).FirstOrDefault();
                        if (previousImpliedYield == null)
                        {
                            continue;
                        }
                        var QuotedAndPrevious = averageWeightedQuotedYield - previousImpliedYield.Yield;
                        var TradedAndPrevious = averageWeightedTradedYield - previousImpliedYield.Yield;
                        var VarianceinTBills = LastWeeksTBill[0].Yield - lastWeekButOneTBill[0].Yield;

                        bool isQuotedWithinMargin = Math.Abs(QuotedAndPrevious - VarianceinTBills) <= AllowedMarginOfError;
                        bool isTradedWithinMargin = Math.Abs(TradedAndPrevious - VarianceinTBills) <= AllowedMarginOfError;

                        decimal impliedYield;

                        if (isQuotedWithinMargin && isTradedWithinMargin)
                        {
                            // If both are within margin, the tradedMargin takes precedence
                            impliedYield = averageWeightedTradedYield;
                        }
                        else if (isQuotedWithinMargin)
                        {
                            impliedYield = 0;
                        }


                    }

                    return Ok();
                }
            }
            catch (Exception Ex)
            {
                UtilityService.LogException(Ex);
                return StatusCode(500, UtilityService.HandleException(Ex));

            }
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
            decimal totalWeightedBuyYield = bondTradeLines.Where(x => x.Side == "BUY").Sum(x => x.Yield * x.ExecutedSize);
            decimal totalWeightedSellYield = bondTradeLines.Where(x => x.Side == "SELL").Sum(x => x.Yield * x.ExecutedSize);
            decimal totalBuyVolume = bondTradeLines.Where(x => x.Side == "BUY").Sum(x => x.ExecutedSize);
            decimal totalSellVolume = bondTradeLines.Where(x => x.Side == "SELL").Sum(x => x.ExecutedSize);
            decimal averageBuyYield = totalWeightedBuyYield / totalBuyVolume;
            decimal averageSellYield = totalWeightedSellYield / totalSellVolume;
            decimal totalExecutedSize = bondTradeLines.Sum(x => x.ExecutedSize);
            averageWeightedTradedYield = (averageBuyYield + averageSellYield) / 2;
            return averageWeightedTradedYield;
        }

        // calculate the Average Weighted Quoted Yield for a Bond
        private decimal CalculateAverageWeightedQuotedYield(List<Quotation> quotations)
        {
            decimal averageWeightedQuotedYield = 0;
            decimal totalWeightedBuyYield = quotations.Sum(x => x.BuyingYield * x.BuyVolume);
            decimal totalWeightedSellYield = quotations.Sum(x => x.SellingYield * x.SellVolume);
            decimal totalBuyVolume = quotations.Sum(x => x.BuyVolume);
            decimal totalSellVolume = quotations.Sum(x => x.SellVolume);
            decimal averageBuyYield = totalWeightedBuyYield / totalBuyVolume;
            decimal averageSellYield = totalWeightedSellYield / totalSellVolume;
            averageWeightedQuotedYield = (averageBuyYield + averageSellYield) / 2;
            return averageWeightedQuotedYield;
        }




    }
}
