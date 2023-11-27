using AutoMapper;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace Quotations_Board_Backend.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    // [Authorize(Roles = CustomRoles.Dealer + "," + CustomRoles.ChiefDealer, AuthenticationSchemes = "Bearer")]

    public class QuotationsController : ControllerBase
    {
        // Create a new quotation
        [HttpPost("CreateQuotation")]
        [ProducesResponseType(StatusCodes.Status201Created)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        public async Task<ActionResult<Quotation>> CreateQuotation(NewQuotation newQuotation)
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
                    var userId = UtilityService.GetUserIdFromToken(TokenContents.token);
                    // Map the DTO to the model
                    var mapper = new MapperConfiguration(cfg => cfg.CreateMap<NewQuotation, Quotation>()).CreateMapper();
                    var quotation = mapper.Map<Quotation>(newQuotation);
                    quotation.UserId = userId;
                    quotation.CreatedAt = DateTime.Now;
                    quotation.InstitutionId = TokenContents.InstitutionId;
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

    }
}
