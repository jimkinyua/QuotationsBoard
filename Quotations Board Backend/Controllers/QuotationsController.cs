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
        [HttpGet("GetQuotationsFilledByInstitution/{From}")]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        public async Task<ActionResult<List<QuotationDTO>>> GetQuotationsFilledByInstitution(string From)
        {
            try
            {
                DateTime fromDate = DateTime.Now;
                if (String.IsNullOrEmpty(From))
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
                    var quotations = await context.Quotations.Include(x => x.Institution)
                        .Where(q => q.InstitutionId == TokenContents.InstitutionId && q.CreatedAt >= fromDate).ToListAsync();
                    List<QuotationDTO> quotationDTOs = new List<QuotationDTO>();
                    foreach (var quotation in quotations)
                    {
                        var quotationDTO = new QuotationDTO
                        {
                            BondId = quotation.BondId,
                            BuyingYield = quotation.BuyingYield,
                            CreatedAt = quotation.CreatedAt,
                            InstitutionId = quotation.InstitutionId,
                            SellingYield = quotation.SellingYield,
                            UserId = quotation.UserId,
                            BuyVolume = quotation.BuyVolume,
                            SellVolume = quotation.SellVolume,
                            Id = quotation.Id

                        };
                        quotationDTOs.Add(quotationDTO);
                    }
                    if (quotationDTOs.Count > 0)
                    {
                        // Calculate the total buying yield, total selling yield, average buy yield, average sell yield and average yield
                        var totalBuyingYield = quotationDTOs.Sum(x => x.BuyingYield);
                        var totalSellingYield = quotationDTOs.Sum(x => x.SellingYield);
                        var averageBuyYield = quotationDTOs.Average(x => x.BuyingYield);
                        var averageSellYield = quotationDTOs.Average(x => x.SellingYield);
                        var averageYield = quotationDTOs.Average(x => (x.BuyingYield + x.SellingYield) / 2);

                        foreach (var quotationDTO in quotationDTOs)
                        {
                            quotationDTO.TotalBuyingYield = totalBuyingYield;
                            quotationDTO.TotalSellingYield = totalSellingYield;
                            quotationDTO.AverageBuyYield = averageBuyYield;
                            quotationDTO.AverageSellYield = averageSellYield;
                            quotationDTO.AverageYield = averageYield;
                        }
                    }


                    return StatusCode(200, quotationDTOs);
                }

            }
            catch (Exception Ex)
            {
                UtilityService.LogException(Ex);
                return StatusCode(500, UtilityService.HandleException(Ex));
            }
        }

        // Fetch Quaotes Filled by a Specific user
        [HttpGet("GetQuotationsFilledByUser/{From}")]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        public async Task<ActionResult<List<QuotationDTO>>> GetQuotationsFilledByUser(string From)
        {
            try
            {
                DateTime fromDate = DateTime.Now;
                if (String.IsNullOrEmpty(From))
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
                    var quotations = await context.Quotations.Include(x => x.Institution).Where(q => q.UserId == userId && q.CreatedAt >= fromDate).ToListAsync();
                    List<QuotationDTO> quotationDTOs = new List<QuotationDTO>();
                    foreach (var quotation in quotations)
                    {
                        var quotationDTO = new QuotationDTO
                        {
                            BondId = quotation.BondId,
                            BuyingYield = quotation.BuyingYield,
                            CreatedAt = quotation.CreatedAt,
                            InstitutionId = quotation.InstitutionId,
                            SellingYield = quotation.SellingYield,
                            UserId = quotation.UserId,
                            BuyVolume = quotation.BuyVolume,
                            SellVolume = quotation.SellVolume,
                            Id = quotation.Id
                        };
                        quotationDTOs.Add(quotationDTO);
                    }
                    if (quotationDTOs.Count > 0)
                    {
                        // Calculate the total buying yield, total selling yield, average buy yield, average sell yield and average yield

                        var totalBuyingYield = quotationDTOs.Sum(x => x.BuyingYield);
                        var totalSellingYield = quotationDTOs.Sum(x => x.SellingYield);
                        var averageBuyYield = quotationDTOs.Average(x => x.BuyingYield);
                        var averageSellYield = quotationDTOs.Average(x => x.SellingYield);
                        var averageYield = quotationDTOs.Average(x => (x.BuyingYield + x.SellingYield) / 2);

                        foreach (var quotationDTO in quotationDTOs)
                        {
                            quotationDTO.TotalBuyingYield = totalBuyingYield;
                            quotationDTO.TotalSellingYield = totalSellingYield;
                            quotationDTO.AverageBuyYield = averageBuyYield;
                            quotationDTO.AverageSellYield = averageSellYield;
                            quotationDTO.AverageYield = averageYield;
                        }

                    }



                    return StatusCode(200, quotationDTOs);

                }

            }
            catch (Exception Ex)
            {
                UtilityService.LogException(Ex);
                return StatusCode(500, UtilityService.HandleException(Ex));
            }
        }

        // Quotations For a Specific Bond
        [HttpGet("GetQuotationsForBond/{bondId}/{From}")]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        public async Task<ActionResult<List<QuotationDTO>>> GetQuotationsForBond(string bondId, string From)
        {
            try
            {
                DateTime fromDate = DateTime.Now;
                if (String.IsNullOrEmpty(From))
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
                    var quotations = await context.Quotations.Include(x => x.Institution).Where(q => q.BondId == bondId && q.CreatedAt >= fromDate).ToListAsync();
                    List<QuotationDTO> quotationDTOs = new List<QuotationDTO>();
                    foreach (var quotation in quotations)
                    {
                        var quotationDTO = new QuotationDTO
                        {
                            BondId = quotation.BondId,
                            BuyingYield = quotation.BuyingYield,
                            CreatedAt = quotation.CreatedAt,
                            InstitutionId = quotation.InstitutionId,
                            SellingYield = quotation.SellingYield,
                            UserId = quotation.UserId,
                            BuyVolume = quotation.BuyVolume,
                            SellVolume = quotation.SellVolume,
                            Id = quotation.Id
                        };
                        quotationDTOs.Add(quotationDTO);

                    }

                    // Calculate the total buying yield, total selling yield, average buy yield, average sell yield and average yield
                    if (quotationDTOs.Count > 0)
                    {
                        var totalBuyingYield = quotationDTOs.Sum(x => x.BuyingYield);
                        var totalSellingYield = quotationDTOs.Sum(x => x.SellingYield);
                        var averageBuyYield = quotationDTOs.Average(x => x.BuyingYield);
                        var averageSellYield = quotationDTOs.Average(x => x.SellingYield);
                        var averageYield = quotationDTOs.Average(x => (x.BuyingYield + x.SellingYield) / 2);

                        foreach (var quotationDTO in quotationDTOs)
                        {
                            quotationDTO.TotalBuyingYield = totalBuyingYield;
                            quotationDTO.TotalSellingYield = totalSellingYield;
                            quotationDTO.AverageBuyYield = averageBuyYield;
                            quotationDTO.AverageSellYield = averageSellYield;
                            quotationDTO.AverageYield = averageYield;
                        }
                    }


                    return StatusCode(200, quotationDTOs);
                }

            }
            catch (Exception Ex)
            {
                UtilityService.LogException(Ex);
                return StatusCode(500, UtilityService.HandleException(Ex));
            }
        }

        // Yield Curve for a Specific Bond
        [HttpGet("GetYieldCurveForBond/{bondId}")]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        public async Task<ActionResult<List<YieldCurveDTO>>> GetYieldCurveForBond(string bondId)
        {
            try
            {
                using (var context = new QuotationsBoardContext())
                {
                    LoginTokenDTO TokenContents = UtilityService.GetUserIdFromCurrentRequest(Request);
                    var userId = UtilityService.GetUserIdFromToken(Request);
                    var quotations = await context.Quotations.Include(x => x.Institution).Where(q => q.BondId == bondId).ToListAsync();
                    var dailyAverages = quotations.GroupBy(x => x.CreatedAt.Date).Select(
                         (g => new
                         {
                             BondId = g.Key,
                             Date = g.Key.Date,
                             AverageYield = g.Average(q => q.BuyingYield)
                         })
                    ).ToList();

                    var yieldCurveData = dailyAverages.Select(x => new YieldCurveDTO
                    {
                        BondId = bondId,
                        Date = x.Date,
                        Yield = x.AverageYield
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
