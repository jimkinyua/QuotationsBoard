﻿using System.Globalization;
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
            /*if (UploadFile.ContentType != "text/csv")
            {
                return BadRequest("The file is not an excel file");
            }*/
            DateTime TargetTradeDate = DateTime.ParseExact(uploadTradedBondValue.TradeDate, "dd/MM/yyyy", CultureInfo.InvariantCulture);
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
                                TargetDate = TargetTradeDate,
                                UploadedBy = "Admin"
                            };
                            db.GorvermentBondTradeStages.Add(gorvermentBondTradeStage);
                            var trades = ReadExcelData(sheetWhereDataIsLocated, gorvermentBondTradeStage);
                            await db.GorvermentBondTradeLinesStage.AddRangeAsync(trades);
                            await db.SaveChangesAsync();

                            // fetch details about the trades
                            var tradesDetails = await db.GorvermentBondTradeStages
                                .Where(t => t.Id == gorvermentBondTradeStage.Id)
                                .Include(t => t.GorvermentBondTradeLineStage)
                                .FirstOrDefaultAsync();
                            // construc to UploadedBondTrade DTO
                            var tradesDetailsDTO = new UploadedBondTrade
                            {
                                Id = tradesDetails.Id,
                                UploadedAt = tradesDetails.TargetDate,
                                UploadedBy = tradesDetails.UploadedBy,
                                UploadedBondTradeLineDTO = new List<UploadedBondTradeLineDTO>()
                            };
                            foreach (var trade in tradesDetails.GorvermentBondTradeLineStage)
                            {
                                var tradeDTO = new UploadedBondTradeLineDTO
                                {
                                    Id = trade.Id,
                                    GorvermentBondTradeStageId = trade.GorvermentBondTradeStageId,
                                    Side = trade.Side,
                                    SecurityId = trade.SecurityId,
                                    ExecutedSize = trade.ExecutedSize,
                                    ExcecutedPrice = trade.ExcecutedPrice,
                                    ExecutionID = trade.ExecutionID,
                                    TransactionTime = trade.TransactionTime,
                                    DirtyPrice = trade.DirtyPrice,
                                    Yield = trade.Yield,
                                    TradeDate = trade.TradeDate
                                };
                                tradesDetailsDTO.UploadedBondTradeLineDTO.Add(tradeDTO);
                            }
                            return Ok(tradesDetailsDTO);
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

        // Allows user to confirm the trdes uploaded are correct so that we can move them to the main table
        [HttpPost]
        [Route("ConfirmTradedBondsValues")]
        public async Task<IActionResult> ConfirmTradedBondsValues([FromBody] ConfirmTradedBondValue confirmTradedBondValue)
        {
            if (!ModelState.IsValid)
            {
                return BadRequest(ModelState);
            }

            try
            {
                using (var db = new QuotationsBoardContext())
                {
                    db.Database.EnsureCreated();
                    var tradesDetails = await db.GorvermentBondTradeStages
                        .Where(t => t.Id == confirmTradedBondValue.Id)
                        .Include(t => t.GorvermentBondTradeLineStage)
                        .FirstOrDefaultAsync();
                    if (tradesDetails == null)
                    {
                        return BadRequest("The trades you are trying to confirm do not exist");
                    }
                    var targetedDate = tradesDetails.TargetDate;
                    // Any Trades that been uploaded for this date? Only one should be uploaded
                    var trades = await db.BondTrades.Where(t => t.TradeDate == targetedDate).ToListAsync();
                    if (trades.Any())
                    {
                        return BadRequest("The trades for this date have already been uploaded");
                    }

                    // create a new bond trade
                    BondTrade bondTrade = new BondTrade
                    {
                        UploadedBy = "Admin",
                        UploadedOn = DateTime.Now,
                        TradeDate = targetedDate
                    };

                    db.BondTrades.Add(bondTrade);

                    // Loop the GorvermentBondTradeLineStage if any and add them to the BondTradeLine
                    foreach (var trade in tradesDetails.GorvermentBondTradeLineStage)
                    {
                        // ensure the bond exists
                        var TransformedSecId = TransformSecurityId(trade.SecurityId);
                        var bond = await db.Bonds.Where(b => b.IssueNumber == TransformedSecId).FirstOrDefaultAsync();
                        if (bond == null)
                        {
                            return BadRequest($"The bond with security id {trade.SecurityId} does not exist");
                        }
                        BondTradeLine bondTradeLine = new BondTradeLine
                        {
                            BondTradeId = bondTrade.Id,
                            Side = trade.Side,
                            SecurityId = trade.SecurityId,
                            ExecutedSize = trade.ExecutedSize,
                            ExcecutedPrice = trade.ExcecutedPrice,
                            ExecutionID = trade.ExecutionID,
                            TransactionTime = trade.TransactionTime,
                            DirtyPrice = trade.DirtyPrice,
                            Yield = trade.Yield,
                            BondId = bond.Id
                        };
                        db.BondTradeLines.Add(bondTradeLine);
                    }

                    await db.SaveChangesAsync();

                    return Ok(bondTrade);
                }
            }
            catch (Exception Ex)
            {
                UtilityService.LogException(Ex);
                return StatusCode(500, UtilityService.HandleException(Ex));

            }
        }

        private string TransformSecurityId(string excelSecurityId)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(excelSecurityId))
                    throw new ArgumentException("Excel Security ID is null or whitespace.");

                // Excel format is "PREFIX.BondCode/Year/Year"
                // Remove the prefix up to the first dot (.)
                var parts = excelSecurityId.Split(new char[] { '.' }, 2);
                if (parts.Length < 2)
                    throw new FormatException("Excel Security ID does not contain a valid format with a dot separator.");

                string transformedId = parts[1]; // Take the second part

                // Split the second part into segments
                var segments = transformedId.Split('/');
                if (segments.Length < 3)
                    throw new FormatException("Excel Security ID does not contain enough segments after the dot separator.");

                // Determine if the rate is a whole number or a decimal
                if (decimal.TryParse(segments[2], out decimal rate))
                {
                    // Format the rate with no leading zeros and two decimal places if it's not a whole number
                    string rateWithYear = rate % 1 == 0 ? $"{rate:0}" : $"{rate:0.0}";

                    // Reconstruct the transformed ID
                    transformedId = $"{segments[0]}/{segments[1]}/{rateWithYear}Yr";
                }
                else
                {
                    throw new FormatException("The rate segment is not a valid decimal number.");
                }

                // // Check if the last segment ends with a digit, then append "Yr"
                // var lastSegmentParts = transformedId.Split(new char[] { '/' });
                // if (lastSegmentParts.Length >= 2 && char.IsDigit(lastSegmentParts.Last().Last()))
                // {
                //     transformedId += "Yr";
                // }

                return transformedId;
            }
            catch (Exception ex)
            {
                // Log the error, return null, or throw a custom exception as appropriate for your application's error handling policy
                // Example: Log to console or application logs
                Console.WriteLine($"Error transforming Security ID: {ex.Message}");
                return null; // or handle differently as per your requirements
            }
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

        private List<GorvermentBondTradeLineStage> ReadExcelData(IXLWorksheet worksheet, GorvermentBondTradeStage gorvermentBondTradeStage)
        {
            var trades = new List<GorvermentBondTradeLineStage>();
            int rowCount = CountNonEmptyRows(worksheet);
            int maxColumnCount = worksheet.ColumnsUsed().Count();

            for (int row = 2; row <= rowCount; row++)
            {
                // Check if the row is empty
                bool isEmptyRow = true;
                for (int col = 1; col <= maxColumnCount; col++)
                {
                    if (!string.IsNullOrWhiteSpace(worksheet.Cell(row, col).Value.ToString()))
                    {
                        isEmptyRow = false;
                        break;
                    }
                }

                if (isEmptyRow) continue; // Skip this row if it's empty

                // Assuming data is already validated and can be directly parsed
                var trade = new GorvermentBondTradeLineStage
                {
                    Side = worksheet.Cell(row, 2).Value.ToString(),
                    SecurityId = worksheet.Cell(row, 3).Value.ToString(),
                    ExecutedSize = decimal.Parse(worksheet.Cell(row, 4).Value.ToString()),
                    ExcecutedPrice = decimal.Parse(worksheet.Cell(row, 5).Value.ToString()),
                    ExecutionID = worksheet.Cell(row, 6).Value.ToString(),
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

                string excelSecurityId = worksheet.Cell(rowToBeginAt, 3).Value.ToString();
                string transformedSecurityId = TransformSecurityId(excelSecurityId);

                if (string.IsNullOrEmpty(transformedSecurityId))
                {
                    errors.Add($"Row {rowToBeginAt}: Security ID format is invalid.");
                    continue; // Skip further validation for this row
                }

                using (var dbContext = new QuotationsBoardContext())
                {
                    dbContext.Database.EnsureCreated();
                    // Check if transformedSecurityId exists in the database
                    var bondExists = dbContext.Bonds.Any(b => b.IssueNumber == transformedSecurityId);
                    if (!bondExists)
                    {
                        errors.Add($"Row {rowToBeginAt}: Security ID '{transformedSecurityId}' does not exist in the system.");
                    }
                }




                if (isEmptyRow) continue; // Skip this row if it's empty

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

                if (string.IsNullOrWhiteSpace(dirtySecurityID))
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
