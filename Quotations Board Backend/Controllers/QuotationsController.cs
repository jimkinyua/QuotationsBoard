using AutoMapper;
using ClosedXML.Excel;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Quotations_Board_Backend.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    //[Authorize(Roles = $"{CustomRoles.Dealer}, {CustomRoles.ChiefDealer}", AuthenticationSchemes = "Bearer")]
    //[Authorize(Roles = CustomRoles.Dealer + "," + CustomRoles.ChiefDealer, AuthenticationSchemes = "Bearer")]

    public class QuotationsController : ControllerBase
    {
        // Create a new quotation
        [HttpPost("CreateQuotation")]
        [ProducesResponseType(StatusCodes.Status201Created)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        public async Task<ActionResult> CreateQuotation(NewQuotation newQuotation)
        {
            try
            {
                // validate Model
                if (!ModelState.IsValid)
                {
                    return BadRequest(ModelState);
                }
                using (var context = new QuotationsBoardContext())
                {
                    LoginTokenDTO TokenContents = UtilityService.GetUserIdFromCurrentRequest(Request);
                    var userId = UtilityService.GetUserIdFromToken(Request);
                    Quotation quotation = new Quotation
                    {
                        BondId = newQuotation.BondId,
                        BuyingYield = newQuotation.BuyYield,
                        SellingYield = newQuotation.SellYield,
                        BuyVolume = newQuotation.BuyVolume,
                        UserId = userId,
                        CreatedAt = DateTime.Now,//- TimeSpan.FromDays(5),
                        InstitutionId = TokenContents.InstitutionId,
                        SellVolume = newQuotation.SellVolume
                    };
                    // Quotes Past 9 am will not be accepted
                    // if (quotation.CreatedAt.Hour >= 9)
                    // {
                    //     return BadRequest("Quotations past 9 am are not accepted");
                    // }
                    // Ensure selling yield is not greater than buying yield
                    if (quotation.SellingYield > quotation.BuyingYield)
                    {
                        return BadRequest("Selling yield cannot be greater than buying yield");
                    }

                    // ensure Selling Yield is not greater than 100
                    if (quotation.SellingYield > 100)
                    {
                        return BadRequest("Selling yield cannot be greater than 100");
                    }

                    // ensure Buying Yield is not greater than 100
                    if (quotation.BuyingYield > 100)
                    {
                        return BadRequest("Buying yield cannot be greater than 100");
                    }

                    // Ensure difrence between selling yield and buying yield is not greater than 1%
                    var difference = Math.Abs(quotation.BuyingYield - quotation.SellingYield);
                    var percentageDifference = Math.Round((difference / quotation.BuyingYield) * 100, 2);
                    if (percentageDifference > 1)
                    {
                        return BadRequest("The difference between selling yield and buying yield divided by Buying Yield, cannot be greater than 1%. The current difference is " + percentageDifference + "%");
                    }


                    // Ensure that today this institution has not already filled a quotation for this bond
                    var existingQuotation = await context.Quotations.FirstOrDefaultAsync(q => q.InstitutionId == quotation.InstitutionId && q.BondId == quotation.BondId && q.CreatedAt.Date == quotation.CreatedAt.Date);
                    if (existingQuotation != null)
                    {
                        return BadRequest(" A quotation for this bond has already been  for today");
                    }

                    // GET THE MOST RECENT DAY A QUOTATION WAS FILLED FOR THIS BOND Except today
                    var mostRecentTradingDay = await context.Quotations
                    .Where(q => q.BondId == quotation.BondId && q.CreatedAt < quotation.CreatedAt.Date)
                    .OrderByDescending(q => q.CreatedAt)
                    .Select(q => q.CreatedAt.Date)
                    .FirstOrDefaultAsync();
                    if (mostRecentTradingDay == default(DateTime))
                    {
                        // First time this bond is being quoted (We can allow the quotation because there is no previous quotation hennce not bale to compare)
                        // Save the quotation
                        await context.Quotations.AddAsync(quotation);
                        await context.SaveChangesAsync();
                        return StatusCode(201, quotation);
                    }
                    else
                    {
                        var mostRecentDayQuotations = await context.Quotations
                            .Where(q => q.BondId == quotation.BondId && q.CreatedAt.Date == mostRecentTradingDay.Date)
                            .ToListAsync();

                        // Calculate the Average Weighted Yield of the previous day's quotes
                        decimal totalWeightedYield = 0;
                        decimal totalVolume = 0;
                        foreach (var q in mostRecentDayQuotations)
                        {
                            if (q.BuyVolume < 50000000 || q.SellVolume < 50000000)
                            {
                                continue;
                            }
                            totalWeightedYield += (q.BuyingYield * q.BuyVolume) + (q.SellingYield * q.SellVolume);
                            totalVolume += q.BuyVolume + q.SellVolume;
                        }
                        decimal averageRecentWeightedYield = totalWeightedYield / totalVolume;
                        decimal currentTotalWeightedYield = (quotation.BuyingYield * quotation.BuyVolume) + (quotation.SellingYield * quotation.SellVolume);
                        decimal currentQuotationVolume = quotation.BuyVolume + quotation.SellVolume;
                        decimal currentAverageWeightedYield = currentTotalWeightedYield / currentQuotationVolume;
                        var change = currentAverageWeightedYield - averageRecentWeightedYield;
                        var percentgeChange = (change / averageRecentWeightedYield) * 100;
                        // if greater than 1% reject the quotation
                        if (percentgeChange > 1)
                        {
                            string errorMessage = $"Quotation rejected. The current average weighted yield ({currentAverageWeightedYield:0.##}%) significantly differs from the most recent trading day's average weighted yield ({averageRecentWeightedYield:0.##}%) recorded on {mostRecentTradingDay:yyyy-MM-dd}. The percentage change of {percentgeChange:0.##}% exceeds the allowable limit of 1%.";
                            return BadRequest(errorMessage);
                        }
                        else
                        {
                            // Save the quotation
                            await context.Quotations.AddAsync(quotation);
                            await context.SaveChangesAsync();
                            return StatusCode(201, quotation);
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

        // Bulk upload quotations
        [HttpPost("BulkUploadQuotations")]
        [ProducesResponseType(StatusCodes.Status201Created)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        public async Task<ActionResult> BulkUploadQuotations([FromForm] BulkUpload bulkUpload)
        {
            try
            {
                // validate Model
                if (!ModelState.IsValid)
                {
                    return BadRequest(ModelState);
                }
                using (var context = new QuotationsBoardContext())
                {
                    LoginTokenDTO TokenContents = UtilityService.GetUserIdFromCurrentRequest(Request);
                    var userId = UtilityService.GetUserIdFromToken(Request);

                    // Is it past 9 am?
                    // if (DateTime.Now.Hour >= 9)
                    // {
                    //     return BadRequest("Bulk upload quotations past 9 am are not accepted");
                    // }

                    // Read the excel file
                    var excelFile = bulkUpload.ExcelFile;

                    using (var stream = new MemoryStream())
                    {
                        await excelFile.CopyToAsync(stream);
                        using (var workbook = new XLWorkbook(stream))
                        {
                            var sheetWhereDataIsLocated = workbook.Worksheet(1);
                            var errors = ValidateBondData(sheetWhereDataIsLocated);
                            if (errors.Count > 0)
                            {
                                return BadRequest(errors);
                            }

                            using (var db = new QuotationsBoardContext())
                            {

                                db.Database.EnsureCreated();
                                var quotes = ReadExcelData(sheetWhereDataIsLocated);
                                await db.Quotations.AddRangeAsync(quotes);
                                await db.SaveChangesAsync();
                                return StatusCode(201);
                            }
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

        private List<Quotation> ReadExcelData(IXLWorksheet worksheet)
        {
            var quotationRows = new List<Quotation>();
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

                var bondId = worksheet.Cell(row, 1).Value.ToString();
                using (var dbContext = new QuotationsBoardContext())
                {
                    dbContext.Database.EnsureCreated();
                    var bond = dbContext.Bonds.FirstOrDefault(b => b.IssueNumber == bondId);
                    if (bond == null)
                    {
                        throw new Exception($"Bond ID '{bondId}' does not exist in the system.");
                    }
                    var userId = UtilityService.GetUserIdFromToken(Request);
                    // Get User details
                    var user = dbContext.Users.FirstOrDefault(u => u.Id == userId);
                    if (user == null)
                    {
                        throw new Exception($"User ID '{userId}' does not exist in the system.");
                    }

                    if (user.InstitutionId == null)
                    {
                        throw new Exception($"User ID '{userId}' does not have an institution.");
                    }

                    // Ensure selling yield is not greater than buying yield
                    var buyYield = decimal.Parse(worksheet.Cell(row, 2).Value.ToString());
                    var sellYield = decimal.Parse(worksheet.Cell(row, 4).Value.ToString());
                    var buyVolume = int.Parse(worksheet.Cell(row, 3).Value.ToString());
                    var sellVolume = int.Parse(worksheet.Cell(row, 5).Value.ToString());

                    //  Ensure selling yield is not greater than 100
                    if (sellYield > 100)
                    {
                        throw new Exception($"Selling yield cannot be greater than 100.");
                    }


                    //  Ensure buying yield is not greater than 100
                    if (buyYield > 100)
                    {
                        throw new Exception($"Buying yield cannot be greater than 100.");
                    }

                    if (sellYield > buyYield)
                    {
                        throw new Exception($"Selling yield cannot be greater than buying yield.");
                    }

                    // Ensure difrence between selling yield and buying yield is not greater than 1%
                    var difference = buyYield - sellYield;
                    var percentageDifference = (difference / buyYield) * 100;
                    if (percentageDifference > 1)
                    {
                        throw new Exception($"The difference between selling yield and buying yield cannot be greater than 1%. The current difference is {percentageDifference}% check on row {row}");
                    }

                    // Enusre that today this institution has not already filled a quotation for this bond
                    var existingQuotation = dbContext.Quotations.FirstOrDefault(q => q.InstitutionId == user.InstitutionId && q.BondId == bondId && q.CreatedAt.Date == DateTime.Now.Date);
                    if (existingQuotation != null)
                    {
                        throw new Exception($"A quotation for this bond at cell {row} has already been filled for today.");
                    }

                    var mostRecentTradingDay = dbContext.Quotations
                        .Where(q => q.BondId == bond.Id && q.CreatedAt < DateTime.Now.Date)
                        .OrderByDescending(q => q.CreatedAt)
                        .Select(q => q.CreatedAt.Date)
                        .FirstOrDefault();

                    Quotation quote = new Quotation
                    {
                        BondId = bond.Id,
                        BuyingYield = decimal.Parse(worksheet.Cell(row, 2).Value.ToString()),
                        BuyVolume = int.Parse(worksheet.Cell(row, 3).Value.ToString()),
                        SellingYield = decimal.Parse(worksheet.Cell(row, 4).Value.ToString()),
                        SellVolume = int.Parse(worksheet.Cell(row, 5).Value.ToString()),
                        UserId = UtilityService.GetUserIdFromToken(Request),
                        CreatedAt = DateTime.Now,
                        InstitutionId = user.InstitutionId
                    };
                    if (mostRecentTradingDay == default(DateTime))
                    {
                        // First time this bond is being quoted (We can allow the quotation because there is no previous quotation hennce not bale to compare)
                        quotationRows.Add(quote);
                    }
                    else
                    {
                        var mostRecentDayQuotations = dbContext.Quotations
                            .Where(q => q.BondId == bond.Id && q.CreatedAt.Date == mostRecentTradingDay.Date)
                            .ToList();
                        // Calculate the Average Weighted Yield of the previous day's quotes
                        decimal totalWeightedYield = 0;
                        decimal totalVolume = 0;
                        foreach (var q in mostRecentDayQuotations)
                        {
                            if (q.BuyVolume < 50000000 || q.SellVolume < 50000000)
                            {
                                continue;
                            }
                            totalWeightedYield += (q.BuyingYield * q.BuyVolume) + (q.SellingYield * q.SellVolume);
                            totalVolume += q.BuyVolume + q.SellVolume;
                        }
                        decimal averageRecentWeightedYield = totalWeightedYield / totalVolume;
                        decimal currentTotalWeightedYield = (buyYield * buyVolume) + (sellYield * sellVolume);
                        decimal currentQuotationVolume = buyVolume + sellVolume;
                        decimal currentAverageWeightedYield = currentTotalWeightedYield / currentQuotationVolume;
                        var change = currentAverageWeightedYield - averageRecentWeightedYield;
                        var percentgeChange = (change / averageRecentWeightedYield) * 100;
                        // if greater than 1% reject the quotation
                        if (percentgeChange > 1)
                        {
                            throw new Exception($"Quotation at row {row} rejected. The current average weighted yield ({currentAverageWeightedYield:0.##}%) significantly differs from the most recent trading day's average weighted yield ({averageRecentWeightedYield:0.##}%) recorded on {mostRecentTradingDay:yyyy-MM-dd}. The percentage change of {percentgeChange:0.##}% exceeds the allowable limit of 1%.");
                        }
                        else
                        {
                            quotationRows.Add(quote);
                        }
                    }
                }
            }
            return quotationRows;
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

        private List<string> ValidateBondData(IXLWorksheet worksheet)
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

                if (isEmptyRow) continue; // Skip this row if it's empty

                var bondId = worksheet.Cell(rowToBeginAt, 1).Value.ToString();
                var buyYield = worksheet.Cell(rowToBeginAt, 2).Value.ToString();
                var buyVolume = worksheet.Cell(rowToBeginAt, 3).Value.ToString();
                var sellYield = worksheet.Cell(rowToBeginAt, 4).Value.ToString();
                var sellQuantity = worksheet.Cell(rowToBeginAt, 5).Value.ToString();

                // Example validations:
                if (string.IsNullOrWhiteSpace(bondId))
                    errors.Add($"Row {rowToBeginAt} Cell A: 'Bond Id' is required.");

                if (!decimal.TryParse(buyYield, out _))
                    errors.Add($"Row {rowToBeginAt} Cell B: 'Buy Yield' is not a valid decimal number.");

                if (!int.TryParse(buyVolume, out _))
                    errors.Add($"Row {rowToBeginAt} Cell C: 'Buy Volume' is not a valid integer.");

                if (!decimal.TryParse(sellYield, out _))
                    errors.Add($"Row {rowToBeginAt} Cell D: 'Sell Yield' is not a valid decimal number.");

                if (!int.TryParse(sellQuantity, out _))
                    errors.Add($"Row {rowToBeginAt} Cell E: 'Sell Quantity' is not a valid integer.");

                // Check if bondId exists in the database
                using (var dbContext = new QuotationsBoardContext())
                {
                    dbContext.Database.EnsureCreated();
                    var bondExists = dbContext.Bonds.Any(b => b.IssueNumber == bondId);
                    if (!bondExists)
                    {
                        errors.Add($"Row {rowToBeginAt}: Bond ID '{bondId}' does not exist in the system.");
                    }
                }
            }

            return errors;
        }



        // Edit a quotation
        [HttpPut("EditQuotation")]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        public async Task<ActionResult> EditQuotation(EditQuotation editQuotation)
        {
            try
            {
                // validate Model
                if (!ModelState.IsValid)
                {
                    return BadRequest(ModelState);
                }
                using (var context = new QuotationsBoardContext())
                {
                    LoginTokenDTO TokenContents = UtilityService.GetUserIdFromCurrentRequest(Request);
                    var userId = UtilityService.GetUserIdFromToken(Request);
                    var existingQuotation = await context.Quotations.FirstOrDefaultAsync(q => q.Id == editQuotation.Id);

                    // what time ?
                    if (DateTime.Now.Hour >= 9)
                    {
                        return BadRequest("Quotations past 9 am are not accepted");
                    }

                    if (existingQuotation == null)
                    {
                        return BadRequest("Quotation does not exist");
                    }

                    // Essure selling Yield is not greater than 100
                    if (editQuotation.SellYield > 100)
                    {
                        return BadRequest("Selling yield cannot be greater than 100");
                    }

                    // Essure buying Yield is not greater than 100
                    if (editQuotation.BuyYield > 100)
                    {
                        return BadRequest("Buying yield cannot be greater than 100");
                    }

                    // Ensure selling yield is not greater than buying yield
                    if (editQuotation.SellYield > editQuotation.BuyYield)
                    {
                        return BadRequest("Selling yield cannot be greater than buying yield");
                    }

                    // Ensure difrence between selling yield and buying yield is not greater than 1%
                    var difference = editQuotation.BuyYield - editQuotation.SellYield;
                    var percentageDifference = (difference / editQuotation.BuyYield) * 100;
                    if (percentageDifference > 1)
                    {
                        return BadRequest("The difference between selling yield and buying yield cannot be greater than 1%. The current difference is " + percentageDifference + "%");
                    }

                    Quotation quotation = new Quotation
                    {
                        Id = editQuotation.Id,
                        BondId = editQuotation.BondId,
                        BuyingYield = editQuotation.BuyYield,
                        SellingYield = editQuotation.SellYield,
                        BuyVolume = editQuotation.BuyVolume,
                        SellVolume = editQuotation.SellVolume,
                        UserId = userId,
                        InstitutionId = TokenContents.InstitutionId
                    };

                    var mostRecentTradingDay = await context.Quotations
                    .Where(q => q.BondId == existingQuotation.BondId && q.CreatedAt < existingQuotation.CreatedAt.Date)
                    .OrderByDescending(q => q.CreatedAt)
                    .Select(q => q.CreatedAt.Date)
                    .FirstOrDefaultAsync();
                    if (mostRecentTradingDay == default(DateTime))
                    {
                        // Save the quotation
                        context.Quotations.Update(quotation);
                        await context.SaveChangesAsync();
                        return StatusCode(200);
                    }
                    else
                    {
                        var mostRecentDayQuotations = await context.Quotations
                            .Where(q => q.BondId == existingQuotation.BondId && q.CreatedAt.Date == mostRecentTradingDay.Date)
                            .ToListAsync();

                        // Calculate the Average Weighted Yield of the previous day's quotes
                        decimal totalWeightedYield = 0;
                        decimal totalVolume = 0;
                        foreach (var q in mostRecentDayQuotations)
                        {
                            if (q.BuyVolume < 50000000 || q.SellVolume < 50000000)
                            {
                                continue;
                            }

                            totalWeightedYield += (q.BuyingYield * q.BuyVolume) + (q.SellingYield * q.SellVolume);
                            totalVolume += q.BuyVolume + q.SellVolume;
                        }
                        decimal averageRecentWeightedYield = totalWeightedYield / totalVolume;
                        decimal currentTotalWeightedYield = (quotation.BuyingYield * quotation.BuyVolume) + (quotation.SellingYield * quotation.SellVolume);
                        decimal currentQuotationVolume = quotation.BuyVolume + quotation.SellVolume;
                        decimal currentAverageWeightedYield = currentTotalWeightedYield / currentQuotationVolume;
                        var change = currentAverageWeightedYield - averageRecentWeightedYield;
                        var percentgeChange = (change / averageRecentWeightedYield) * 100;
                        // if greater than 1% reject the quotation
                        if (percentgeChange > 1)
                        {
                            string errorMessage = $"Quotation rejected. The current average weighted yield ({currentAverageWeightedYield:0.##}%) significantly differs from the most recent trading day's average weighted yield ({averageRecentWeightedYield:0.##}%) recorded on {mostRecentTradingDay:yyyy-MM-dd}. The percentage change of {percentgeChange:0.##}% exceeds the allowable limit of 1%.";
                            return BadRequest(errorMessage);
                        }
                        else
                        {
                            // Save the quotation
                            context.Quotations.Update(quotation);
                            await context.SaveChangesAsync();
                            return StatusCode(200);
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

        // Fetch all quotations filled by Institution
        [HttpGet("GetQuotationsFilledByInstitution/{bondId}/{From}/{To}")]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        public async Task<ActionResult<QuotationDTO>> GetQuotationsFilledByInstitution(string bondId, string? From = "default", string? To = "default")
        {
            try
            {
                DateTime fromDate = DateTime.Now;
                DateTime toDate = DateTime.Now;
                if (From == "default")
                {
                    fromDate = DateTime.Now;
                }
                else
                {
                    var parsedDate = DateTime.Parse(From);
                    // is date valid?
                    if (fromDate == DateTime.MinValue)
                    {
                        return BadRequest("Invalid date");
                    }
                    // is date in the future?
                    if (fromDate > DateTime.Now)
                    {
                        return BadRequest("Date cannot be in the future");
                    }
                    fromDate = parsedDate;
                }

                if (To == "default")
                {
                    toDate = DateTime.Now;
                }
                else
                {
                    var parsedDate = DateTime.Parse(To);
                    // is date valid?
                    if (toDate == DateTime.MinValue)
                    {
                        return BadRequest("Invalid date");
                    }
                    // is date in the future?
                    if (toDate > DateTime.Now)
                    {
                        return BadRequest("Date cannot be in the future");
                    }
                    toDate = parsedDate;
                }

                if (string.IsNullOrEmpty(bondId))
                {
                    return BadRequest("BondId cannot be null or empty");
                }


                using (var context = new QuotationsBoardContext())
                {
                    LoginTokenDTO TokenContents = UtilityService.GetUserIdFromCurrentRequest(Request);
                    var userId = UtilityService.GetUserIdFromToken(Request);
                    var quotations = await context.Quotations.Include(x => x.Institution)
                        .Where(q =>
                         q.InstitutionId == TokenContents.InstitutionId
                         && q.BondId == bondId
                         && q.CreatedAt.Date >= fromDate.Date
                         && q.CreatedAt.Date <= toDate.Date

                         ).ToListAsync();
                    QuotationDTO sample = new();
                    //List<QuotationDTO> quotationDTOs = new List<QuotationDTO>();
                    List<Quoteinfo> quoteinfos = new List<Quoteinfo>();
                    foreach (var quotation in quotations)
                    {
                        var institution = await context.Institutions.FirstOrDefaultAsync(i => i.Id == quotation.InstitutionId);
                        var user = await context.Users.FirstOrDefaultAsync(u => u.Id == quotation.UserId);
                        if (institution == null || user == null)
                        {
                            return BadRequest("Invalid institution or user");
                        }

                        var quotationDTO = new Quoteinfo
                        {
                            BondId = quotation.BondId,
                            TotalBuyYield = quotation.BuyingYield,
                            CreatedAt = quotation.CreatedAt,
                            InstitutionId = institution.OrganizationName,
                            TotalSellYield = quotation.SellingYield,
                            UserId = user.FirstName + " " + user.LastName,
                            TotalBuyVolume = quotation.BuyVolume,
                            TotalSellVolume = quotation.SellVolume,
                            Id = quotation.Id
                        };
                        quoteinfos.Add(quotationDTO);
                    }

                    // quotationDTOs.Add(new QuotationDTO
                    // {
                    //     Quotes = quoteinfos
                    // });

                    sample.Quotes = quoteinfos;

                    if (quoteinfos.Count > 0)
                    {
                        // Calculate the total buying yield, total selling yield, average buy yield, average sell yield and average yield
                        var totalBuyingYield = quoteinfos.Sum(x => x.TotalBuyYield);
                        var totalSellingYield = quoteinfos.Sum(x => x.TotalSellYield);
                        var averageBuyYield = quoteinfos.Average(x => x.TotalBuyYield);
                        var averageSellYield = quoteinfos.Average(x => x.TotalSellYield);
                        var weightedSellingYield = quoteinfos.Sum(x => x.TotalSellYield * x.TotalSellVolume) / quoteinfos.Sum(x => x.TotalSellVolume);
                        var weightedBuyingYield = quoteinfos.Sum(x => x.TotalBuyYield * x.TotalBuyVolume) / quoteinfos.Sum(x => x.TotalBuyVolume);
                        var averageWeightedYield = (weightedBuyingYield + weightedSellingYield) / 2;
                        var averageYield = averageWeightedYield; //quoteinfos.Average(x => (x.TotalBuyYield + x.TotalSellYield) / 2);

                        QuoteStatistic quoteStatistic = new QuoteStatistic
                        {
                            AverageBuyYield = averageBuyYield,
                            AverageSellYield = averageSellYield,
                            AverageYield = averageYield,
                            TotalBuyingYield = totalBuyingYield,
                            TotalSellingYield = totalSellingYield,
                            TotalQuotations = quoteinfos.Count,
                            TotalBuyVolume = quoteinfos.Sum(x => x.TotalBuyVolume),
                            TotalSellVolume = quoteinfos.Sum(x => x.TotalSellVolume)
                        };

                        // Add the quote statistic to the quotation DTO
                        //quotationDTOs[0].QuoteStatistic = quoteStatistic;
                        sample.QuoteStatistic = quoteStatistic;
                    }


                    return StatusCode(200, sample);
                }

            }
            catch (Exception Ex)
            {
                UtilityService.LogException(Ex);
                return StatusCode(500, UtilityService.HandleException(Ex));
            }
        }

        // Fetch all quotations filled by Institution no Bond Provided just from and To
        [HttpGet("GetAllQuotationsFilledByInstitution/{From}/{To}")]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        public async Task<ActionResult<QuotationDTO>> GetAllQuotationsFilledByInstitution(string? From = "default", string? To = "default")
        {
            try
            {
                DateTime fromDate = DateTime.Now;
                DateTime toDate = DateTime.Now;
                if (From == "default")
                {
                    fromDate = DateTime.Now;
                }
                else
                {
                    var parsedDate = DateTime.Parse(From);
                    // is date valid?
                    if (fromDate == DateTime.MinValue)
                    {
                        return BadRequest("Invalid date");
                    }
                    // is date in the future?
                    if (fromDate > DateTime.Now)
                    {
                        return BadRequest("Date cannot be in the future");
                    }
                    fromDate = parsedDate;
                }

                if (To == "default")
                {
                    toDate = DateTime.Now;
                }
                else
                {
                    var parsedDate = DateTime.Parse(To);
                    // is date valid?
                    if (toDate == DateTime.MinValue)
                    {
                        return BadRequest("Invalid date");
                    }
                    // is date in the future?
                    if (toDate > DateTime.Now)
                    {
                        return BadRequest("Date cannot be in the future");
                    }
                    toDate = parsedDate;
                }

                using (var context = new QuotationsBoardContext())
                {
                    LoginTokenDTO TokenContents = UtilityService.GetUserIdFromCurrentRequest(Request);
                    // Check if InstutionName Nairobi Security Exchange if it is then return all quotations no need to filter by institution
                    Institution? inst = await context.Institutions.FirstOrDefaultAsync(i => i.Id == TokenContents.InstitutionId);
                    var userId = UtilityService.GetUserIdFromToken(Request);
                    var quotations = null as List<Quotation>;

                    if (inst != null && inst.OrganizationName == "Nairobi Security Exchange")
                    {
                        quotations = await context.Quotations.Include(x => x.Institution)
                       .Where(q =>
                        q.CreatedAt.Date >= fromDate.Date
                        && q.CreatedAt.Date <= toDate.Date
                        ).ToListAsync();
                    }
                    else
                    {
                        quotations = await context.Quotations.Include(x => x.Institution)
                       .Where(q =>
                        q.InstitutionId == TokenContents.InstitutionId
                        && q.CreatedAt.Date >= fromDate.Date
                        && q.CreatedAt.Date <= toDate.Date
                        ).ToListAsync();
                    }

                    QuotationDTO sample = new();
                    //List<QuotationDTO> quotationDTOs = new List<QuotationDTO>();
                    List<Quoteinfo> quoteinfos = new List<Quoteinfo>();
                    foreach (var quotation in quotations)
                    {
                        var institution = await context.Institutions.FirstOrDefaultAsync(i => i.Id == quotation.InstitutionId);
                        var user = await context.Users.FirstOrDefaultAsync(u => u.Id == quotation.UserId);
                        var bond = await context.Bonds.FirstOrDefaultAsync(b => b.Id == quotation.BondId);
                        if (bond == null)
                        {
                            return BadRequest("Invalid Bond");
                        }
                        if (institution == null || user == null)
                        {
                            return BadRequest("Invalid institution or user");
                        }

                        var quotationDTO = new Quoteinfo
                        {
                            BondId = quotation.BondId,
                            IssueNumber = bond.IssueNumber,
                            BondIsin = bond.Isin,
                            TotalBuyYield = quotation.BuyingYield,
                            CreatedAt = quotation.CreatedAt,
                            InstitutionId = institution.OrganizationName,
                            TotalSellYield = quotation.SellingYield,
                            UserId = user.FirstName + " " + user.LastName,
                            TotalBuyVolume = quotation.BuyVolume,
                            TotalSellVolume = quotation.SellVolume,
                            Id = quotation.Id,
                            AverageVolume = (quotation.BuyVolume + quotation.SellVolume) / 2,
                            AverageYield = (quotation.BuyingYield + quotation.SellingYield) / 2
                        };
                        quoteinfos.Add(quotationDTO);
                    }
                    sample.Quotes = quoteinfos;

                    return StatusCode(200, sample);
                }

            }
            catch (Exception Ex)
            {
                UtilityService.LogException(Ex);
                return StatusCode(500, UtilityService.HandleException(Ex));
            }
        }

        // Gets all quotations Totals Grouped by Bond
        [HttpGet("GetAllQuotationTotals/{From}/{To}")]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        public async Task<ActionResult<QuotationDTO>> GetAllQuotationsAveragesAndTotalsFilledByInstitutions(string? From = "default", string? To = "default")
        {
            try
            {
                DateTime fromDate = DateTime.Now;
                DateTime toDate = DateTime.Now;
                if (From == "default")
                {
                    fromDate = DateTime.Now;
                }
                else
                {
                    var parsedDate = DateTime.Parse(From);
                    // is date valid?
                    if (fromDate == DateTime.MinValue)
                    {
                        return BadRequest("Invalid date");
                    }
                    // is date in the future?
                    if (fromDate > DateTime.Now)
                    {
                        return BadRequest("Date cannot be in the future");
                    }
                    fromDate = parsedDate;
                }

                if (To == "default")
                {
                    toDate = DateTime.Now;
                }
                else
                {
                    var parsedDate = DateTime.Parse(To);
                    // is date valid?
                    if (toDate == DateTime.MinValue)
                    {
                        return BadRequest("Invalid date");
                    }
                    // is date in the future?
                    if (toDate > DateTime.Now)
                    {
                        return BadRequest("Date cannot be in the future");
                    }
                    toDate = parsedDate;
                }

                using (var context = new QuotationsBoardContext())
                {
                    LoginTokenDTO TokenContents = UtilityService.GetUserIdFromCurrentRequest(Request);
                    // Check if InstutionName Nairobi Security Exchange if it is then return all quotations no need to filter by institution
                    Institution? inst = await context.Institutions.FirstOrDefaultAsync(i => i.Id == TokenContents.InstitutionId);
                    var userId = UtilityService.GetUserIdFromToken(Request);
                    var quotations = null as List<Quotation>;

                    if (inst != null && inst.OrganizationName == "Nairobi Security Exchange")
                    {
                        quotations = await context.Quotations.Include(x => x.Institution)
                       .Where(q =>
                        q.CreatedAt.Date >= fromDate.Date
                        && q.CreatedAt.Date <= toDate.Date
                        ).ToListAsync();
                    }
                    else
                    {
                        quotations = await context.Quotations.Include(x => x.Institution)
                       .Where(q =>
                        q.InstitutionId == TokenContents.InstitutionId
                        && q.CreatedAt.Date >= fromDate.Date
                        && q.CreatedAt.Date <= toDate.Date
                        ).ToListAsync();
                    }

                    QuotationDTO sample = new();
                    //List<QuotationDTO> quotationDTOs = new List<QuotationDTO>();
                    List<Quoteinfo> quoteinfos = new List<Quoteinfo>();

                    var quotationsGroupedByBond = quotations.GroupBy(x => x.BondId).Select(
                         (g => new
                         {
                             BondId = g.Key,
                             WeightedAverageBuyYield = g.Sum(q => q.BuyingYield * q.BuyVolume) / g.Sum(q => q.BuyVolume),
                             WeightedAverageSellYield = g.Sum(q => q.SellingYield * q.SellVolume) / g.Sum(q => q.SellVolume),
                             AverageWeightedYield = (g.Sum(q => q.BuyingYield * q.BuyVolume) / g.Sum(q => q.BuyVolume) + g.Sum(q => q.SellingYield * q.SellVolume) / g.Sum(q => q.SellVolume)) / 2,
                             AverageBuyYield = g.Average(q => q.BuyingYield),
                             AverageSellYield = g.Average(q => q.SellingYield),
                             TotalQuotations = g.Count(),
                             CombinedAverageYield = g.Average(q => (q.BuyingYield + q.SellingYield)),
                             TotalBuyVolume = g.Sum(q => q.BuyVolume),
                             TotalSellVolume = g.Sum(q => q.SellVolume),
                             TotalBuyYield = g.Sum(q => q.BuyingYield),
                             TotalSellYield = g.Sum(q => q.SellingYield),
                         })
                    ).ToList();

                    foreach (var quotation in quotationsGroupedByBond)
                    {
                        Bond? bd = await context.Bonds.FirstOrDefaultAsync(b => b.Id == quotation.BondId);
                        if (bd == null)
                        {
                            return BadRequest("Invalid Bond");
                        }
                        var quotationDTO = new Quoteinfo
                        {
                            BondId = quotation.BondId,
                            BondIsin = bd.Isin,
                            IssueNumber = bd.IssueNumber,
                            TotalBuyVolume = quotation.TotalBuyVolume,
                            TotalBuyYield = quotation.TotalBuyYield,
                            TotalSellVolume = quotation.TotalSellVolume,
                            TotalSellYield = quotation.TotalSellYield,
                            AverageYield = quotation.AverageWeightedYield,//(quotation.AverageSellYield + quotation.AverageBuyYield),
                            AverageVolume = (quotation.TotalBuyVolume + quotation.TotalSellVolume) / 2,
                            Id = quotation.BondId,
                        };
                        quoteinfos.Add(quotationDTO);
                    }
                    sample.Quotes = quoteinfos;
                    return StatusCode(200, sample);
                }

            }
            catch (Exception Ex)
            {
                UtilityService.LogException(Ex);
                return StatusCode(500, UtilityService.HandleException(Ex));
            }
        }

        // Gets all quotations Averages Grouped by Bond
        [HttpGet("GetAllQuotationAverages/{From}/{To}")]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        public async Task<ActionResult<QuotationAverages>> GetAllQuotationsAveragesFilledByInstitutions(string? From = "default", string? To = "default")
        {
            try
            {
                DateTime fromDate = DateTime.Now;
                DateTime toDate = DateTime.Now;
                if (From == "default")
                {
                    fromDate = DateTime.Now;
                }
                else
                {
                    var parsedDate = DateTime.Parse(From);
                    // is date valid?
                    if (fromDate == DateTime.MinValue)
                    {
                        return BadRequest("Invalid date");
                    }
                    // is date in the future?
                    if (fromDate > DateTime.Now)
                    {
                        return BadRequest("Date cannot be in the future");
                    }
                    fromDate = parsedDate;
                }

                if (To == "default")
                {
                    toDate = DateTime.Now;
                }
                else
                {
                    var parsedDate = DateTime.Parse(To);
                    // is date valid?
                    if (toDate == DateTime.MinValue)
                    {
                        return BadRequest("Invalid date");
                    }
                    // is date in the future?
                    if (toDate > DateTime.Now)
                    {
                        return BadRequest("Date cannot be in the future");
                    }
                    toDate = parsedDate;
                }

                using (var context = new QuotationsBoardContext())
                {
                    LoginTokenDTO TokenContents = UtilityService.GetUserIdFromCurrentRequest(Request);
                    // Check if InstutionName Nairobi Security Exchange if it is then return all quotations no need to filter by institution
                    Institution? inst = await context.Institutions.FirstOrDefaultAsync(i => i.Id == TokenContents.InstitutionId);
                    var userId = UtilityService.GetUserIdFromToken(Request);
                    var quotations = null as List<Quotation>;

                    if (inst != null && inst.OrganizationName == "Nairobi Security Exchange")
                    {
                        quotations = await context.Quotations.Include(x => x.Institution)
                       .Where(q =>
                        q.CreatedAt.Date >= fromDate.Date
                        && q.CreatedAt.Date <= toDate.Date
                        ).ToListAsync();
                    }
                    else
                    {
                        quotations = await context.Quotations.Include(x => x.Institution)
                       .Where(q =>
                        // q.InstitutionId == TokenContents.InstitutionId &&
                        q.CreatedAt.Date >= fromDate.Date
                        && q.CreatedAt.Date <= toDate.Date
                        ).ToListAsync();
                    }

                    QuotationAverages sample = new();
                    //List<QuotationDTO> quotationDTOs = new List<QuotationDTO>();
                    List<QuoteAverageData> quoteaverages = new List<QuoteAverageData>();

                    var quotationsGroupedByBond = quotations.GroupBy(x => x.BondId).Select(
                         (g => new
                         {
                             BondId = g.Key,
                             WeightedAverageBuyYield = g.Sum(q => q.BuyingYield * q.BuyVolume) / g.Sum(q => q.BuyVolume),
                             WeightedAverageSellYield = g.Sum(q => q.SellingYield * q.SellVolume) / g.Sum(q => q.SellVolume),
                             AverageWeightedYield = (g.Sum(q => q.BuyingYield * q.BuyVolume) / g.Sum(q => q.BuyVolume) + g.Sum(q => q.SellingYield * q.SellVolume) / g.Sum(q => q.SellVolume)) / 2,
                             AverageBuyYield = g.Average(q => q.BuyingYield),
                             AverageSellYield = g.Average(q => q.SellingYield),
                             TotalQuotations = g.Count(),
                             CombinedAverageYield = g.Average(q => (q.BuyingYield + q.SellingYield) / 2),
                             TotalBuyVolume = g.Sum(q => q.BuyVolume),
                             TotalSellVolume = g.Sum(q => q.SellVolume),
                             TotalBuyYield = g.Sum(q => q.BuyingYield),
                             TotalSellYield = g.Sum(q => q.SellingYield),
                             AverageSellVolume = g.Average(q => q.SellVolume),
                             AverageBuyVolume = g.Average(q => q.BuyVolume),
                             AverageVolume = g.Average(q => (q.BuyVolume + q.SellVolume) / 2),
                         })
                    ).ToList();

                    foreach (var quotation in quotationsGroupedByBond)
                    {
                        Bond? bd = await context.Bonds.FirstOrDefaultAsync(b => b.Id == quotation.BondId);
                        if (bd == null)
                        {
                            return BadRequest("Invalid Bond");
                        }
                        var quotationDTO = new QuoteAverageData
                        {
                            BondId = quotation.BondId,
                            BondIsin = bd.Isin,
                            IssueNumber = bd.IssueNumber,
                            AverageBuyYield = quotation.WeightedAverageBuyYield,
                            AverageSellYield = quotation.WeightedAverageSellYield,
                            AverageYield = quotation.AverageWeightedYield, // quotation.CombinedAverageYield,
                            AverageVolume = quotation.AverageVolume,
                            AverageSellVolume = quotation.AverageSellVolume,
                            AverageBuyVolume = quotation.AverageBuyVolume,
                        };
                        quoteaverages.Add(quotationDTO);
                    }
                    sample.Averages = quoteaverages;
                    return StatusCode(200, sample);
                }

            }
            catch (Exception Ex)
            {
                UtilityService.LogException(Ex);
                return StatusCode(500, UtilityService.HandleException(Ex));
            }
        }


        // Fetch Quaotes Filled by a Specific user
        [HttpGet("GetQuotationsFilledByUser/{From}/{To}")]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        public async Task<ActionResult<QuotationDTO>> GetQuotationsFilledByUser(string? From = "default", string? To = "default")
        {
            try
            {
                DateTime fromDate = DateTime.Now;
                DateTime toDate = DateTime.Now;
                if (From == "default")
                {
                    fromDate = DateTime.Now;
                }
                else
                {
                    var parsedDate = DateTime.Parse(From);
                    // is date valid?
                    if (fromDate == DateTime.MinValue)
                    {
                        return BadRequest("Invalid date");
                    }
                    // is date in the future?
                    if (fromDate > DateTime.Now)
                    {
                        return BadRequest("Date cannot be in the future");
                    }
                    fromDate = parsedDate;
                }

                if (To == "default")
                {
                    toDate = DateTime.Now;
                }
                else
                {
                    var parsedDate = DateTime.Parse(To);
                    // is date valid?
                    if (toDate == DateTime.MinValue)
                    {
                        return BadRequest("Invalid date");
                    }
                    // is date in the future?
                    if (toDate > DateTime.Now)
                    {
                        return BadRequest("Date cannot be in the future");
                    }
                    toDate = parsedDate;
                }

                using (var context = new QuotationsBoardContext())
                {
                    LoginTokenDTO TokenContents = UtilityService.GetUserIdFromCurrentRequest(Request);
                    var userId = UtilityService.GetUserIdFromToken(Request);
                    var quotations = await context.Quotations.Include(x => x.Institution).Where(q =>
                     q.UserId == userId
                      && q.CreatedAt.Date >= fromDate.Date
                        && q.CreatedAt.Date <= toDate.Date
                      )
                      .ToListAsync();
                    //List<QuotationDTO> quotationDTOs = new List<QuotationDTO>();
                    QuotationDTO sample = new();
                    List<Quoteinfo> quoteinfos = new List<Quoteinfo>();
                    foreach (var quotation in quotations)
                    {
                        var institution = await context.Institutions.FirstOrDefaultAsync(i => i.Id == quotation.InstitutionId);
                        var user = await context.Users.FirstOrDefaultAsync(u => u.Id == quotation.UserId);
                        if (institution == null || user == null)
                        {
                            return BadRequest("Invalid institution or user");
                        }

                        var quotationDTO = new Quoteinfo
                        {
                            BondId = quotation.BondId,
                            TotalBuyYield = quotation.BuyingYield,
                            CreatedAt = quotation.CreatedAt,
                            InstitutionId = institution.OrganizationName,
                            TotalSellYield = quotation.SellingYield,
                            UserId = user.FirstName + " " + user.LastName,
                            TotalBuyVolume = quotation.BuyVolume,
                            TotalSellVolume = quotation.SellVolume,
                            Id = quotation.Id
                        };
                        quoteinfos.Add(quotationDTO);
                    }
                    // quotationDTOs.Add(new QuotationDTO
                    // {
                    //     Quotes = quoteinfos
                    // });

                    sample.Quotes = quoteinfos;

                    if (quoteinfos.Count > 0)
                    {
                        // Calculate the total buying yield, total selling yield, average buy yield, average sell yield and average yield

                        var totalBuyingYield = quoteinfos.Sum(x => x.TotalBuyYield);
                        var totalSellingYield = quoteinfos.Sum(x => x.TotalSellYield);
                        var weightedBuyingYield = quoteinfos.Sum(x => x.TotalBuyYield * x.TotalBuyVolume) / quoteinfos.Sum(x => x.TotalBuyVolume);
                        var weightedSellingYield = quoteinfos.Sum(x => x.TotalSellYield * x.TotalSellVolume) / quoteinfos.Sum(x => x.TotalSellVolume);
                        var averageWeightedYield = (weightedBuyingYield + weightedSellingYield) / 2;
                        var averageBuyYield = quoteinfos.Average(x => x.TotalBuyYield);
                        var averageSellYield = quoteinfos.Average(x => x.TotalSellYield);
                        var averageYield = quoteinfos.Average(x => (x.TotalBuyYield + x.TotalSellYield) / 2);

                        QuoteStatistic quoteStatistic = new QuoteStatistic
                        {
                            AverageBuyYield = weightedBuyingYield,
                            AverageSellYield = weightedSellingYield,
                            AverageYield = averageWeightedYield, //averageYield,
                            TotalBuyingYield = totalBuyingYield,
                            TotalSellingYield = totalSellingYield,
                            TotalQuotations = quoteinfos.Count,
                            TotalBuyVolume = quoteinfos.Sum(x => x.TotalBuyVolume),
                            TotalSellVolume = quoteinfos.Sum(x => x.TotalSellVolume)
                        };

                        // Add the quote statistic to the quotation DTO
                        //quotationDTOs[0].QuoteStatistic = quoteStatistic;
                        sample.QuoteStatistic = quoteStatistic;

                    }

                    return StatusCode(200, sample);

                }

            }
            catch (Exception Ex)
            {
                UtilityService.LogException(Ex);
                return StatusCode(500, UtilityService.HandleException(Ex));
            }
        }

        // Quotations For a Specific Bond
        [HttpGet("GetQuotationsForBond/{bondId}/{From}/{To}")]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        public async Task<ActionResult<List<QuotationDTO>>> GetQuotationsForBond(string bondId, string? From = "default", string To = "default")
        {
            try
            {
                DateTime fromDate = DateTime.Now;
                DateTime toDate = DateTime.Now;
                if (From == "default")
                {
                    fromDate = DateTime.Now;
                }
                else
                {
                    var parsedDate = DateTime.Parse(From);
                    // is date valid?
                    if (fromDate == DateTime.MinValue)
                    {
                        return BadRequest("Invalid date");
                    }
                    // is date in the future?
                    if (fromDate > DateTime.Now)
                    {
                        return BadRequest("Date cannot be in the future");
                    }
                    fromDate = parsedDate;
                }

                if (To == "default")
                {
                    toDate = DateTime.Now;
                }
                else
                {
                    var parsedDate = DateTime.Parse(To);
                    // is date valid?
                    if (toDate == DateTime.MinValue)
                    {
                        return BadRequest("Invalid date");
                    }
                    // is date in the future?
                    if (toDate > DateTime.Now)
                    {
                        return BadRequest("Date cannot be in the future");
                    }
                    toDate = parsedDate;
                }


                using (var context = new QuotationsBoardContext())
                {
                    LoginTokenDTO TokenContents = UtilityService.GetUserIdFromCurrentRequest(Request);
                    var userId = UtilityService.GetUserIdFromToken(Request);
                    var quotations = await context.Quotations.Include(x => x.Institution).Where(
                        q => q.BondId == bondId
                        && q.CreatedAt.Date >= fromDate.Date
                        && q.CreatedAt.Date <= toDate.Date
                         ).ToListAsync();

                    //List<QuotationDTO> quotationDTOs = new List<QuotationDTO>();
                    QuotationDTO sample = new();
                    List<Quoteinfo> quoteinfos = new List<Quoteinfo>();

                    foreach (var quotation in quotations)
                    {
                        // Find the institution name and user that created the quotation
                        var institution = await context.Institutions.FirstOrDefaultAsync(i => i.Id == quotation.InstitutionId);
                        var user = await context.Users.FirstOrDefaultAsync(u => u.Id == quotation.UserId);
                        if (institution == null || user == null)
                        {
                            return BadRequest("Invalid institution or user");
                        }
                        var quotationInfo = new Quoteinfo
                        {
                            BondId = quotation.BondId,
                            TotalBuyYield = quotation.BuyingYield,
                            CreatedAt = quotation.CreatedAt,
                            InstitutionId = institution.OrganizationName,
                            TotalSellYield = quotation.SellingYield,
                            UserId = user.FirstName + " " + user.LastName,
                            TotalBuyVolume = quotation.BuyVolume,
                            TotalSellVolume = quotation.SellVolume,
                            Id = quotation.Id
                        };
                        quoteinfos.Add(quotationInfo);
                    }

                    // quotationDTOs.Add(new QuotationDTO
                    // {
                    //     Quotes = quoteinfos
                    // });
                    sample.Quotes = quoteinfos;
                    // Calculate the total buying yield, total selling yield, average buy yield, average sell yield and average yield
                    if (quoteinfos.Count > 0)
                    {
                        var totalBuyingYield = quoteinfos.Sum(x => x.TotalBuyYield);
                        var totalSellingYield = quoteinfos.Sum(x => x.TotalSellYield);
                        var weightedBuyingYield = quoteinfos.Sum(x => x.TotalBuyYield * x.TotalBuyVolume) / quoteinfos.Sum(x => x.TotalBuyVolume);
                        var weightedSellingYield = quoteinfos.Sum(x => x.TotalSellYield * x.TotalSellVolume) / quoteinfos.Sum(x => x.TotalSellVolume);
                        var averageWeightedYield = (weightedBuyingYield + weightedSellingYield) / 2;
                        var averageBuyYield = quoteinfos.Average(x => x.TotalBuyYield);
                        var averageSellYield = quoteinfos.Average(x => x.TotalSellYield);
                        var averageYield = quoteinfos.Average(x => (x.TotalBuyYield + x.TotalSellYield) / 2);

                        QuoteStatistic quoteStatistic = new QuoteStatistic
                        {
                            AverageBuyYield = weightedBuyingYield, //averageBuyYield,
                            AverageSellYield = weightedSellingYield, //averageSellYield,
                            AverageYield = averageWeightedYield, //averageYield,
                            TotalBuyingYield = totalBuyingYield,
                            TotalSellingYield = totalSellingYield,
                            TotalQuotations = quoteinfos.Count,
                            TotalBuyVolume = quoteinfos.Sum(x => x.TotalBuyVolume),
                            TotalSellVolume = quoteinfos.Sum(x => x.TotalSellVolume)
                        };

                        // Add the quote statistic to the quotation DTO
                        //quotationDTOs[0].QuoteStatistic = quoteStatistic;
                        sample.QuoteStatistic = quoteStatistic;
                    }


                    return StatusCode(200, sample);
                }

            }
            catch (Exception Ex)
            {
                UtilityService.LogException(Ex);
                return StatusCode(500, UtilityService.HandleException(Ex));
            }
        }

        // Yield Curve for a Specific Bond
        [HttpGet("GetYieldCurveForBond/{bondId}/{From}/{To}")]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        public async Task<ActionResult<List<YieldCurveDTO>>> GetYieldCurveForBond(string bondId, string? From = "default", string? To = "default")
        {
            try
            {
                DateTime fromDate = DateTime.Now;
                DateTime toDate = DateTime.Now;
                if (From == "default")
                {
                    fromDate = DateTime.Now;
                }
                else
                {
                    var parsedDate = DateTime.Parse(From);
                    // is date valid?
                    if (fromDate == DateTime.MinValue)
                    {
                        return BadRequest("Invalid date");
                    }
                    // is date in the future?
                    if (fromDate > DateTime.Now)
                    {
                        return BadRequest("Date cannot be in the future");
                    }
                    fromDate = parsedDate;
                }

                if (To == "default")
                {
                    toDate = DateTime.Now;
                }
                else
                {
                    var parsedDate = DateTime.Parse(To);
                    // is date valid?
                    if (toDate == DateTime.MinValue)
                    {
                        return BadRequest("Invalid date");
                    }
                    // is date in the future?
                    if (toDate > DateTime.Now)
                    {
                        return BadRequest("Date cannot be in the future");
                    }
                    toDate = parsedDate;
                }

                using (var context = new QuotationsBoardContext())
                {
                    LoginTokenDTO TokenContents = UtilityService.GetUserIdFromCurrentRequest(Request);
                    var userId = UtilityService.GetUserIdFromToken(Request);
                    var quotations = await context.Quotations.Include(x => x.Institution).Where(q => q.BondId == bondId
                    && q.CreatedAt.Date >= fromDate.Date
                    && q.CreatedAt.Date <= toDate.Date
                    ).ToListAsync();
                    var dailyAverages = quotations.GroupBy(x => x.CreatedAt.Date).Select(
                         (g => new
                         {
                             BondId = g.Key,
                             Date = g.Key.Date,
                             AverageBuyYield = g.Average(q => q.BuyingYield),
                             AverageSellYield = g.Average(q => q.SellingYield),
                             TotalQuotations = g.Count(),
                             CombinedAverageYield = g.Average(q => (q.BuyingYield + q.SellingYield) / 2),
                         })
                    ).ToList();

                    var yieldCurveData = dailyAverages.Select(x => new YieldCurveDTO
                    {
                        BondId = bondId,
                        Date = x.Date,
                        Yield = x.CombinedAverageYield,
                        TotalQuotationsUsed = x.TotalQuotations,
                        AverageBuyYield = x.AverageBuyYield,
                        AverageSellYield = x.AverageSellYield
                    }).ToList();

                    return StatusCode(200, yieldCurveData);
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
