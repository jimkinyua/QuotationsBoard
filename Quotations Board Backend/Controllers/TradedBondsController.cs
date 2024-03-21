using System.Globalization;
using ClosedXML.Excel;
using DocumentFormat.OpenXml.Bibliography;
using Microsoft.AspNetCore.Authorization;
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
        [Authorize(Roles = CustomRoles.SuperAdmin, AuthenticationSchemes = "Bearer")]

        public async Task<ActionResult<Dictionary<string, List<UploadedTrade>>>> UploadTradedBondsValues([FromForm] UploadTradedBondValue uploadTradedBondValue)
        {
            if (!ModelState.IsValid)
            {
                return BadRequest(ModelState);
            }

            // check if the file is an excel file
            var UploadFile = uploadTradedBondValue.ExcelFile;
            // Dictionary to store new GorvermentBondTradeLineStages grouped by GorvermentBondTradeStageId
            var newTradeLineStages = new Dictionary<string, List<UploadedTrade>>();

            /*if (UploadFile.ContentType != "text/csv")
            {
                return BadRequest("The file is not an excel file");
            }*/
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
                            List<UploadedTrade> trades = ReadExcelData(sheetWhereDataIsLocated);
                            // Group By Trades by TransactionTime Date
                            var groupedTrades = trades.GroupBy(x => x.TradeDate.Date);
                            foreach (var tradeGroup in groupedTrades)
                            {
                                // Check if GorvermentBondTradeStage with the TargetDate already exists
                                var existingGorvermentBondTradeStage = db.GorvermentBondTradeStages
                                    .FirstOrDefault(g => g.TargetDate == tradeGroup.Key);

                                if (existingGorvermentBondTradeStage == null)
                                {
                                    existingGorvermentBondTradeStage = new GorvermentBondTradeStage
                                    {
                                        TargetDate = tradeGroup.Key,
                                        UploadedBy = "Admin"
                                    };
                                    db.GorvermentBondTradeStages.Add(existingGorvermentBondTradeStage);
                                }

                                foreach (var _trade in tradeGroup)
                                {
                                    GorvermentBondTradeLineStage gorvermentBondTradeLineStage = new GorvermentBondTradeLineStage
                                    {
                                        GorvermentBondTradeStageId = existingGorvermentBondTradeStage.Id,
                                        Side = _trade.Side,
                                        SecurityId = _trade.SecurityId,
                                        ExecutedSize = _trade.ExecutedSize,
                                        ExcecutedPrice = _trade.ExecutedPrice,
                                        ExecutionID = _trade.ExecutionID,
                                        TransactionTime = _trade.TradeDate,
                                        DirtyPrice = _trade.DirtyPrice,
                                        Yield = _trade.Yield,
                                        TradeDate = existingGorvermentBondTradeStage.TargetDate,
                                        TransactionID = _trade.TradeReportID
                                    };
                                    db.GorvermentBondTradeLinesStage.Add(gorvermentBondTradeLineStage);

                                    // Add to newTradeLineStages
                                    if (!newTradeLineStages.ContainsKey(existingGorvermentBondTradeStage.Id))
                                    {
                                        newTradeLineStages[existingGorvermentBondTradeStage.Id] = new List<UploadedTrade>();
                                    }
                                    newTradeLineStages[existingGorvermentBondTradeStage.Id].Add(_trade);
                                }
                            }
                            await db.SaveChangesAsync();
                            return Ok(newTradeLineStages);
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
        [Authorize(Roles = CustomRoles.SuperAdmin, AuthenticationSchemes = "Bearer")]

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

                    // Loop through the Ids and confirm the trades
                    foreach (var _GorvermentBondTradeStageId in confirmTradedBondValue.Id)
                    {
                        var tradesDetails = await db.GorvermentBondTradeStages
                            .Where(t => t.Id == _GorvermentBondTradeStageId)
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
                                BondId = bond.Id,
                                TransactionID = trade.TransactionID
                            };
                            db.BondTradeLines.Add(bondTradeLine);
                        }
                        await db.SaveChangesAsync();
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

        // Allows user to add a a single trade to the main table without having to upload an excel file
        [HttpPost]
        [Route("AddSingleTrade")]
        [Authorize(Roles = CustomRoles.SuperAdmin, AuthenticationSchemes = "Bearer")]

        public async Task<IActionResult> AddSingleTrade([FromBody] AddSingleTrade addSingleTrade)
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
                    var targetedDate = addSingleTrade.TradeDate;
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
                    foreach (var trade in addSingleTrade.Trades)
                    {
                        // ensure the bond exists
                        var bond = await db.Bonds.Where(b => b.IssueNumber == trade.BondId).FirstOrDefaultAsync();
                        if (bond == null)
                        {
                            return BadRequest($"The bond with bond id {trade.BondId} does not exist");
                        }
                        BondTradeLine bondTradeLine = new BondTradeLine
                        {
                            BondTradeId = bondTrade.Id,
                            Side = trade.Side,
                            SecurityId = bond.IssueNumber,
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

        // gets a list of all uploaded and confirmed Bond Trades
        [HttpGet]
        [Route("GetConfirmedBondTrades/{For}")]
        [Authorize(Roles = CustomRoles.SuperAdmin, AuthenticationSchemes = "Bearer")]
        public async Task<ActionResult<UploadedBondTrade>> GetConfirmedBondTrades(string? For = "default")
        {


            BondTrade? uploadedTrade = new BondTrade();
            UploadedBondTrade uploadedTradesDTO = new UploadedBondTrade();
            var parsedDate = DateTime.Now;
            try
            {
                using (var db = new QuotationsBoardContext())
                {
                    db.Database.EnsureCreated();
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

                    uploadedTrade = await db.BondTrades
                        .Include(x => x.BondTradeLines)
                        .Where(t => t.TradeDate.Date == parsedDate.Date)
                        .FirstOrDefaultAsync();

                    if (uploadedTrade != null)
                    {
                        uploadedTradesDTO.Id = uploadedTrade.Id;
                        uploadedTradesDTO.UploadedAt = uploadedTrade.UploadedOn;
                        uploadedTradesDTO.UploadedBy = uploadedTrade.UploadedBy;
                        uploadedTradesDTO.UploadedBondTradeLineDTO = new List<UploadedBondTradeLineDTO>();
                        foreach (var trade in uploadedTrade.BondTradeLines)
                        {
                            uploadedTradesDTO.UploadedBondTradeLineDTO.Add(new UploadedBondTradeLineDTO
                            {
                                Id = trade.Id,
                                GorvermentBondTradeStageId = trade.BondTradeId,
                                Side = trade.Side,
                                SecurityId = trade.SecurityId,
                                ExecutedSize = trade.ExecutedSize,
                                ExcecutedPrice = trade.ExcecutedPrice,
                                ExecutionID = trade.ExecutionID,
                                TransactionTime = trade.TransactionTime,
                                DirtyPrice = trade.DirtyPrice,
                                Yield = trade.Yield,
                                TradeDate = trade.TransactionTime,
                                TradeReportID = trade.TransactionID ?? ""
                            });
                        }
                    }

                    return Ok(uploadedTradesDTO);
                }
            }
            catch (Exception Ex)
            {
                UtilityService.LogException(Ex);
                return StatusCode(500, UtilityService.HandleException(Ex));
            }
        }

        // Gives the details of a specific Bond Trade

        [HttpGet]
        [Route("GetConfirmedBondTradeDetails/{BondTradeId}")]
        [Authorize(Roles = CustomRoles.SuperAdmin, AuthenticationSchemes = "Bearer")]

        public async Task<ActionResult<List<UploadedBondTradeLineDTO>>> GetConfirmedBondTradeDetails(string BondTradeId)
        {
            if (string.IsNullOrWhiteSpace(BondTradeId))
            {
                return BadRequest("The Bond Trade Id is required");
            }
            List<UploadedBondTradeLineDTO> uploadedTradesDTO = new List<UploadedBondTradeLineDTO>();
            try
            {
                using (var db = new QuotationsBoardContext())
                {
                    db.Database.EnsureCreated();
                    var bondTrade = await db.BondTrades.Include(x => x.BondTradeLines).Where(t => t.Id == BondTradeId).FirstOrDefaultAsync();
                    if (bondTrade == null)
                    {
                        return BadRequest("The Bond Trade you are looking for does not exist");
                    }
                    foreach (var trade in bondTrade.BondTradeLines)
                    {
                        uploadedTradesDTO.Add(new UploadedBondTradeLineDTO
                        {
                            Id = trade.Id,
                            GorvermentBondTradeStageId = trade.BondTradeId,
                            Side = trade.Side,
                            SecurityId = trade.SecurityId,
                            ExecutedSize = trade.ExecutedSize,
                            ExcecutedPrice = trade.ExcecutedPrice,
                            ExecutionID = trade.ExecutionID,
                            TransactionTime = trade.TransactionTime,
                            DirtyPrice = trade.DirtyPrice,
                            Yield = trade.Yield,
                            TradeDate = trade.TransactionTime
                        });
                    }
                    return Ok(uploadedTradesDTO);
                }
            }
            catch (Exception Ex)
            {
                UtilityService.LogException(Ex);
                return StatusCode(500, UtilityService.HandleException(Ex));
            }
        }

        // deletes a specific Bond Trade
        [HttpDelete]
        [Route("DeleteConfirmedBondTrade/{BondTradeId}")]
        [Authorize(Roles = CustomRoles.SuperAdmin, AuthenticationSchemes = "Bearer")]

        public async Task<IActionResult> DeleteConfirmedBondTrade(string BondTradeId)
        {
            if (string.IsNullOrWhiteSpace(BondTradeId))
            {
                return BadRequest("The Bond Trade Id is required");
            }
            try
            {
                using (var db = new QuotationsBoardContext())
                {
                    db.Database.EnsureCreated();
                    var bondTrade = await db.BondTrades.Include(x => x.BondTradeLines).Where(t => t.Id == BondTradeId).FirstOrDefaultAsync();
                    if (bondTrade == null)
                    {
                        return BadRequest("The Bond Trade you are looking for does not exist");
                    }
                    db.BondTrades.Remove(bondTrade);
                    await db.SaveChangesAsync();
                    return Ok("Bond Trade Deleted Successfully");
                }
            }
            catch (Exception Ex)
            {
                UtilityService.LogException(Ex);
                return StatusCode(500, UtilityService.HandleException(Ex));
            }
        }

        // gets average statistics for all bonds (Trades only) for a specific date
        [HttpGet]
        [Route("GetAverageTradeStatistics/{For}")]
        [Authorize(Roles = $"{CustomRoles.SuperAdmin},{CustomRoles.Institution}", AuthenticationSchemes = "Bearer")]

        public async Task<ActionResult<List<BondTradeAverageStatistic>>> GetAverageTradeStatistics(string? For = "default")
        {

            var parsedDate = DateTime.Now;
            // if no date is specified, use today's date
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

            List<Bond> bonds = new List<Bond>();
            List<BondTradeAverageStatistic> bondAverageStatistics = new List<BondTradeAverageStatistic>();
            BondTrade? bondTrade = new BondTrade();
            var bondStatisticsDict = new Dictionary<string, BondTradeAverageStatistic>();
            try
            {
                using (var db = new QuotationsBoardContext())
                {
                    db.Database.EnsureCreated();
                    var tradedBondIds = await db.BondTrades
                                    .Where(t => t.TradeDate.Date == parsedDate.Date)
                                    .SelectMany(t => t.BondTradeLines)
                                    .Select(btl => btl.BondId)
                                    .Distinct()
                                    .ToListAsync();

                    var allNotMaturedTradedBonds = await db.Bonds
                              .Where(b => b.MaturityDate.Date > parsedDate.Date && tradedBondIds.Contains(b.Id))
                              .ToListAsync();

                    // var allNotMaturedBonds = await db.Bonds.Where(b => b.MaturityDate.Date > parsedDate.Date).ToListAsync();

                    foreach (var bond in allNotMaturedTradedBonds)
                    {
                        var diffrenceBetweenSelectedDateAndMaturityDate = bond.MaturityDate.Date - parsedDate.Date;
                        var m = diffrenceBetweenSelectedDateAndMaturityDate.TotalDays / 364;
                        var yearsToMaturity = Math.Round(m, 2, MidpointRounding.AwayFromZero);

                        bondStatisticsDict[bond.Id] = new BondTradeAverageStatistic
                        {
                            BondId = bond.Id,
                            BondName = bond.IssueNumber,
                            YearsToMaturity = yearsToMaturity,
                            IssueNo = bond.Isin
                        };

                        // get all trades for the day selected
                        bondTrade = await db.BondTrades
                            .Include(x => x.BondTradeLines)
                            .Where(t => t.TradeDate.Date == parsedDate.Date)
                            .FirstOrDefaultAsync();

                        if (bondTrade != null)
                        {
                            // group the BondTradeLines by BondId
                            var bondTradeLinesGroupedByBondId = bondTrade.BondTradeLines.GroupBy(x => x.BondId);


                            foreach (var bond_trade_line in bondTradeLinesGroupedByBondId)
                            {
                                var trades = bond_trade_line.ToList();
                                if (!bondStatisticsDict.TryGetValue(bond_trade_line.Key, out var bondStatistic))
                                {
                                    bondStatistic = new BondTradeAverageStatistic
                                    {
                                        BondId = bond_trade_line.Key,
                                    };
                                    bondStatisticsDict[bond_trade_line.Key] = bondStatistic;
                                }

                                var _buyVolume = bond_trade_line.Where(x => x.Side == "BUY").Sum(x => x.ExecutedSize);
                                var _sellVolume = bond_trade_line.Where(x => x.Side == "SELL").Sum(x => x.ExecutedSize);

                                // if _buyVolume is less than 50 Million skip the trade
                                if (_buyVolume < 50000000)
                                {
                                    continue;
                                }

                                var totalExecutedVolume = QuotationsHelper.GetTotalVolume(trades);
                                var totalWeightedBuyYield = QuotationsHelper.GetTotalWeightedYield(trades, "BUY");
                                var totalWeightedSellYield = QuotationsHelper.GetTotalWeightedYield(trades, "SELL");
                                var totalBuyExecutedVolume = QuotationsHelper.GetTotalVolumeBySide(trades, "BUY");
                                var totalSellExecutedVolume = QuotationsHelper.GetTotalVolumeBySide(trades, "SELL");
                                var totalTradeCount = trades.Count();
                                var totalCombinedWeightedYield = totalWeightedBuyYield + totalWeightedSellYield;
                                var averageCombinedYield = QuotationsHelper.CalculateAverageWeightedTradedYield(trades);
                                var averageExecutedVolumePerTrade = QuotationsHelper.CalculateAverage(totalExecutedVolume, totalTradeCount);
                                var averageWeightedBuyYield = QuotationsHelper.CalculateAverage(totalWeightedBuyYield, totalBuyExecutedVolume);

                                bondStatistic.AverageWeightedTradeYield = Math.Round(averageCombinedYield, 4, MidpointRounding.AwayFromZero);
                                bondStatistic.TradedVolume = totalExecutedVolume;
                                bondStatistic.NumberofTrades = totalTradeCount;
                                bondStatistic.WeightedTradeBuyYield = Math.Round(averageWeightedBuyYield, 4, MidpointRounding.AwayFromZero);

                            }
                        }
                    }

                    bondAverageStatistics = bondStatisticsDict.Values.ToList();
                    return Ok(bondAverageStatistics);
                }
            }
            catch (Exception Ex)
            {
                UtilityService.LogException(Ex);
                return StatusCode(500, UtilityService.HandleException(Ex));
            }


        }


        // gets average statistics for all bonds (both traded and quoted) for a specific date
        [HttpGet]
        [Route("GetAverageStatistics/{For}")]
        [Authorize(AuthenticationSchemes = "Bearer")]
        public async Task<ActionResult<List<BondAverageStatistic>>> GetAverageStatistics(string? For = "default")
        {

            var parsedDate = DateTime.Now;
            // if no date is specified, use today's date
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

            List<Bond> bonds = new List<Bond>();
            List<BondAverageStatistic> bondAverageStatistics = new List<BondAverageStatistic>();
            BondTrade? bondTrade = new BondTrade();
            var bondStatisticsDict = new Dictionary<string, BondAverageStatistic>();
            try
            {
                using (var db = new QuotationsBoardContext())
                {
                    db.Database.EnsureCreated();
                    var allNotMaturedBonds = await db.Bonds.Where(b => b.MaturityDate.Date > parsedDate.Date).ToListAsync();
                    var (startOfCycle, endOfCycle) = TBillHelper.GetCurrentTBillCycle(parsedDate);
                    var (previousCylestart, previousCycleEnd) = TBillHelper.GetPreviousTBillCycle(parsedDate);
                    // DateTime LastWeekRelativeToSelectedDate = parsedDate.AddDays(-7);
                    // DateTime startOfLastWeek = LastWeekRelativeToSelectedDate.AddDays(-(int)LastWeekRelativeToSelectedDate.DayOfWeek); // Sunday
                    // DateTime endOfLastWeek = LastWeekRelativeToSelectedDate.AddDays(6 - (int)LastWeekRelativeToSelectedDate.DayOfWeek); // Saturday

                    var PreviousTbillImpliedYields = await db.TBillImpliedYields
                    .Include(x => x.TBill)
                    .Where(t => t.Date.Date < parsedDate.Date)
                    .OrderByDescending(t => t.Date)
                    .ToListAsync();

                    var CurrentTbillYields = await db.TBillImpliedYields
                    .Include(x => x.TBill)
                    .Where(t => t.Date.Date == parsedDate.Date)
                    .OrderByDescending(t => t.Date)
                    .ToListAsync();

                    foreach (var tBillYield in CurrentTbillYields)
                    {
                        if (tBillYield.TBill.Tenor == 364)
                        {
                            var previous364 = PreviousTbillImpliedYields.Where(t => t.TBill.Tenor == 364).FirstOrDefault();
                            if (previous364 == null)
                            {
                                continue;
                                // return BadRequest("The previous implied yield for the 364 days T-Bill is not available");
                            }
                            var diffrenceBetweenSelectedDateAndMaturityDate = tBillYield.TBill.MaturityDate.Date - parsedDate.Date;
                            var m = diffrenceBetweenSelectedDateAndMaturityDate.TotalDays / 364;
                            var yearsToMaturity = Math.Round(m, 4, MidpointRounding.AwayFromZero);

                            bondStatisticsDict[tBillYield.TBillId] = new BondAverageStatistic
                            {
                                BondId = tBillYield.TBillId,
                                BondName = tBillYield.TBill.Tenor.ToString() + " Days T-Bill",
                                YearsToMaturity = yearsToMaturity,
                                BondCategory = "T-Bill",
                                BondType = "T-Bill",
                                AverageWeightedTradeYield = 0,
                                AverageWeightedQuotedYield = 0,
                                DaysImpliedYield = tBillYield.Yield,
                                ISIN = "T-Bill",
                                PreviousImpliedYield = previous364.Yield,
                            };

                        }

                        if (tBillYield.TBill.Tenor == 182)
                        {
                            var previous182 = PreviousTbillImpliedYields.Where(t => t.TBill.Tenor == 182).FirstOrDefault();
                            if (previous182 == null)
                            {
                                return BadRequest("The previous implied yield for the 182 days T-Bill is not available");
                            }
                            var diffrenceBetweenSelectedDateAndMaturityDate = tBillYield.TBill.MaturityDate.Date - parsedDate.Date;
                            var m = diffrenceBetweenSelectedDateAndMaturityDate.TotalDays / 364;
                            var yearsToMaturity = Math.Round(m, 4, MidpointRounding.AwayFromZero);

                            bondStatisticsDict[tBillYield.TBillId] = new BondAverageStatistic
                            {
                                BondId = tBillYield.TBillId,
                                BondName = tBillYield.TBill.Tenor.ToString() + " Days T-Bill",
                                YearsToMaturity = yearsToMaturity,
                                BondCategory = "T-Bill",
                                BondType = "T-Bill",
                                AverageWeightedTradeYield = 0,
                                AverageWeightedQuotedYield = 0,
                                DaysImpliedYield = tBillYield.Yield,
                                ISIN = "T-Bill",
                                PreviousImpliedYield = previous182.Yield,
                            };
                        }

                        if (tBillYield.TBill.Tenor == 91)
                        {
                            var previous91 = PreviousTbillImpliedYields.Where(t => t.TBill.Tenor == 91).FirstOrDefault();
                            if (previous91 == null)
                            {
                                return BadRequest("The previous implied yield for the 91 days T-Bill is not available");
                            }
                            var diffrenceBetweenSelectedDateAndMaturityDate = tBillYield.TBill.MaturityDate.Date - parsedDate.Date;
                            var m = diffrenceBetweenSelectedDateAndMaturityDate.TotalDays / 364;
                            var yearsToMaturity = Math.Round(m, 4, MidpointRounding.AwayFromZero);

                            bondStatisticsDict[tBillYield.TBillId] = new BondAverageStatistic
                            {
                                BondId = tBillYield.TBillId,
                                BondName = tBillYield.TBill.Tenor.ToString() + " Days T-Bill",
                                YearsToMaturity = yearsToMaturity,
                                BondCategory = "T-Bill",
                                BondType = "T-Bill",
                                AverageWeightedTradeYield = 0,
                                AverageWeightedQuotedYield = 0,
                                DaysImpliedYield = tBillYield.Yield,
                                ISIN = "T-Bill",
                                PreviousImpliedYield = previous91.Yield,
                            };
                        }


                    }


                    foreach (var bond in allNotMaturedBonds)
                    {
                        var diffrenceBetweenSelectedDateAndMaturityDate = bond.MaturityDate.Date - parsedDate.Date;

                        var previousImpliedYield = db.ImpliedYields
                        .Where(i => i.BondId == bond.Id
                            && i.YieldDate.Date < parsedDate.Date)
                        .OrderByDescending(i => i.YieldDate)
                        .FirstOrDefault();

                        double currentImplied = 0;
                        var stringImplied = "Not Yet Calculated";

                        // get implied yield for the bond as at the selected date
                        var impliedYield = db.ImpliedYields
                            .Where(i => i.BondId == bond.Id
                                && i.YieldDate.Date == parsedDate.Date)
                            .FirstOrDefault();

                        if (impliedYield != null)
                        {
                            currentImplied = impliedYield.Yield;
                            stringImplied = currentImplied.ToString();
                        }
                        else
                        {
                            currentImplied = 0;

                        }

                        var ImpliedYieldString = stringImplied;


                        var m = diffrenceBetweenSelectedDateAndMaturityDate.TotalDays / 364;
                        var yearsToMaturity = Math.Round(m, 4, MidpointRounding.AwayFromZero);

                        bondStatisticsDict[bond.Id] = new BondAverageStatistic
                        {
                            BondId = bond.Id,
                            BondName = bond.IssueNumber,
                            YearsToMaturity = yearsToMaturity,
                            BondCategory = bond.BondCategory,
                            BondType = bond.BondType,
                            PreviousImpliedYield = previousImpliedYield != null ? previousImpliedYield.Yield : 0,
                            ISIN = bond.Isin,
                            DaysImpliedYield = currentImplied,
                            IssueDate = bond.IssueDate,
                        };


                        var _quotations = await db.Quotations
                       .Include(x => x.Bond)
                       .Where(q => q.CreatedAt.Date == parsedDate.Date && q.BondId == bond.Id)
                       .ToListAsync();

                        var groupedQuotations = _quotations.GroupBy(x => x.BondId);

                        // get all trades for the day selected
                        bondTrade = await db.BondTrades
                            .Include(x => x.BondTradeLines)
                            .Where(t => t.TradeDate.Date == parsedDate.Date)
                            .FirstOrDefaultAsync();



                        foreach (var _quote in groupedQuotations)
                        {
                            if (!bondStatisticsDict.TryGetValue(_quote.Key, out var bondStatistic))
                            {
                                bondStatistic = new BondAverageStatistic
                                {
                                    BondId = _quote.Key,
                                };
                                bondStatisticsDict[_quote.Key] = bondStatistic;
                            }

                            var _quotedBuyVolume = _quote.Where(x => x.BuyingYield > 0).Sum(x => x.BuyVolume);
                            var _quotedSellVolume = _quote.Where(x => x.SellingYield > 0).Sum(x => x.SellVolume);

                            // Skip processing if both volumes are below the threshold
                            if (_quotedBuyVolume < 50000000 && _quotedSellVolume < 50000000)
                            {
                                continue;
                            }

                            var totalQuotesCount = _quote.Count();
                            var totalCombinedVolume = _quote.Sum(x => x.BuyVolume + x.SellVolume);
                            var averageCombinedVolume = totalCombinedVolume / totalQuotesCount;
                            double averageWeightedSellYield = QuotationsHelper.CalculateAverageWeightedQuotedSellYield(_quote.ToList());
                            double averageWeightedBuyYield = QuotationsHelper.CalculateAverageWeightedQuotedBuyYield(_quote.ToList());
                            double averageTotalWeightedYield = QuotationsHelper.CalculateAverageWeightedQuotedYield(_quote.ToList());

                            bondStatistic.BondName = _quote.First().Bond.IssueNumber;
                            bondStatistic.AverageWeightedQuotedYield = Math.Round(averageTotalWeightedYield, 4, MidpointRounding.AwayFromZero);
                            bondStatistic.QuotedVolume = totalCombinedVolume;
                            bondStatistic.NumberofQuotes = totalQuotesCount;
                            bondStatistic.WeightedQuotedSellYield = Math.Round(averageWeightedSellYield, 4, MidpointRounding.AwayFromZero);
                            bondStatistic.WeightedQuotedBuyYield = Math.Round(averageWeightedBuyYield, 4, MidpointRounding.AwayFromZero);
                        }


                        if (bondTrade != null)
                        {
                            // group the BondTradeLines by BondId
                            var bondTradeLinesGroupedByBondId = bondTrade.BondTradeLines.GroupBy(x => x.BondId);


                            foreach (var bond_trade_line in bondTradeLinesGroupedByBondId)
                            {

                                if (!bondStatisticsDict.TryGetValue(bond_trade_line.Key, out var bondStatistic))
                                {
                                    bondStatistic = new BondAverageStatistic
                                    {
                                        BondId = bond_trade_line.Key,
                                    };
                                    bondStatisticsDict[bond_trade_line.Key] = bondStatistic;
                                }


                                var _totalBuyExecutedVolume = QuotationsHelper.GetTotalVolumeBySide(bond_trade_line.ToList(), "BUY");
                                double averageWeightedBuyYield = QuotationsHelper.CalculateAverageWeightedTradedYield(bond_trade_line.ToList());
                                var totalTradeCount = bond_trade_line.Where(x => x.Side == "BUY" && x.ExecutedSize >= 50000000).Count();

                                bondStatistic.AverageWeightedTradeYield = Math.Round(averageWeightedBuyYield, 4, MidpointRounding.AwayFromZero);
                                bondStatistic.TradedVolume = _totalBuyExecutedVolume;
                                bondStatistic.NumberofTrades = totalTradeCount;
                                bondStatistic.WeightedTradeBuyYield = Math.Round(averageWeightedBuyYield, 4, MidpointRounding.AwayFromZero);
                                bondStatistic.WeightedTradeSellYield = 0;
                            }
                        }
                    }

                    bondAverageStatistics = bondStatisticsDict.Values.ToList();
                    return Ok(bondAverageStatistics);
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
            return excelSecurityId;
            try
            {
                if (string.IsNullOrWhiteSpace(excelSecurityId))
                    throw new ArgumentException("Excel Security ID is null or whitespace.");
                int indexOfPeriod = excelSecurityId.IndexOf('.');
                string formattedString = excelSecurityId.Substring(indexOfPeriod + 1);
                return formattedString;
            }
            catch (Exception ex)
            {
                // Log the error, return null, or throw a custom exception as appropriate for your application's error handling policy
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

        private List<UploadedTrade> ReadExcelData(IXLWorksheet worksheet)
        {
            var UploadedTrades = new List<UploadedTrade>();
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

                // Check if the Side is "SELL", skip if true
                string side = worksheet.Cell(row, 2).Value.ToString();
                if (side.Equals("SELL", StringComparison.OrdinalIgnoreCase)) continue;

                // Assuming data is already validated and can be directly parsed
                var UploadedTrade = new UploadedTrade
                {
                    Side = side, // This will only be "BUY" due to the check above
                    SecurityId = worksheet.Cell(row, 3).Value.ToString().Trim(),
                    ExecutedSize = double.Parse(worksheet.Cell(row, 4).Value.ToString()),
                    ExecutedPrice = double.Parse(worksheet.Cell(row, 5).Value.ToString()),
                    ExecutionID = worksheet.Cell(row, 6).Value.ToString(),
                    TradeDate = DateTime.Parse(worksheet.Cell(row, 13).Value.ToString()),
                    DirtyPrice = decimal.Parse(worksheet.Cell(row, 8).Value.ToString()),
                    Yield = double.Parse(worksheet.Cell(row, 9).Value.ToString()),
                    TradeReportID = worksheet.Cell(row, 11).Value.ToString(),
                };
                UploadedTrades.Add(UploadedTrade);
            }

            return UploadedTrades;
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

                if (string.IsNullOrEmpty(excelSecurityId))
                {
                    errors.Add($"Row {rowToBeginAt}: Security ID format is invalid.");
                    continue; // Skip further validation for this row
                }

                using (var dbContext = new QuotationsBoardContext())
                {
                    dbContext.Database.EnsureCreated();
                    // Check if transformedSecurityId exists in the database
                    var bondExists = dbContext.Bonds.Any(b => b.IssueNumber == transformedSecurityId.Trim());
                    if (!bondExists)
                    {
                        errors.Add($"Row {rowToBeginAt}: Security ID '{excelSecurityId}' does not exist in the system.");
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
