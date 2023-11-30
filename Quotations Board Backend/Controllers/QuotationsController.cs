using AutoMapper;
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

                    // Ensure selling yield is not greater than buying yield
                    if (quotation.SellingYield > quotation.BuyingYield)
                    {
                        return BadRequest("Selling yield cannot be greater than buying yield");
                    }

                    // Ensure that today this institution has not already filled a quotation for this bond
                    var existingQuotation = await context.Quotations.FirstOrDefaultAsync(q => q.InstitutionId == quotation.InstitutionId && q.BondId == quotation.BondId && q.CreatedAt.Date == quotation.CreatedAt.Date);
                    if (existingQuotation != null)
                    {
                        return BadRequest(" A quotation for this bond has already been  for today");
                    }

                    // Save the quotation
                    await context.Quotations.AddAsync(quotation);
                    await context.SaveChangesAsync();
                    return StatusCode(201, quotation);
                }

            }
            catch (Exception Ex)
            {
                UtilityService.LogException(Ex);
                return StatusCode(500, UtilityService.HandleException(Ex));
            }
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
                    if (existingQuotation == null)
                    {
                        return BadRequest("Quotation does not exist");
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

                    // Ensure selling yield is not greater than buying yield
                    if (quotation.SellingYield > quotation.BuyingYield)
                    {
                        return BadRequest("Selling yield cannot be greater than buying yield");
                    }

                    // Save the quotation
                    context.Quotations.Update(quotation);
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
                        var averageYield = quoteinfos.Average(x => (x.TotalBuyYield + x.TotalSellYield) / 2);

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
                             AverageBuyYield = g.Average(q => q.BuyingYield),
                             AverageSellYield = g.Average(q => q.SellingYield),
                             TotalQuotations = g.Count(),
                             CombinedAverageYield = g.Average(q => (q.BuyingYield + q.SellingYield) / 2),
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
                            AverageYield = quotation.CombinedAverageYield,
                            AverageVolume = (quotation.TotalBuyVolume + quotation.TotalSellVolume) / 2,
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
                        q.InstitutionId == TokenContents.InstitutionId
                        && q.CreatedAt.Date >= fromDate.Date
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
                            AverageBuyYield = quotation.AverageBuyYield,
                            AverageSellYield = quotation.AverageSellYield,
                            AverageYield = quotation.CombinedAverageYield,
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
                        var averageBuyYield = quoteinfos.Average(x => x.TotalBuyYield);
                        var averageSellYield = quoteinfos.Average(x => x.TotalSellYield);
                        var averageYield = quoteinfos.Average(x => (x.TotalBuyYield + x.TotalSellYield) / 2);

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
                        var averageBuyYield = quoteinfos.Average(x => x.TotalBuyYield);
                        var averageSellYield = quoteinfos.Average(x => x.TotalSellYield);
                        var averageYield = quoteinfos.Average(x => (x.TotalBuyYield + x.TotalSellYield) / 2);

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

        // Yield Curve for a Specific Bond
        [HttpGet("GetYieldCurveForBond/{bondId}/{From}")]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        public async Task<ActionResult<List<YieldCurveDTO>>> GetYieldCurveForBond(string bondId, string? From = "default")
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
                    // is date in the future?
                    if (fromDate > DateTime.Now)
                    {
                        return BadRequest("Date cannot be in the future");
                    }
                    fromDate = parsedDate;
                }
                using (var context = new QuotationsBoardContext())
                {
                    LoginTokenDTO TokenContents = UtilityService.GetUserIdFromCurrentRequest(Request);
                    var userId = UtilityService.GetUserIdFromToken(Request);
                    var quotations = await context.Quotations.Include(x => x.Institution).Where(q => q.BondId == bondId
                    && q.CreatedAt.Date >= fromDate.Date).ToListAsync();
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
