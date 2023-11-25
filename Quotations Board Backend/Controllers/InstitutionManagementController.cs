using AutoMapper;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Quotations_Board_Backend.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    [Authorize(Roles = CustomRoles.InstitutionAdmin, AuthenticationSchemes = "Bearer")]
    public class InstitutionManagementController : ControllerBase
    {
        private readonly IMapper _mapper;
        private readonly IConfiguration _configuration;
        private readonly UserManager<PortalUser> _userManager;
        private readonly RoleManager<IdentityRole> _roleManager;

        public InstitutionManagementController(
            IMapper mapper,
            IConfiguration configuration,
            UserManager<PortalUser> userManager,
            RoleManager<IdentityRole> roleManager)
        {
            _mapper = mapper;
            _configuration = configuration;
            _userManager = userManager;
            _roleManager = roleManager;
        }

        [HttpGet("GetInstitutionUsers")]
        public async Task<ActionResult<List<PortalUserDTO>>> GetInstitutionUsers()
        {
            LoginTokenDTO TokenContents = UtilityService.GetUserIdFromCurrentRequest(Request);
            if (TokenContents == null)
            {
                return Unauthorized();
            }

            using (var context = new QuotationsBoardContext())
            {
                Institution? institution = await context.Institutions
                    .Include(i => i.PortalUsers)
                    .FirstOrDefaultAsync(i => i.Id == TokenContents.InstitutionId);
                if (institution == null)
                {
                    return NotFound();
                }

                List<PortalUserDTO> portalUsers = _mapper.Map<List<PortalUserDTO>>(institution.PortalUsers);
                return Ok(portalUsers);

            }
        }
    }
}
