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
                    // Map the DTO to the model
                    var mapper = new MapperConfiguration(cfg => cfg.CreateMap<NewQuotation, Quotation>()).CreateMapper();
                    var quotation = mapper.Map<Quotation>(newQuotation);
                    quotation.UserId = userId;
                    quotation.CreatedAt = DateTime.Now;
                    quotation.InstitutionId = TokenContents.InstitutionId;

                    // Ensure selling yield is greater than buying yield
                    if (quotation.SellingYield < quotation.BuyingYield)
                    {
                        return BadRequest("Selling yield cannot be less than buying yield");
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
                    // Map the DTO to the model
                    var mapper = new MapperConfiguration(cfg => cfg.CreateMap<EditQuotation, Quotation>()).CreateMapper();
                    var quotation = mapper.Map<Quotation>(editQuotation);
                    quotation.UserId = userId;
                    //quotation.CreatedAt = DateTime.Now;
                    quotation.InstitutionId = TokenContents.InstitutionId;

                    // Ensure selling yield is greater than buying yield
                    if (quotation.SellingYield < quotation.BuyingYield)
                    {
                        return BadRequest("Selling yield cannot be less than buying yield");
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
        [HttpGet("GetQuotationsFilledByInstitution")]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        public async Task<ActionResult<List<QuotationDTO>>> GetQuotationsFilledByInstitution()
        {
            try
            {
                using (var context = new QuotationsBoardContext())
                {
                    LoginTokenDTO TokenContents = UtilityService.GetUserIdFromCurrentRequest(Request);
                    var userId = UtilityService.GetUserIdFromToken(Request);
                    var quotations = await context.Quotations.Include(x => x.Institution).ToListAsync();
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
                            UserId = quotation.UserId
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
        [HttpGet("GetQuotationsFilledByUser")]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        public async Task<ActionResult<List<QuotationDTO>>> GetQuotationsFilledByUser()
        {
            try
            {
                using (var context = new QuotationsBoardContext())
                {
                    LoginTokenDTO TokenContents = UtilityService.GetUserIdFromCurrentRequest(Request);
                    var userId = UtilityService.GetUserIdFromToken(Request);
                    var quotations = await context.Quotations.Include(x => x.Institution).Where(q => q.UserId == userId).ToListAsync();
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
                            UserId = quotation.UserId
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
        [HttpGet("GetQuotationsForBond/{bondId}")]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        public async Task<ActionResult<List<QuotationDTO>>> GetQuotationsForBond(string bondId)
        {
            try
            {
                using (var context = new QuotationsBoardContext())
                {
                    LoginTokenDTO TokenContents = UtilityService.GetUserIdFromCurrentRequest(Request);
                    var userId = UtilityService.GetUserIdFromToken(Request);
                    var quotations = await context.Quotations.Include(x => x.Institution).Where(q => q.BondId == bondId).ToListAsync();
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
                            UserId = quotation.UserId
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


    }
}
