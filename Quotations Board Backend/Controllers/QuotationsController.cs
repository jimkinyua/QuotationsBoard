using AutoMapper;
using ClosedXML.Excel;
using DocumentFormat.OpenXml.Drawing;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Globalization;

namespace Quotations_Board_Backend.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    // [Authorize(Roles = $"{CustomRoles.Dealer}, {CustomRoles.ChiefDealer}, {CustomRoles.InstitutionAdmin}, {CustomRoles.SuperAdmin}", AuthenticationSchemes = "Bearer")]

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
                    // is it a weekend?
                    if (DateTime.Now.DayOfWeek == DayOfWeek.Saturday || DateTime.Now.DayOfWeek == DayOfWeek.Sunday)
                    {
                        return BadRequest("Quotations cannot be filled on weekends");
                    }
                    LoginTokenDTO TokenContents = UtilityService.GetUserIdFromCurrentRequest(Request);
                    var userId = UtilityService.GetUserIdFromToken(Request);
                    // Validate bond
                    var bondValidationResult = await QuotationsHelper.ValidateBond(newQuotation.BondId);
                    if (bondValidationResult != null)
                    {
                        return BadRequest(bondValidationResult);
                    }
                    var bond = await context.Bonds.FirstOrDefaultAsync(b => b.Id == newQuotation.BondId);
                    if (bond == null)
                    {
                        return BadRequest("Invalid bond");
                    }
                    // Validate quotation time (if required)
                    if (!QuotationsHelper.IsValidQuotationTime(DateTime.Now))
                    {
                        return BadRequest("Quotations past 9 am are not accepted");
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

                    // Validate yields
                    var yieldValidationResult = QuotationsHelper.ValidateYields(quotation.BuyingYield, quotation.SellingYield);
                    if (yieldValidationResult != null)
                    {
                        return BadRequest(yieldValidationResult);
                    }

                    // Ensure that today this institution has not already filled a quotation for this bond
                    var existingQuotation = await context.Quotations.FirstOrDefaultAsync(q => q.InstitutionId == quotation.InstitutionId && q.BondId == quotation.BondId && q.CreatedAt.Date == quotation.CreatedAt.Date);
                    if (existingQuotation != null)
                    {
                        return BadRequest(" A quotation for this bond has already been placed for today");
                    }

                    // GET THE MOST RECENT DAY A QUOTATION WAS FILLED FOR THIS BOND Except today
                    var mostRecentTradingDay = await context.Quotations
                    .Where(q => q.BondId == quotation.BondId && q.CreatedAt < quotation.CreatedAt.Date)
                    .OrderByDescending(q => q.CreatedAt)
                    .Select(q => q.CreatedAt.Date)
                    .FirstOrDefaultAsync();
                    // if (mostRecentTradingDay == default(DateTime))
                    if (1 == 2)
                    {
                        // First time this bond is being quoted (We can allow the quotation because there is no previous quotation hennce not bale to compare)
                        // Save the quotation
                        await context.Quotations.AddAsync(quotation);
                        await context.SaveChangesAsync();
                        return StatusCode(201, quotation);
                    }
                    else
                    {
                        var MostRecentImpliedYield = await context.ImpliedYields
                            .Where(i => i.BondId == bond.Id)
                            .OrderByDescending(i => i.YieldDate)
                            .Select(i => i.Yield)
                            .FirstOrDefaultAsync();

                        if (MostRecentImpliedYield == default(double))
                        {
                            // reject
                            return BadRequest("Quotation Rejected: The last implied yield for this bond was not found. Please contact the system administrator for assistance.");
                        }

                        var RemainingTenorInYearsQuotedBond = QuotationsHelper.CalculateRemainingTenor(bond.MaturityDate, quotation.CreatedAt);

                        // fetch the Tenure from db

                        if (await QuotationsHelper.IsValidationEnabledForTenureAsync(RemainingTenorInYearsQuotedBond))
                        {
                            double currentAverageWeightedYield = QuotationsHelper.CalculateCurrentAverageWeightedYield(quotation.BuyingYield, quotation.BuyVolume, quotation.SellingYield, quotation.SellVolume);
                            var change = Math.Abs(currentAverageWeightedYield - MostRecentImpliedYield);
                            if (change > 1)
                            {
                                return BadRequest($"Quotation Rejected: The change in yield is {change}%, which significantly differs from the last implied yield recorded on {mostRecentTradingDay}. This exceeds our allowable limit of 1%. To resolve this, please adjust your buying or selling yields to bring the change within the 1% limit and resubmit the quotation.");
                            }

                        }

                        // Save the quotation
                        await context.Quotations.AddAsync(quotation);
                        await context.SaveChangesAsync();
                        return StatusCode(201, quotation);


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

                    // Validate quotation time (if required)
                    if (!QuotationsHelper.IsValidQuotationTime(DateTime.Now))
                    {
                        return BadRequest("Quotations past 9 am are not accepted");
                    }

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
                                var quotes = await ReadExcelDataAsync(sheetWhereDataIsLocated);
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

        private async Task<List<Quotation>> ReadExcelDataAsync(IXLWorksheet worksheet)
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

                var bondId = worksheet.Cell(row, 1).Value.ToString().Trim();
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
                    var buyYield = double.Parse(worksheet.Cell(row, 2).Value.ToString());
                    var sellYield = double.Parse(worksheet.Cell(row, 4).Value.ToString());
                    var buyVolume = int.Parse(worksheet.Cell(row, 3).Value.ToString());
                    var sellVolume = int.Parse(worksheet.Cell(row, 5).Value.ToString());
                    var yieldValidationResult = QuotationsHelper.ValidateYields(buyYield, sellYield);
                    if (yieldValidationResult != null)
                    {
                        throw new Exception(yieldValidationResult);
                    }

                    // Enusre that today this institution has not already filled a quotation for this bond
                    var existingQuotation = dbContext.Quotations.FirstOrDefault(q => q.InstitutionId == user.InstitutionId && q.BondId == bondId && q.CreatedAt.Date == DateTime.Now.Date);
                    if (existingQuotation != null)
                    {
                        throw new Exception($"A quotation for this bond at cell {row} has already been filled for today.");
                    }

                    /*var mostRecentTradingDay = dbContext.Quotations
                        .Where(q => q.BondId == bond.Id && q.CreatedAt < DateTime.Now.Date)
                        .OrderByDescending(q => q.CreatedAt)
                        .Select(q => q.CreatedAt.Date)
                        .FirstOrDefault();*/

                    Quotation quote = new Quotation
                    {
                        BondId = bond.Id,
                        BuyingYield = double.Parse(worksheet.Cell(row, 2).Value.ToString()),
                        BuyVolume = int.Parse(worksheet.Cell(row, 3).Value.ToString()),
                        SellingYield = double.Parse(worksheet.Cell(row, 4).Value.ToString()),
                        SellVolume = int.Parse(worksheet.Cell(row, 5).Value.ToString()),
                        UserId = UtilityService.GetUserIdFromToken(Request),
                        CreatedAt = DateTime.Now,
                        InstitutionId = user.InstitutionId
                    };
                    //if (mostRecentTradingDay == default(DateTime))
                    if (1==2)
                    {
                        // First time this bond is being quoted (We can allow the quotation because there is no previous quotation hennce not bale to compare)
                        //quotationRows.Add(quote);
                    }
                    else
                    {
                        var LastImpliedYield = dbContext.ImpliedYields
                            .Where(q => q.BondId == bond.Id && q.YieldDate.Date <= DateTime.Now.Date)
                            .OrderByDescending(q => q.YieldDate)
                            .Select(q => q.Yield)
                            .FirstOrDefault();
                        if (LastImpliedYield == default(double))
                        {
                            quotationRows.Add(quote);
                            continue;
                        }
                        var RemainingTenorInYearsQuotedBond = QuotationsHelper.CalculateRemainingTenor(bond.MaturityDate, quote.CreatedAt);
                        if (await QuotationsHelper.IsValidationEnabledForTenureAsync(RemainingTenorInYearsQuotedBond))
                        {
                            double currentAverageWeightedYield = QuotationsHelper.CalculateCurrentAverageWeightedYield(quote.BuyingYield, quote.BuyVolume, quote.SellingYield, quote.SellVolume);
                            var change = Math.Abs(currentAverageWeightedYield - LastImpliedYield);
                            if (change > 1)
                            {
                                throw new Exception($"Quotation at row {row} rejected. The current average weighted yield ({currentAverageWeightedYield:0.##}%) significantly differs from the last implied yield ({LastImpliedYield:0.##}%) recorded on. The percentage change of {change:0.##}% exceeds the allowable limit of 1%.");
                            }
                        }
                        quotationRows.Add(quote);


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
                    // if (bondExists.BondCategory != BondCategories.FXD)
                    // {
                    //     errors.Add($"Row {rowToBeginAt}: Only FXD bonds are allowed to be quoted.");
                    // }
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

                    // Validate yields
                    var yieldValidationResult = QuotationsHelper.ValidateYields(editQuotation.BuyYield, editQuotation.SellYield);

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

                        var emailBody = $@"
                                        <html>
                                            <head>
                                                <style>
                                                    body {{
                                                        font-family: Arial, sans-serif;
                                                        background-color: #f4f4f4;
                                                        padding: 20px;
                                                    }}
                                                    .container {{
                                                        background-color: #ffffff;
                                                        padding: 20px;
                                                        border-radius: 5px;
                                                        box-shadow: 0 2px 4px rgba(0, 0, 0, 0.1);
                                                    }}
                                                    .button {{
                                                        display: inline-block;
                                                        padding: 10px 20px;
                                                        background-color: #007bff;
                                                        color: #ffffff;
                                                        text-decoration: none;
                                                        border-radius: 5px;
                                                    }}
                                                    .button:hover {{
                                                        background-color: #0056b3;
                                                    }}
                                                    .quotation-details {{
                                                        margin-bottom: 20px;
                                                    }}
                                                    .quotation-details h3 {{
                                                        margin-top: 0;
                                                    }}
                                                </style>
                                            </head>
                                            <body>
                                                <div class='container'>
                                                    <h2>Quotation Edit Notification</h2>
                                                    <p>A quotation edit has been made by {userDetails.FirstName} {userDetails.LastName} of {institution.OrganizationName} on {DateTime.Today} for bond {bond.IssueNumber} with the following details:</p>
                                                    <div class='quotation-details'>
                                                        <h3>Existing Quotation:</h3>
                                                        <p>Buying Yield: {existingQuotation.BuyingYield}</p>
                                                        <p>Selling Yield: {existingQuotation.SellingYield}</p>
                                                        <p>Buy Volume: {existingQuotation.BuyVolume}</p>
                                                        <p>Sell Volume: {existingQuotation.SellVolume}</p>
                                                    </div>
                                                    <div class='quotation-details'>
                                                        <h3>New Proposed Quotation:</h3>
                                                        <p>Buying Yield: {editQuotation.BuyYield}</p>
                                                        <p>Selling Yield: {editQuotation.SellYield}</p>
                                                        <p>Buy Volume: {editQuotation.BuyVolume}</p>
                                                        <p>Sell Volume: {editQuotation.SellVolume}</p>
                                                        <p>Comment: {editQuotation.Comment}</p>
                                                    </div>
                                                    <p>Please approve or reject the quotation by clicking the button below.</p>
                                                    <a href='{callbackUrl}' class='button'>Approve or Reject</a>
                                                </div>
                                            </body>
                                        </html>";


                        var emailSubject = "Quotation Edit";
                        List<string> CCEmails = new List<string>();
                        CCEmails.Add("souko@nse.co.ke");

                        await UtilityService.SendEmailAsync(
                            superAdmin.Email,
                            emailSubject,
                            emailBody,
                            CCEmails
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
                    context.Quotations.Update(QuotationToUpdate);
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




                    // Update the quotation edit
                    existingQuotationEdit.Status = QuotationEditStatus.Rejected;
                    existingQuotationEdit.RejectionReason = rejectQuotationEdit.RejectionReason;
                    context.QuotationEdits.Update(existingQuotationEdit);
                    await context.SaveChangesAsync();

                    var emailSubject = "Quotation Edit Rejected";


                    var emailBody = $@"
                                    <html>
                                        <head>
                                            <style>
                                                body {{
                                                    font-family: Arial, sans-serif;
                                                    background-color: #f4f4f4;
                                                    padding: 20px;
                                                }}
                                                .container {{
                                                    background-color: #ffffff;
                                                    padding: 20px;
                                                    border-radius: 5px;
                                                    box-shadow: 0 2px 4px rgba(0, 0, 0, 0.1);
                                                }}
                                                .quotation-details {{
                                                    margin-bottom: 20px;
                                                }}
                                                .quotation-details h3 {{
                                                    margin-top: 0;
                                                }}
                                            </style>
                                        </head>
                                        <body>
                                            <div class='container'>
                                                <h2>Quotation Edit Rejected</h2>
                                                <p>Your quotation edit has been rejected for bond {bond.IssueNumber} with the following details:</p>
                                                <div class='quotation-details'>
                                                    <p>Buying Yield: {existingQuotationEdit.BuyingYield}</p>
                                                    <p>Selling Yield: {existingQuotationEdit.SellingYield}</p>
                                                    <p>Buy Volume: {existingQuotationEdit.BuyVolume}</p>
                                                    <p>Sell Volume: {existingQuotationEdit.SellVolume}</p>
                                                </div>
                                                <p>Because of the following reason:</p>
                                                <p>Comment: {rejectQuotationEdit.RejectionReason}</p>
                                                <p>Please make the necessary changes and re-submit the quotation edit.</p>
                                            </div>
                                        </body>
                                    </html>";



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
                            AverageYield = (quotation.BuyingYield + quotation.SellingYield) / 2,
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
        public async Task<ActionResult<List<FinalYieldCurveData>>> GetYieldCurveForAllBondsUsingQuotations(string? From = "default")
        {
            try
            {
                DateTime fromDate = DateTime.Now;
                if (From == "default" || From == null || string.IsNullOrWhiteSpace(From))
                {
                    fromDate = DateTime.Now;
                }
                else
                {

                    string[] formats = { "dd/MM/yyyy", "yyyy-MM-dd", "MM/dd/yyyy", "dd-MM-yyyy", "dd/MM/yyyy HH:mm:ss", "yyyy-MM-dd HH:mm:ss", "MM/dd/yyyy HH:mm:ss", "dd-MM-yyyy HH:mm:ss" };
                    DateTime targetQuoteDate;
                    bool success = DateTime.TryParseExact(From, formats, CultureInfo.InvariantCulture, DateTimeStyles.None, out targetQuoteDate);
                    if (!success)
                    {
                        return BadRequest("The date format is invalid");
                    }

                    // is date valid?
                    if (targetQuoteDate == DateTime.MinValue)
                    {
                        return BadRequest("Invalid date");
                    }
                    fromDate = targetQuoteDate;
                }

                using (var context = new QuotationsBoardContext())
                {

                    List<BondAndAverageQuotedYield> bondAndAverageQuotedYields = new List<BondAndAverageQuotedYield>();
                    List<FinalYieldCurveData> yieldCurveToPlot = new List<FinalYieldCurveData>();
                    Dictionary<int, (double, double)> benchmarkRanges = YieldCurveHelper.GetBenchmarkRanges(fromDate);
                    HashSet<double> tenuresThatRequireInterPolation = new HashSet<double>();
                    HashSet<double> tenuresThatDoNotRequireInterpolation = new HashSet<double>();
                    HashSet<string> usedBondIds = new HashSet<string>();
                    List<YieldCurveDataSet> yieldCurveCalculations = new List<YieldCurveDataSet>();
                    List<BondAndYield> bondCurrentValues = new List<BondAndYield>();

                    var (startofCycle, endOfCycle) = TBillHelper.GetCurrentTBillCycle(fromDate);
                    var bondsNotMatured = context.Bonds.Where(b => b.BondCategory == "FXD" && b.MaturityDate.Date > fromDate.Date).ToList();

                    var quotationsForSelectedDate = await QuotationsHelper.GetQuotationsForDate(fromDate);
                    var LastDateWithImpliedYields = await QuotationsHelper.GetMostRecentDateWithImpliedYieldsBeforeDateInQuestion(fromDate);

                    if (LastDateWithImpliedYields == DateTime.MinValue)
                    {
                        return BadRequest("It seems there are are no implied yields keyed in the system");
                    }

                    var impliedYields = await QuotationsHelper.GetImpliedYieldsForDate(LastDateWithImpliedYields);
                    var res = YieldCurveHelper.AddOneYearTBillToYieldCurve(LastDateWithImpliedYields, tenuresThatDoNotRequireInterpolation, yieldCurveCalculations, true);
                    if (res.Success == false)
                    {
                        return BadRequest(res.ErrorMessage);
                    }
                    bondCurrentValues = QuotationsHelper.LoadBondCurrentValues(quotationsForSelectedDate, impliedYields, bondsNotMatured, fromDate);
                    ProcessBenchmarkResult Mnaoes = YieldCurveHelper.ProcessYieldCurveUsingQuotes(fromDate, context, yieldCurveCalculations, benchmarkRanges, tenuresThatRequireInterPolation, tenuresThatDoNotRequireInterpolation, usedBondIds, bondCurrentValues);
                    if (Mnaoes.Success == false)
                    {
                        return BadRequest(Mnaoes.ErrorMessage);
                    }
                    yieldCurveCalculations.AddRange(Mnaoes.YieldCurveCalculations);
                    YieldCurveHelper.InterpolateWhereNecessary(yieldCurveCalculations, tenuresThatRequireInterPolation);
                    yieldCurveToPlot = YieldCurveHelper.GenerateYieldCurves(tenuresThatRequireInterPolation, tenuresThatDoNotRequireInterpolation, yieldCurveCalculations);
                    return Ok(yieldCurveToPlot);
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
