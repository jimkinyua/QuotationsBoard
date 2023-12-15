using System.Globalization;
using ClosedXML.Excel;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Quotations_Board_Backend.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class TradedBondsController : ControllerBase
    {
        // handle upload of ecel file with the traded bonds
        [HttpPost]
        [Route("UploadTradedBondsValues")]
        public async Task<IActionResult> UploadTradedBondsValues([FromForm] UploadTradedBondValue uploadTradedBondValue)
        {
            if (!ModelState.IsValid)
            {
                return BadRequest(ModelState);
            }

            // check if the file is an excel file
            var UploadFile = uploadTradedBondValue.ExcelFile;
            if (UploadFile.ContentType != "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet")
            {
                return BadRequest("The file is not an excel file");
            }

            try
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

                        using (var db = new QuotationsBoardContext())
                        {
                            db.Database.EnsureCreated();
                            GorvermentBondTradeStage gorvermentBondTradeStage = new GorvermentBondTradeStage
                            {
                                UploadedAt = DateTime.Now,
                                UploadedBy = "Admin"
                            };
                            db.GorvermentBondTradeStages.Add(gorvermentBondTradeStage);
                            var trades = ReadExcelData(sheetWhereDataIsLocated, gorvermentBondTradeStage);
                            await db.GorvermentBondTradeLinesStage.AddRangeAsync(trades);
                            await db.SaveChangesAsync();

                            // fetch details about the trades
                            var tradesDetails = db.GorvermentBondTradeStages
                                .Where(t => t.Id == gorvermentBondTradeStage.Id)
                                .Include(t => t.GorvermentBondTradeLineStage)
                               .ToList();

                            return Ok(tradesDetails);

                        }




                    }

                }
            }
            catch (System.Exception)
            {

                throw;
            }


        }

        private List<GorvermentBondTradeLineStage> ReadExcelData(IXLWorksheet worksheet, GorvermentBondTradeStage gorvermentBondTradeStage)
        {
            var trades = new List<GorvermentBondTradeLineStage>();
            int rowCount = worksheet.RowCount();

            for (int row = 2; row <= rowCount; row++)
            {
                // Assuming data is already validated and can be directly parsed
                var trade = new GorvermentBondTradeLineStage
                {
                    Side = worksheet.Cell(row, 2).Value.ToString(),
                    SecurityId = worksheet.Cell(row, 3).Value.ToString(),
                    ExecutedSize = decimal.Parse(worksheet.Cell(row, 4).Value.ToString()),
                    ExcecutedPrice = decimal.Parse(worksheet.Cell(row, 5).Value.ToString()),
                    TransactionTime = DateTime.Parse(worksheet.Cell(row, 7).Value.ToString()),
                    DirtyPrice = decimal.Parse(worksheet.Cell(row, 8).Value.ToString()),
                    Yield = decimal.Parse(worksheet.Cell(row, 9).Value.ToString()),
                    TradeDate = DateTime.Parse(worksheet.Cell(row, 13).Value.ToString()),
                    GorvermentBondTradeStageId = gorvermentBondTradeStage.Id
                };
                trades.Add(trade);
            }

            return trades;
        }

        private List<string> ValidateExcelData(IXLWorksheet worksheet)
        {
            var errors = new List<string>();
            int rowCount = worksheet.RowCount();

            for (int rowToBeginAt = 2; rowToBeginAt <= rowCount; rowToBeginAt++)
            {
                var board = worksheet.Cell(rowToBeginAt, 1).Value.ToString();
                var side = worksheet.Cell(rowToBeginAt, 2).Value.ToString();
                var dirtySecurityID = worksheet.Cell(rowToBeginAt, 3).Value.ToString();
                var executedSize = worksheet.Cell(rowToBeginAt, 4).Value.ToString();
                var executedPrice = worksheet.Cell(rowToBeginAt, 5).Value.ToString();
                var transactionTime = worksheet.Cell(rowToBeginAt, 7).Value.ToString();
                var dirtyPrice = worksheet.Cell(rowToBeginAt, 8).Value.ToString();
                var yield = worksheet.Cell(rowToBeginAt, 9).Value.ToString();
                var tradeDate = worksheet.Cell(rowToBeginAt, 13).Value.ToString();

                // Example validations:
                if (string.IsNullOrWhiteSpace(board))
                    errors.Add($"Row {rowToBeginAt} Cell A: 'Board' is required.");

                if (string.IsNullOrWhiteSpace(side))
                    errors.Add($"Row {rowToBeginAt} Cell B: 'Side' is required.");

                if (string.IsNullOrWhiteSpace(dirtySecurityID) || !long.TryParse(dirtySecurityID, out _))
                    errors.Add($"Row {rowToBeginAt} Cell C: 'Dirty Security ID' is invalid or missing.");

                if (!decimal.TryParse(executedSize, out _))
                    errors.Add($"Row {rowToBeginAt} Cell D: 'Executed Size' is not a valid decimal number.");

                if (!decimal.TryParse(executedPrice, out _))
                    errors.Add($"Row {rowToBeginAt} Cell E: 'Executed Price' is not a valid decimal number.");

                if (!DateTime.TryParse(transactionTime, out _))
                    errors.Add($"Row {rowToBeginAt} Cell G: 'Transaction Time' is not a valid date/time.");

                if (!decimal.TryParse(dirtyPrice, out _))
                    errors.Add($"Row {rowToBeginAt} Cell H: 'Dirty Price' is not a valid decimal number.");

                if (!decimal.TryParse(yield, out _))
                    errors.Add($"Row {rowToBeginAt} Cell I: 'Yield' is not a valid decimal number.");

                if (!DateTime.TryParse(tradeDate, out _))
                    errors.Add($"Row {rowToBeginAt} Cell M: 'Trade Date' is not a valid date.");
            }

            return errors;
        }

    }
}
