using System.Web;
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

        [HttpPost("AddInstitutionUser")]
        public async Task<ActionResult<PortalUserDTO>> AddInstitutionUser(NewPortalUser portalUserDTO)
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

                // check if user already exists
                PortalUser? existingUser = await _userManager.FindByEmailAsync(portalUserDTO.Email);
                if (existingUser != null)
                {
                    // May exis but not verified/confirmed their email
                    if (!await _userManager.IsEmailConfirmedAsync(existingUser))
                    {
                        return BadRequest("User already exists but has not confirmed their email");
                    }
                    return BadRequest("User already exists");
                }

                var mapper = new MapperConfiguration(cfg => cfg.CreateMap<NewPortalUser, PortalUser>()).CreateMapper();
                var portalUser = mapper.Map<PortalUser>(portalUserDTO);
                portalUser.InstitutionId = institution.Id;
                portalUser.UserName = portalUser.Email;
                portalUser.EmailConfirmed = false;
                context.Users.Add(portalUser);

                // add user to role
                var result = await _userManager.AddToRoleAsync(portalUser, portalUserDTO.Role);
                if (!result.Succeeded)
                {
                    return BadRequest(result.Errors);
                }

                // Generate Email Confirmation Token
                var token = await _userManager.GenerateEmailConfirmationTokenAsync(portalUser);
                var encodedUserId = HttpUtility.UrlEncode(portalUser.Id);
                var encodedCode = HttpUtility.UrlEncode(token);
                var callbackUrl = $"{_configuration["FrontEndUrl"]}/complete-institution-setup?userId={encodedUserId}&code={encodedCode}";

                // add user to institution
                institution.PortalUsers.Add(portalUser);
                await context.SaveChangesAsync();

                // send email to user
                string emailBody = $"<p>Dear {portalUser.FirstName},</p>" +
                    "<p>Your account has been created on the Quotations Board Portal. " +
                    " Follow the link below to complete your account setup.</p>" +
                    $"<a href='{callbackUrl}'>Complete Account Setup</a>";

                await UtilityService.SendEmailAsync(portalUser.Email, "Quotations Board Portal Account Created", emailBody);

                // return user
                PortalUserDTO portalUserDTOToReturn = _mapper.Map<PortalUserDTO>(portalUser);
                return Ok(portalUserDTOToReturn);
            }
        }

        [HttpPut("UpdateInstitutionUser")]
        public async Task<ActionResult> UpdateInstitutionUser(EditPortalUser portalUserDTO)
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

                // check if user exists
                PortalUser? existingUser = await _userManager.FindByEmailAsync(portalUserDTO.Email);
                if (existingUser == null)
                {
                    return BadRequest("User does not exist");
                }

                // Enusre that user being update is not being oved from InstitutionAdmin. Only One InstitutionAdmin per Institution
                if (existingUser.Id != portalUserDTO.Id && portalUserDTO.Role == CustomRoles.InstitutionAdmin)
                {
                    var existingAdmin = await _userManager.FindByEmailAsync(portalUserDTO.Email);
                    if (existingAdmin != null)
                    {
                        return BadRequest("Institution already has an admin");
                    }
                }

                // update user
                existingUser.FirstName = portalUserDTO.FirstName;
                existingUser.LastName = portalUserDTO.LastName;

                // update user role
                var existingRole = await _userManager.GetRolesAsync(existingUser);

                // Ensure user can only have one role at any given time
                if (existingRole.Count > 0)
                {
                    await _userManager.RemoveFromRoleAsync(existingUser, existingRole[0]);
                }
                var result = await _userManager.AddToRoleAsync(existingUser, portalUserDTO.Role);
                if (!result.Succeeded)
                {
                    return BadRequest(result.Errors);
                }
                // update user details if any have changed
                existingUser.FirstName = portalUserDTO.FirstName;
                existingUser.LastName = portalUserDTO.LastName;

                await context.SaveChangesAsync();

                // return user
                return Ok();
            }
        }

        // Disable User
        [HttpDelete("DisableInstitutionUser/{userId}")]
        public async Task<ActionResult> DisableInstitutionUser(string userId)
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

                // check if user exists
                PortalUser? existingUser = await _userManager.FindByIdAsync(userId);
                if (existingUser == null)
                {
                    return BadRequest("User does not exist");
                }


                var existingAdminRole = await _userManager.GetRolesAsync(existingUser);
                if (existingAdminRole.Count > 0 && existingAdminRole[0] == CustomRoles.InstitutionAdmin)
                {
                    var otherAdmins = await _userManager.GetUsersInRoleAsync(CustomRoles.InstitutionAdmin);
                    if (otherAdmins.Count == 1)
                    {
                        return BadRequest("Cannot disable the only admin");
                    }
                }

                existingUser.LockoutEnabled = true;
                existingUser.LockoutEnd = DateTime.Now.AddYears(100);

                await context.SaveChangesAsync();

                return Ok();
            }
        }
    }
}
