using AutoMapper;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Swashbuckle.AspNetCore.Annotations;

namespace Quotations_Board_Backend.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class InstitutionController : ControllerBase
    {
        private readonly IMapper _mapper;

        public InstitutionController(IMapper mapper)
        {
            _mapper = mapper;
        }

        [HttpPost]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [SwaggerOperation(Summary = "Register new institution", Description = "Registers a new institution", OperationId = "RegisterInstitution")]
        [AllowAnonymous]
        public async Task<IActionResult> RegisterInstitutionAsync([FromBody] RegisterInstitution institution)
        {
            // check if model is valid
            if (!ModelState.IsValid)
            {
                return BadRequest(ModelState);
            }

            try
            {
                using (var context = new QuotationsBoardContext())
                {
                    // enssure no duplicate application exists with the same email
                    var existing = context.InstitutionApplications.FirstOrDefault(x => x.InstitutionEmail == institution.InstitutionEmail);
                    if (existing != null)
                    {
                        // check if the application has been approved
                        if (existing.ApplicationStatus == InstitutionApplicationStatus.Approved)
                        {
                            return BadRequest("An institution with the same email already exists and has been approved");
                        }
                        else if (existing.ApplicationStatus == InstitutionApplicationStatus.Open)
                        {
                            return BadRequest("An institution with the same email already exists and is pending approval");
                        }
                        else
                        {
                            return BadRequest("An Application alreasy exists with the same email");
                        }

                    }

                    // create new application
                    var newApplication = new InstitutionApplication
                    {
                        AdministratorEmail = institution.ContactEmail,
                        AdministratorName = institution.ContactPerson,
                        AdministratorPhoneNumber = institution.ContactPhone,
                        ApplicationStatus = InstitutionApplicationStatus.Pending,
                        InstitutionAddress = institution.Address,
                        InstitutionEmail = institution.InstitutionEmail,
                        InstitutionName = institution.Name,
                        InstitutionType = institution.InstitutionType
                    };
                    context.InstitutionApplications.Add(newApplication);
                    await context.SaveChangesAsync();
                    return Ok();
                }

            }
            catch (Exception Ex)
            {
                UtilityService.LogException(Ex);
                return StatusCode(500, UtilityService.HandleException(Ex));
            }

        }

        // Istitution InstitutionTypes
        [HttpGet("InstitutionTypes")]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [SwaggerOperation(Summary = "Get Institution Types", Description = "Gets all institution types", OperationId = "GetInstitutionTypes")]
        [AllowAnonymous]
        public async Task<ActionResult<InstitutionTypeDTO>> GetInstitutionTypesAsync()
        {
            try
            {
                using (var context = new QuotationsBoardContext())
                {
                    var institutionTypes = await context.InstitutionTypes.ToListAsync();
                    var institutionTypesDTO = _mapper.Map<List<InstitutionTypeDTO>>(institutionTypes);
                    return Ok(institutionTypesDTO);
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
