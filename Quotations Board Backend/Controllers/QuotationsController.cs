﻿using AutoMapper;
using ClosedXML.Excel;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Quotations_Board_Backend.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    [Authorize(Roles = $"{CustomRoles.Dealer}, {CustomRoles.ChiefDealer}, {CustomRoles.InstitutionAdmin}, {CustomRoles.SuperAdmin}", AuthenticationSchemes = "Bearer")]

    public class QuotationsController : ControllerBase
    {
        private readonly UserManager<PortalUser> _userManager;
        private readonly IConfiguration _configuration;


        public QuotationsController(UserManager<PortalUser> userManager, IConfiguration configuration)
        {
            _userManager = userManager;
            _configuration = configuration;
        }

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
                    // get the bond details
                    var bond = await context.Bonds.FirstOrDefaultAsync(b => b.Id == newQuotation.BondId);
                    if (bond == null)
                    {
                        return BadRequest("Invalid bond");
                    }
                    // ensure bond is not matured
                    if (bond.MaturityDate < DateTime.Now)
                    {
                        return BadRequest("Bond has matured");
                    }
                    // ensure bond is an FXD
                    if (bond.BondCategory != BondCategories.FXD)
                    {
                        return BadRequest("Only FXD bonds are allowed to be quoted");
                    }
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
                    // var percentageDifference = Math.Round((difference / quotation.BuyingYield) * 100, 2);
                    if (difference > 1)
                    {
                        return BadRequest("The difference between selling yield and buying yield divided by Buying Yield, cannot be greater than 1%. The current difference is " + difference + "%");
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
                        var LastImpliedYield = context.Quotations
                                                    .Where(q => q.BondId == quotation.BondId && q.CreatedAt.Date == mostRecentTradingDay.Date)
                                                    .OrderByDescending(q => q.CreatedAt)
                                                    .Select(q => q.BuyingYield)
                                                    .FirstOrDefault();
                        if (LastImpliedYield == default(decimal))
                        {
                            // Save the quotation
                            await context.Quotations.AddAsync(quotation);
                            await context.SaveChangesAsync();
                            return StatusCode(201, quotation);
                        }

                        decimal currentTotalWeightedYield = (quotation.BuyingYield * quotation.BuyVolume) + (quotation.SellingYield * quotation.SellVolume);
                        decimal currentQuotationVolume = quotation.BuyVolume + quotation.SellVolume;
                        decimal currentAverageWeightedYield = currentTotalWeightedYield / currentQuotationVolume;
                        var change = Math.Abs(currentAverageWeightedYield - LastImpliedYield);

                        if (change > 1)
                        {
                            return BadRequest("Quotation rejected. The current average weighted yield significantly differs from the last implied yield recorded on " + mostRecentTradingDay + ". The percentage change of " + change + "% exceeds the allowable limit of 1%");
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

                    if (difference > 1)
                    {
                        throw new Exception($"The difference between selling yield and buying yield cannot be greater than 1%. The current difference is {difference}% check on row {row}");
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
                        var LastImpliedYield = dbContext.Quotations
                            .Where(q => q.BondId == bond.Id && q.CreatedAt.Date == mostRecentTradingDay.Date)
                            .OrderByDescending(q => q.CreatedAt)
                            .Select(q => q.BuyingYield)
                            .FirstOrDefault();
                        if (LastImpliedYield == default(decimal))
                        {
                            quotationRows.Add(quote);
                            continue;
                        }
                        decimal currentTotalWeightedYield = (buyYield * buyVolume) + (sellYield * sellVolume);
                        decimal currentQuotationVolume = buyVolume + sellVolume;
                        decimal currentAverageWeightedYield = currentTotalWeightedYield / currentQuotationVolume;
                        var change = Math.Abs(currentAverageWeightedYield - LastImpliedYield);
                        // var percentgeChange = (change / averageRecentWeightedYield) * 100;
                        // if greater than 1% reject the quotation
                        if (change > 1)
                        {
                            throw new Exception($"Quotation at row {row} rejected. The current average weighted yield ({currentAverageWeightedYield:0.##}%) significantly differs from the last implied yield ({LastImpliedYield:0.##}%) recorded on {mostRecentTradingDay:yyyy-MM-dd}. The percentage change of {change:0.##}% exceeds the allowable limit of 1%.");
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
                    var bondExists = dbContext.Bonds.FirstOrDefault(b => b.IssueNumber == bondId);
                    if (bondExists == null)
                    {
                        errors.Add($"Row {rowToBeginAt}: Bond ID '{bondId}' does not exist in the system.");
                        continue;
                    }

                    // ensure bond is not matured
                    if (bondExists.MaturityDate < DateTime.Now)
                    {
                        errors.Add($"Row {rowToBeginAt}: Bond ID '{bondId}' has matured.");
                    }
                    // ensure bond is an FXD
                    if (bondExists.BondCategory != BondCategories.FXD)
                    {
                        errors.Add($"Row {rowToBeginAt}: Only FXD bonds are allowed to be quoted.");
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
                    var userDetails = await context.Users.FirstOrDefaultAsync(u => u.Id == userId);

                    if (userDetails == null)
                    {
                        return BadRequest("Invalid user");
                    }


                    if (existingQuotation == null)
                    {
                        return BadRequest("Quotation does not exist");
                    }
                    var bond = await context.Bonds.FirstOrDefaultAsync(b => b.Id == existingQuotation.BondId);
                    if (bond == null)
                    {
                        return BadRequest("Invalid bond");
                    }

                    Institution? institution = await context.Institutions.FirstOrDefaultAsync(i => i.Id == existingQuotation.InstitutionId);
                    if (institution == null)
                    {
                        return BadRequest("Invalid institution");
                    }

                    // Ensure no quotation edit has been made for this quotation that has not been approved or rejected
                    var existingQuotationEdit = await context.QuotationEdits.FirstOrDefaultAsync(q => q.QuotationId == existingQuotation.Id && q.Status == QuotationEditStatus.Pending);
                    if (existingQuotationEdit != null)
                    {
                        return BadRequest("A quotation edit has already been made for this quotation that has not been approved or rejected");
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
                    // var percentageDifference = (difference / editQuotation.BuyYield) * 100;
                    if (difference > 1)
                    {
                        return BadRequest("The difference between selling yield and buying yield cannot be greater than 1%. The current difference is " + difference + "%");
                    }

                    // Save in QuotationEdit
                    QuotationEdit quotationEdit = new QuotationEdit
                    {
                        BondId = existingQuotation.BondId,
                        BuyingYield = existingQuotation.BuyingYield,
                        BuyVolume = existingQuotation.BuyVolume,
                        SellingYield = existingQuotation.SellingYield,
                        SellVolume = existingQuotation.SellVolume,
                        CreatedAt = existingQuotation.CreatedAt,
                        InstitutionId = existingQuotation.InstitutionId,
                        UserId = existingQuotation.UserId,
                        QuotationId = existingQuotation.Id,
                        Status = QuotationEditStatus.Pending,
                        Comment = editQuotation.Comment
                    };

                    await context.QuotationEdits.AddAsync(quotationEdit);
                    await context.SaveChangesAsync();

                    var callbackUrl = $"{_configuration["FrontEndUrl"]}/review-quote/{quotationEdit.Id}";

                    // find users via roles
                    var superAdmins = await _userManager.GetUsersInRoleAsync(CustomRoles.SuperAdmin);
                    foreach (var superAdmin in superAdmins)
                    {
                        // send email to super admin about the quotation edit  that has been made and notify them to approve or reject
                        var emailBody = $"A quotation edit has been made by {userDetails.FirstName} {userDetails.LastName} of {institution.OrganizationName} on {DateTime.Today} for bond {bond.IssueNumber} with the following details: <br/>" +
                            $"Buying Yield: {existingQuotation.BuyingYield} <br/>" +
                            $"Selling Yield: {existingQuotation.SellingYield} <br/>" +
                            $"Buy Volume: {existingQuotation.BuyVolume} <br/>" +
                            $"Sell Volume: {existingQuotation.SellVolume} <br/>" +

                            // new proposed quotation
                            $"The new proposed quotation is as follows: <br/>" +
                            $"Buying Yield: {editQuotation.BuyYield} <br/>" +
                            $"Selling Yield: {editQuotation.SellYield} <br/>" +
                            $"Buy Volume: {editQuotation.BuyVolume} <br/>" +
                            $"Sell Volume: {editQuotation.SellVolume} <br/>" +
                            $"Comment: {editQuotation.Comment} <br/>" +

                            $"Please approve or reject the quotation  <br/>" +
                            $"Click <a href='{callbackUrl}'>here</a> to approve or reject the quotation edit <br/>";

                        var emailSubject = "Quotation Edit";
                        await UtilityService.SendEmailAsync(
                            //superAdmin.Email,
                            "jackline.njeri@agilebiz.co.ke",
                            emailSubject,
                            emailBody
                        );
                    }
                    return StatusCode(200, quotationEdit);
                }

            }
            catch (Exception Ex)
            {
                UtilityService.LogException(Ex);
                return StatusCode(500, UtilityService.HandleException(Ex));
            }
        }

        // Fetch details of a quotation edit given the quotation edit id
        [HttpGet("GetQuotationEditDetails/{QuotationEditId}")]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        public async Task<ActionResult<QuotationEdit>> GetQuotationEditDetails(string QuotationEditId)
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
                    var quotationEdit = await context.QuotationEdits.FirstOrDefaultAsync(q => q.Id == QuotationEditId);
                    if (quotationEdit == null)
                    {
                        return BadRequest("Quotation edit does not exist");
                    }
                    // is it approved or rejected?
                    if (quotationEdit.Status == QuotationEditStatus.Approved || quotationEdit.Status == QuotationEditStatus.Rejected)
                    {
                        return BadRequest("Quotation edit has already been approved or rejected");
                    }

                    var bond = await context.Bonds.FirstOrDefaultAsync(b => b.Id == quotationEdit.BondId);
                    if (bond == null)
                    {
                        return BadRequest("Invalid bond");
                    }

                    Institution? institution = await context.Institutions.FirstOrDefaultAsync(i => i.Id == quotationEdit.InstitutionId);
                    if (institution == null)
                    {
                        return BadRequest("Invalid institution");
                    }

                    var user = await context.Users.FirstOrDefaultAsync(u => u.Id == quotationEdit.UserId);
                    if (user == null)
                    {
                        return BadRequest("Invalid user");
                    }

                    var quotation = await context.Quotations.FirstOrDefaultAsync(q => q.Id == quotationEdit.QuotationId);
                    if (quotation == null)
                    {
                        return BadRequest("Invalid quotation");
                    }

                    var quotationEditDTO = new QuotationEditDTO
                    {
                        BondId = bond.Isin,
                        BuyYield = quotationEdit.BuyingYield,
                        BuyVolume = quotationEdit.BuyVolume,
                        SellYield = quotationEdit.SellingYield,
                        SellVolume = quotationEdit.SellVolume,
                        CreatedAt = quotationEdit.CreatedAt,
                        OrganizationName = institution.OrganizationName,
                        EditSubmittedBy = user.FirstName + " " + user.LastName,
                        QuotationId = quotationEdit.QuotationId,
                        Status = quotationEdit.Status,
                        Comment = quotationEdit.Comment ?? "",
                        Id = quotationEdit.Id,
                    };

                    return StatusCode(200, quotationEditDTO);
                }

            }
            catch (Exception Ex)
            {
                UtilityService.LogException(Ex);
                return StatusCode(500, UtilityService.HandleException(Ex));
            }

        }

        // Approve a quotation edit
        [HttpPost("ApproveQuotationEdit")]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [Authorize(Roles = CustomRoles.SuperAdmin, AuthenticationSchemes = "Bearer")]

        public async Task<ActionResult> ApproveQuotationEdit(ApproveQuotationEdit approveQuotationEdit)
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
                    var existingQuotationEdit = await context.QuotationEdits.FirstOrDefaultAsync(q => q.Id == approveQuotationEdit.Id);
                    var userDetails = await context.Users.FirstOrDefaultAsync(u => u.Id == userId);

                    if (userDetails == null)
                    {
                        return BadRequest("Invalid user");
                    }

                    if (existingQuotationEdit == null)
                    {
                        return BadRequest("Quotation edit does not exist");
                    }

                    var bond = await context.Bonds.FirstOrDefaultAsync(b => b.Id == existingQuotationEdit.BondId);
                    if (bond == null)
                    {
                        return BadRequest("Invalid bond");
                    }

                    Institution? institution = await context.Institutions.FirstOrDefaultAsync(i => i.Id == existingQuotationEdit.InstitutionId);
                    if (institution == null)
                    {
                        return BadRequest("Invalid institution");
                    }

                    // Ensure no quotation edit has been made for this quotation that has not been approved or rejected
                    Quotation? QuotationToUpdate = await context.Quotations.FirstOrDefaultAsync(q => q.Id == existingQuotationEdit.QuotationId);
                    if (QuotationToUpdate == null)
                    {
                        return BadRequest("Quotation does not exist");
                    }

                    QuotationToUpdate.BondId = existingQuotationEdit.BondId;
                    QuotationToUpdate.BuyingYield = existingQuotationEdit.BuyingYield;
                    QuotationToUpdate.BuyVolume = existingQuotationEdit.BuyVolume;
                    QuotationToUpdate.SellingYield = existingQuotationEdit.SellingYield;
                    QuotationToUpdate.SellVolume = existingQuotationEdit.SellVolume;
                    QuotationToUpdate.CreatedAt = existingQuotationEdit.CreatedAt;
                    QuotationToUpdate.InstitutionId = existingQuotationEdit.InstitutionId;
                    QuotationToUpdate.UserId = existingQuotationEdit.UserId;
                    QuotationToUpdate.UpdatedAt = DateTime.Now;

                    await context.SaveChangesAsync();

                    return StatusCode(200);

                }

            }
            catch (Exception Ex)
            {
                UtilityService.LogException(Ex);
                return StatusCode(500, UtilityService.HandleException(Ex));
            }

        }

        // Reject a quotation edit
        [HttpPost("RejectQuotationEdit")]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [Authorize(Roles = CustomRoles.SuperAdmin, AuthenticationSchemes = "Bearer")]

        public async Task<ActionResult> RejectQuotationEdit(RejectQuotationEdit rejectQuotationEdit)
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
                    var existingQuotationEdit = await context.QuotationEdits.FirstOrDefaultAsync(q => q.Id == rejectQuotationEdit.Id);


                    if (existingQuotationEdit == null)
                    {
                        return BadRequest("Quotation edit does not exist");
                    }

                    var bond = await context.Bonds.FirstOrDefaultAsync(b => b.Id == existingQuotationEdit.BondId);
                    if (bond == null)
                    {
                        return BadRequest("Invalid bond");
                    }

                    var userDetails = await context.Users.FirstOrDefaultAsync(u => u.Id == existingQuotationEdit.UserId);

                    if (userDetails == null)
                    {
                        return BadRequest("Invalid user");
                    }

                    // notify the user that the quotation edit has been rejected
                    var emailBody = $"Your quotation edit has been rejected  for bond {bond.IssueNumber} with the following details: <br/>" +
                        $"Buying Yield: {existingQuotationEdit.BuyingYield} <br/>" +
                        $"Selling Yield: {existingQuotationEdit.SellingYield} <br/>" +
                        $"Buy Volume: {existingQuotationEdit.BuyVolume} <br/>" +
                        $"Sell Volume: {existingQuotationEdit.SellVolume} <br/>" +
                        $"Because of the following reason: <br/>" +
                        $"Comment: {rejectQuotationEdit.RejectionReason} <br/>" +
                        $"Please make the necessary changes and re-submit the quotation edit  <br/>";


                    // Update the quotation edit
                    existingQuotationEdit.Status = QuotationEditStatus.Rejected;
                    existingQuotationEdit.RejectionReason = rejectQuotationEdit.RejectionReason;
                    await context.SaveChangesAsync();

                    var emailSubject = "Quotation Edit Rejected";
                    await UtilityService.SendEmailAsync(
                        userDetails.Email,
                        emailSubject,
                        emailBody
                    );

                    return StatusCode(200);

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

        // Fetches all quotations filled by other institutions apart from the current institution
        [HttpGet("GetAllQuotationsFilledByOtherInstitutions/{From}/{To}")]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        public async Task<ActionResult<QuotationDTO>> GetAllQuotationsFilledByOtherInstitutions(string? From = "default", string? To = "default")
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
                    var quotations = await context.Quotations.Include(x => x.Institution)
                        .Where(q =>
                         q.InstitutionId != TokenContents.InstitutionId
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


        // calulate the yield curve for all bonds using the average weighted yield for each bond
        [HttpGet("GetYieldCurveForAllBondsUsingQuotations/{From}")]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        public async Task<ActionResult<List<YieldCurve>>> GetYieldCurveForAllBondsUsingQuotations(string? From = "default")
        {
            try
            {
                DateTime fromDate = DateTime.Now;
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
                    fromDate = parsedDate;
                }

                using (var context = new QuotationsBoardContext())
                {
                    var quotations = await context.Quotations.Include(x => x.Institution).Where(q => q.CreatedAt.Date == fromDate.Date).ToListAsync();
                    var groupedQuotations = quotations.GroupBy(x => x.BondId);
                    List<BondAndAverageQuotedYield> bondAndAverageQuotedYields = new List<BondAndAverageQuotedYield>();
                    List<YieldCurve> yieldCurves = new List<YieldCurve>();

                    var LastWeek = fromDate.AddDays(-7);
                    DateTime startOfLastWeek = LastWeek.AddDays(-(int)LastWeek.DayOfWeek + (int)DayOfWeek.Monday);
                    DateTime endOfLastWeek = LastWeek.AddDays(+(int)LastWeek.DayOfWeek + (int)DayOfWeek.Sunday);

                    var currentOneYearTBill = context.TBills
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

                    foreach (var bondQuotes in groupedQuotations)
                    {
                        var bondDetails = await context.Bonds.FirstOrDefaultAsync(b => b.Id == bondQuotes.Key);
                        if (bondDetails == null)
                        {
                            continue;
                        }
                        var RemainingTenor = (bondDetails.MaturityDate - fromDate.Date).TotalDays / 364;

                        var quotationsForBond = bondQuotes.ToList();
                        var totalBuyVolume = quotationsForBond.Sum(x => x.BuyVolume);
                        var weightedBuyingYield = quotationsForBond.Sum(x => x.BuyingYield * x.BuyVolume) / totalBuyVolume;
                        var totalSellVolume = quotationsForBond.Sum(x => x.SellVolume);
                        var weightedSellingYield = quotationsForBond.Sum(x => x.SellingYield * x.SellVolume) / totalSellVolume;
                        var totalQuotes = quotationsForBond.Count;
                        var averageWeightedYield = (weightedBuyingYield + weightedSellingYield) / 2;
                        BondAndAverageQuotedYield bondAndAverageQuotedYield = new BondAndAverageQuotedYield
                        {
                            BondId = bondQuotes.Key,
                            AverageQuotedYield = averageWeightedYield,
                            BondTenor = RemainingTenor,
                        };
                        bondAndAverageQuotedYields.Add(bondAndAverageQuotedYield);

                    }

                    Dictionary<int, (double, double)> benchmarkRanges = YieldCurveHelper.GetBenchmarkRanges(fromDate);


                    foreach (var benchmarkRange in benchmarkRanges)
                    {
                        BondAndAverageQuotedYield closestBond = null;
                        double closestDifference = double.MaxValue;

                        foreach (var bondAndAverageQuotedYield in bondAndAverageQuotedYields)
                        {
                            var bondTenor = bondAndAverageQuotedYield.BondTenor;
                            if (bondTenor >= benchmarkRange.Value.Item1 && bondTenor <= benchmarkRange.Value.Item2)
                            {
                                // Find the bond closest to the middle of the benchmark range
                                var midPoint = (benchmarkRange.Value.Item1 + benchmarkRange.Value.Item2) / 2;
                                var difference = Math.Abs(midPoint - bondTenor);
                                if (difference < closestDifference)
                                {
                                    closestDifference = difference;
                                    closestBond = bondAndAverageQuotedYield;
                                }
                            }
                        }

                        if (closestBond != null)
                        {
                            // Retrieve additional bond details from the database/context if needed
                            var bondDetails = await context.Bonds.FirstOrDefaultAsync(b => b.Id == closestBond.BondId);
                            decimal remainingDaysToMaturity = (decimal)(bondDetails.MaturityDate - fromDate.Date).TotalDays;
                            decimal remainingYearsToMaturity = Math.Round(remainingDaysToMaturity / 364, 1, MidpointRounding.AwayFromZero);

                            // Create a new YieldCurve DTO and fill it with the details
                            YieldCurve yieldCurve = new YieldCurve
                            {
                                BenchMarkTenor = benchmarkRange.Key,
                                Yield = (decimal)closestBond.AverageQuotedYield,
                                BondUsed = bondDetails.IssueNumber,
                                IssueDate = bondDetails.IssueDate,
                                MaturityDate = bondDetails.MaturityDate,
                                Coupon = bondDetails.CouponRate // Assuming there's a CouponRate field in your Bond entity
                            };

                            yieldCurves.Add(yieldCurve);

                        }
                    }
                    YieldCurve tBillYieldCurve = new YieldCurve
                    {
                        BenchMarkTenor = 1,
                        Yield = (decimal)currentOneYearTBill.Yield,
                        IssueDate = currentOneYearTBill.IssueDate,
                        MaturityDate = currentOneYearTBill.MaturityDate,
                    };
                    yieldCurves.Add(tBillYieldCurve);

                    return StatusCode(200, yieldCurves);
                }
            }
            catch (Exception Ex)
            {
                UtilityService.LogException(Ex);
                return StatusCode(500, UtilityService.HandleException(Ex));
            }

        }

        private Bond? GetClosestBond(IEnumerable<Bond> bonds, double lowerBound, double upperBound)
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

    }
}
