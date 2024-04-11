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
    // [Authorize(Roles = $"{CustomRoles.SuperAdmin}", AuthenticationSchemes = "Bearer")]
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

        [HttpGet("GetInstitutionUsers/{institutionId}")]
        public async Task<ActionResult<List<PortalUserDTO>>> GetInstitutionUsers(string institutionId)
        {
            LoginTokenDTO TokenContents = UtilityService.GetUserIdFromCurrentRequest(Request);
            List<PortalUserDTO> portalUserDTO = new List<PortalUserDTO>();

            if (TokenContents == null)
            {
                return Unauthorized();
            }
            var userId = UtilityService.GetUserIdFromToken(Request);
            using (var context = new QuotationsBoardContext())
            {
                // get roles of curren logged in user if they are superAdmin, fetch all Institution and include Users

                var userRoles = await _userManager.GetRolesAsync(await _userManager.FindByIdAsync(userId));

                if (institutionId == "index")
                {
                    institutionId = TokenContents.InstitutionId;
                }

                List<Institution> institutions = context.Institutions
                    .Include(i => i.PortalUsers)
                    .Where(i => i.Id == institutionId)
                    .ToList();

                if (institutions.Count == 0)
                {
                    return portalUserDTO;
                }
                //var mapper = new MapperConfiguration(cfg => cfg.CreateMap<PortalUser, PortalUserDTO>()).CreateMapper();
                //var portalUsers = mapper.Map<List<PortalUserDTO>>(institution.PortalUsers);
                foreach (var institution in institutions)
                {
                    foreach (var user in institution.PortalUsers)
                    {
                        var userRole = await _userManager.GetRolesAsync(user);
                        if (userRole.Count > 0)
                        {
                            portalUserDTO.Add(new PortalUserDTO
                            {
                                Id = user.Id,
                                FirstName = user.FirstName,
                                LastName = user.LastName,
                                Email = user.Email,
                                InstitutionId = user.InstitutionId,
                                Role = userRole[0],
                                IsActive = !user.LockoutEnabled,
                                CreatedAt = institution.CreatedAt
                            });
                        }
                    }
                }

                return Ok(portalUserDTO);

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

            var userId = UtilityService.GetUserIdFromToken(Request);

            if (userId == null)
            {
                return Unauthorized();
            }

            // what institution doe the user belong to? // Make sure they are institution admins too
            var userRoles = await _userManager.GetRolesAsync(await _userManager.FindByIdAsync(userId));
            if (userRoles.Count == 0 || userRoles[0] != CustomRoles.InstitutionAdmin || TokenContents.InstitutionId != portalUserDTO.InstitutionId || userRoles[0] != CustomRoles.SuperAdmin)
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

                // Get the role
                var role = await _roleManager.FindByNameAsync(portalUserDTO.Role);
                if (role == null)
                {
                    return BadRequest("Role does not exist");
                }

                // add user to role 
                // add user to role of InstitutionAdmin
                var userRole = new IdentityUserRole<string>
                {
                    RoleId = role.Id,
                    UserId = portalUser.Id
                };
                context.UserRoles.Add(userRole);

                // Generate Email Confirmation Token
                var token = await _userManager.GenerateEmailConfirmationTokenAsync(portalUser);
                var encodedUserId = HttpUtility.UrlEncode(portalUser.Id);
                var encodedCode = HttpUtility.UrlEncode(token);
                var callbackUrl = $"{_configuration["FrontEndUrl"]}/complete-institution-setup?userId={encodedUserId}&code={encodedCode}";

                // add user to institution
                // institution.PortalUsers.Add(portalUser);
                await context.SaveChangesAsync();

                // send email to user
                string emailBody = $@"
                                    <html>
                                    <head>
                                        <style>
                                            body {{
                                                font-family: Arial, sans-serif;
                                                background-color: #f5f5f5;
                                                padding: 20px;
                                            }}
                                            .container {{
                                                max-width: 600px;
                                                margin: 0 auto;
                                                background-color: #ffffff;
                                                padding: 20px;
                                                border-radius: 5px;
                                                box-shadow: 0 2px 4px rgba(0, 0, 0, 0.1);
                                            }}
                                            a.button {{
                                                display: inline-block;
                                                text-decoration: none;
                                                background-color: #0052CC;
                                                color: #ffffff;
                                                padding: 10px 20px;
                                                border-radius: 5px;
                                                font-weight: bold;
                                            }}
                                            a.button:hover {{
                                                background-color: #003E7E;
                                            }}
                                        </style>
                                    </head>
                                    <body>
                                        <div class='container'>
                                            <p>Dear {portalUser.FirstName},</p>
                                            <p>Your account has been created on the Quotations Board Portal. Please follow the link below to complete your account setup:</p>
                                            <a href='{callbackUrl}' class='button'>Complete Account Setup</a>
                                        </div>
                                    </body>
                                    </html>";


                await UtilityService.SendEmailAsync(portalUser.Email, "Quotations Board Portal Account Created", emailBody);

                // return user
                //PortalUserDTO portalUserDTOToReturn = _mapper.Map<PortalUserDTO>(portalUser);
                return Ok();
            }
        }

        [HttpPost("GetInstitutionApiKey")]
        [Authorize(Roles = CustomRoles.InstitutionAdmin, AuthenticationSchemes = "Bearer")]
        public async Task<ActionResult> GetInstitutionApiKey()
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

                // is the api api access active?
                if (!institution.IsApiAccessEnabled)
                {
                    return BadRequest("API access is inactive");
                }

                // get hold of a user with the APIUser role
                var apiUser = await _userManager.GetUsersInRoleAsync(CustomRoles.APIUser);
                var existingApiUser = apiUser.FirstOrDefault(u => u.InstitutionId == institution.Id);
                if (existingApiUser == null)
                {
                    return BadRequest("Seems like the API user does not exist");
                }

                // generate a new API key
                var apiKey = Guid.NewGuid().ToString();

                // API KEY ACTS AS THE PASSWORD SO UPDATE THE USER PASSWORD
                var result = await _userManager.RemovePasswordAsync(existingApiUser);
                if (!result.Succeeded)
                {
                    return BadRequest(result.Errors);
                }
                result = await _userManager.AddPasswordAsync(existingApiUser, apiKey);
                if (!result.Succeeded)
                {
                    return BadRequest(result.Errors);
                }

                string emailBody = $@"
                                    <html>
                                    <head>
                                        <style>
                                            body {{
                                                font-family: Arial, sans-serif;
                                                background-color: #f5f5f5;
                                                padding: 20px;
                                            }}
                                            .container {{
                                                max-width: 600px;
                                                margin: 0 auto;
                                                background-color: #ffffff;
                                                padding: 20px;
                                                border-radius: 5px;
                                                box-shadow: 0 2px 4px rgba(0, 0, 0, 0.1);
                                            }}
                                            .api-key {{
                                                background-color: #f0f0f0;
                                                padding: 10px;
                                                border-radius: 5px;
                                                font-family: monospace;
                                            }}
                                        </style>
                                    </head>
                                    <body>
                                        <div class='container'>
                                            <p>Hello,</p>
                                            <p>Your Client  Secret has been reset. Your new API key is:</p>
                                            <div class='api-key'>{apiKey}</div>
                                        </div>
                                    </body>
                                    </html>";


                await UtilityService.SendEmailAsync(existingApiUser.Email, "Quotations Board Portal API Key Reset", emailBody);

                return Ok(apiKey);
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

                // if user is assigend institution admin role, ensure there is at least one admin and grant the former admin role the role occupied by the new admin
                var UserToBeUpdatedCurrentRole = await _userManager.GetRolesAsync(existingUser);
                if (UserToBeUpdatedCurrentRole.Count > 0 && UserToBeUpdatedCurrentRole[0] != CustomRoles.InstitutionAdmin)
                {
                    // What role is the user being assigned?
                    var newRole = await _roleManager.FindByNameAsync(portalUserDTO.Role);
                    // is it InstitutionAdmin?
                    if (newRole.Name == CustomRoles.InstitutionAdmin)
                    {
                        // who holds the InstitutionAdmin role within the institution?
                        var otherAdmins = await _userManager.GetUsersInRoleAsync(CustomRoles.InstitutionAdmin);

                        // only interested in other admins within the same institution
                        var existingSchoolAdmin = otherAdmins.FirstOrDefault(u => u.InstitutionId == institution.Id);
                        if (existingSchoolAdmin == null)
                        {
                            return BadRequest("There must be at least one admin");
                        }

                        // Get Inst Amin Role
                        var existingSchoolAdminRole = await _userManager.GetRolesAsync(existingSchoolAdmin);
                        if (existingSchoolAdminRole.Count > 0)
                        {
                            // Remove Exitinsg admin from current  role
                            await _userManager.RemoveFromRoleAsync(existingSchoolAdmin, existingSchoolAdminRole[0]);
                            //Assign them the role previously held by the new admin
                            await _userManager.AddToRoleAsync(existingSchoolAdmin, UserToBeUpdatedCurrentRole[0]);
                        }


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
                await _userManager.UpdateAsync(existingUser);

                // await context.SaveChangesAsync();

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
                await _userManager.UpdateAsync(existingUser);
                // await context.SaveChangesAsync();

                // Send email to user notifying them that their account has been disabled
                string emailBody = $@"
                                    <html>
                                    <head>
                                        <style>
                                            body {{
                                                font-family: Arial, sans-serif;
                                                background-color: #f5f5f5;
                                                padding: 20px;
                                            }}
                                            .container {{
                                                max-width: 600px;
                                                margin: 0 auto;
                                                background-color: #ffffff;
                                                padding: 20px;
                                                border-radius: 5px;
                                                box-shadow: 0 2px 4px rgba(0, 0, 0, 0.1);
                                            }}
                                            .warning {{
                                                color: #D9534F; /* Bootstrap 'danger' color */
                                                font-weight: bold;
                                            }}
                                        </style>
                                    </head>
                                    <body>
                                        <div class='container'>
                                            <p>Dear {existingUser.FirstName},</p>
                                            <p class='warning'>Your account has been disabled on the Quotations Board Portal.</p>
                                        </div>
                                    </body>
                                    </html>";


                await UtilityService.SendEmailAsync(existingUser.Email, "Quotations Board Portal Account Disabled", emailBody);


                return Ok();
            }
        }

        // Enable User
        [HttpPut("EnableInstitutionUser/{userId}")]

        public async Task<ActionResult> EnableInstitutionUser(string userId)
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

                existingUser.LockoutEnabled = false;
                existingUser.LockoutEnd = null;
                await _userManager.UpdateAsync(existingUser);
                await context.SaveChangesAsync();
                // Send email to user notifying them that their account has been enabled
                string emailBody = $@"
                                    <html>
                                    <head>
                                        <style>
                                            body {{
                                                font-family: Arial, sans-serif;
                                                background-color: #f5f5f5;
                                                padding: 20px;
                                            }}
                                            .container {{
                                                max-width: 600px;
                                                margin: 0 auto;
                                                background-color: #ffffff;
                                                padding: 20px;
                                                border-radius: 5px;
                                                box-shadow: 0 2px 4px rgba(0, 0, 0, 0.1);
                                            }}
                                            a.button {{
                                                display: inline-block;
                                                text-decoration: none;
                                                background-color: #0052CC;
                                                color: #ffffff;
                                                padding: 10px 20px;
                                                border-radius: 5px;
                                                font-weight: bold;
                                            }}
                                            a.button:hover {{
                                                background-color: #003E7E;
                                            }}
                                        </style>
                                    </head>
                                    <body>
                                        <div class='container'>
                                            <p>Dear {existingUser.FirstName},</p>
                                            <p>Your account has been enabled on the Quotations Board Portal. Please follow the link below to log in to your account:</p>
                                            <a href='{_configuration["FrontEndUrl"]}' class='button'>Login</a>
                                        </div>
                                    </body>
                                    </html>";


                await UtilityService.SendEmailAsync(existingUser.Email, "Quotations Board Portal Account Enabled", emailBody);


                return Ok();
            }
        }

        // Disable an Institution
        [HttpPost("DisableInstitution/{institutionId}")]
        [Authorize(Roles = CustomRoles.SuperAdmin, AuthenticationSchemes = "Bearer")]

        public async Task<ActionResult> DisableInstitution(string institutionId)
        {
            try
            {
                LoginTokenDTO TokenContents = UtilityService.GetUserIdFromCurrentRequest(Request);
                if (TokenContents == null)
                {
                    return Unauthorized();
                }

                Institution? institution;
                using (var context = new QuotationsBoardContext())
                {
                    institution = await context.Institutions
                                              .Include(i => i.PortalUsers)
                                              .FirstOrDefaultAsync(i => i.Id == institutionId);
                }
                if (institution == null)
                {
                    return NotFound();
                }

                var superAdmin = await _userManager.GetUsersInRoleAsync(CustomRoles.SuperAdmin);
                var superAdminInstitution = superAdmin.FirstOrDefault(u => u.InstitutionId == institution.Id);

                institution.Status = InstitutionStatus.Inactive;
                institution.DeactivatedAt = DateTime.Now;

                foreach (var user in institution.PortalUsers)
                {
                    await DisableUser(user);
                }

                using (var context = new QuotationsBoardContext())
                {
                    context.Institutions.Update(institution);
                    await context.SaveChangesAsync();
                }

                var institutionAdmin = await _userManager.GetUsersInRoleAsync(CustomRoles.InstitutionAdmin);
                var institutionAdminInstitution = institutionAdmin.FirstOrDefault(u => u.InstitutionId == institution.Id);
                if (institutionAdminInstitution == null)
                {
                    return BadRequest("Institution Admin not found");
                }

                // Send email notification
                string emailBody = $@"
                                    <html>
                                    <head>
                                        <style>
                                            body {{
                                                font-family: Arial, sans-serif;
                                                background-color: #f5f5f5;
                                                padding: 20px;
                                            }}
                                            .container {{
                                                max-width: 600px;
                                                margin: 0 auto;
                                                background-color: #ffffff;
                                                padding: 20px;
                                                border-radius: 5px;
                                                box-shadow: 0 2px 4px rgba(0, 0, 0, 0.1);
                                            }}
                                            .warning {{
                                                color: #D9534F; /* Bootstrap 'danger' color */
                                                font-weight: bold;
                                            }}
                                        </style>
                                    </head>
                                    <body>
                                        <div class='container'>
                                            <p>Dear {institution.OrganizationName},</p>
                                            <p class='warning'>Your account has been disabled on the Quotations Board Portal.</p>
                                        </div>
                                    </body>
                                    </html>";

                await UtilityService.SendEmailAsync(institutionAdminInstitution.Email, "Quotations Board Portal Account Disabled", emailBody);

                return Ok();
            }
            catch (Exception Ex)
            {

                UtilityService.HandleException(Ex);
                return StatusCode(StatusCodes.Status500InternalServerError, Ex.Message);
            }
        }

        private async Task DisableUser(PortalUser user)
        {
            user.LockoutEnabled = true;
            user.LockoutEnd = DateTime.Now.AddYears(100);

            using (var context = new QuotationsBoardContext())
            {
                context.Entry(user).State = EntityState.Modified;
                await context.SaveChangesAsync();
            }
        }


        // Enable an Institution
        [HttpPost("EnableInstitution/{institutionId}")]
        [Authorize(Roles = CustomRoles.SuperAdmin, AuthenticationSchemes = "Bearer")]

        public async Task<ActionResult> EnableInstitution(string institutionId)
        {
            LoginTokenDTO TokenContents = UtilityService.GetUserIdFromCurrentRequest(Request);
            if (TokenContents == null)
            {
                return Unauthorized();
            }

            Institution? institution;
            using (var context = new QuotationsBoardContext())
            {
                institution = await context.Institutions
                                          .FirstOrDefaultAsync(i => i.Id == institutionId);
            }

            if (institution == null)
            {
                return NotFound();
            }

            institution.Status = InstitutionStatus.Active;
            // institution.DeactivatedAt = DateTime.Now;
            // 
            using (var context = new QuotationsBoardContext())
            {
                context.Institutions.Update(institution);
                await context.SaveChangesAsync();
            }

            await EnableAllUsersInInstitution(institutionId);
            // get user with InstitutionAdmin role and send them an email
            var institutionAdmin = await _userManager.GetUsersInRoleAsync(CustomRoles.InstitutionAdmin);
            var institutionAdminInstitution = institutionAdmin.FirstOrDefault(u => u.InstitutionId == institution.Id);
            if (institutionAdminInstitution == null)
            {
                return BadRequest("Institution Admin not found");
            }

            // Send email notification
            string emailBody = $@"
                                <html>
                                <head>
                                    <style>
                                        body {{
                                            font-family: Arial, sans-serif;
                                            background-color: #f5f5f5;
                                            padding: 20px;
                                        }}
                                        .container {{
                                            max-width: 600px;
                                            margin: 0 auto;
                                            background-color: #ffffff;
                                            padding: 20px;
                                            border-radius: 5px;
                                            box-shadow: 0 2px 4px rgba(0, 0, 0, 0.1);
                                        }}
                                        a.button {{
                                            display: inline-block;
                                            text-decoration: none;
                                            background-color: #0052CC;
                                            color: #ffffff;
                                            padding: 10px 20px;
                                            border-radius: 5px;
                                            font-weight: bold;
                                        }}
                                        a.button:hover {{
                                            background-color: #003E7E;
                                        }}
                                    </style>
                                </head>
                                <body>
                                    <div class='container'>
                                        <p>Dear {institutionAdminInstitution.LastName},</p>
                                        <p>Your account has been enabled on the Quotations Board Portal. Please follow the link below to log in to your account:</p>
                                        <a href='{_configuration["FrontEndUrl"]}' class='button'>Login</a>
                                    </div>
                                </body>
                                </html>";

            await UtilityService.SendEmailAsync(institutionAdminInstitution.Email, "Quotations Board Portal Account Enabled", emailBody);

            return Ok();
        }

        private async Task EnableAllUsersInInstitution(string institutionId)
        {
            var users = await GetUsersInInstitutionAsync(institutionId); // Assuming such a method exists or can be implemented.
            foreach (var user in users)
            {
                user.LockoutEnabled = false;
                user.LockoutEnd = null;
                using (var context = new QuotationsBoardContext())
                {
                    context.Entry(user).State = EntityState.Modified;
                    await context.SaveChangesAsync();
                }
            }
        }

        private async Task<List<PortalUser>> GetUsersInInstitutionAsync(string institutionId)
        {
            using (var context = new QuotationsBoardContext())
            {
                return await context.Users.Where(u => u.InstitutionId == institutionId).ToListAsync();
            }
        }

    }
}
