﻿using System.Text.RegularExpressions;
using System.Web;
using AutoMapper;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
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
        private readonly IConfiguration _configuration;
        private readonly UserManager<PortalUser> _userManager;
        private readonly RoleManager<IdentityRole> _roleManager;

        public InstitutionController(IMapper mapper, IConfiguration configuration, UserManager<PortalUser> userManager, RoleManager<IdentityRole> roleManager)
        {
            _mapper = mapper;
            _configuration = configuration;
            _userManager = userManager;
            _roleManager = roleManager;

        }


        [HttpGet("GetInstitutionUsers")]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [SwaggerOperation(Summary = "Get Institution Users", Description = "Gets all users of an institution", OperationId = "GetInstitutionUsers")]
        [Authorize(AuthenticationSchemes = "Bearer")]
        public async Task<ActionResult<List<PortalUserDTO>>> GetInstitutionUsers()
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

                List<Institution> institutions = context.Institutions
                    .Include(i => i.PortalUsers)
                    .Where(i => i.Id == TokenContents.InstitutionId)
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


        [HttpPost]
        [Route("RegisterInstitution")]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [SwaggerOperation(Summary = "Register new institution", Description = "Registers a new institution", OperationId = "RegisterInstitution")]
        // [Authorize(AuthenticationSchemes = "Bearer")]
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
                    // var res = await ApproveInstitutionApplicationAsync(newApplication.Id);
                    // check if the application was approved

                    return Ok("Institution Registered");

                }

            }
            catch (Exception Ex)
            {
                UtilityService.LogException(Ex);
                return StatusCode(500, UtilityService.HandleException(Ex));
            }

        }

        // Enable/Diable API Access
        [HttpPost("EnableApiAccess")]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [SwaggerOperation(Summary = "Enable/Disable API Access", Description = "Enables or disables API access for an institution", OperationId = "EnableApiAccess")]
        [Authorize(AuthenticationSchemes = "Bearer", Roles = CustomRoles.SuperAdmin)]
        public async Task<IActionResult> EnableApiAccessAsync(EnableApiAccess enableApiAccess)
        {
            if (!ModelState.IsValid)
            {
                return BadRequest(ModelState);
            }
            try
            {
                using (var context = new QuotationsBoardContext())
                {
                    var institution = await context.Institutions.FirstOrDefaultAsync(x => x.Id == enableApiAccess.InstitutionId);
                    if (institution == null)
                    {
                        return NotFound();
                    }
                    var users = await context.Users.Where(x => x.InstitutionId == enableApiAccess.InstitutionId).ToListAsync();

                    PortalUser? InstAdmin = null;

                    foreach (var user in users)
                    {
                        var _hasApiUser = await _userManager.IsInRoleAsync(user, CustomRoles.InstitutionAdmin);
                        if (_hasApiUser)
                        {
                            InstAdmin = user;
                            break;
                        }
                    }

                    if (InstAdmin == null)
                    {
                        return BadRequest("it is required to have an institution admin to enable API access. All communication will be sent to the institution admin");
                    }

                    institution.IsApiAccessEnabled = enableApiAccess.IsApiAccessEnabled;
                    context.Institutions.Update(institution);
                    await context.SaveChangesAsync();
                    // Was the API access enabled or disabled
                    if (enableApiAccess.IsApiAccessEnabled)
                    {
                        var apiRole = await context.Roles.FirstOrDefaultAsync(r => r.Name == CustomRoles.APIUser);

                        if (apiRole == null)
                        {
                            // we can create the role
                            apiRole = new IdentityRole { Name = CustomRoles.APIUser, NormalizedName = "APIUSER" };
                            context.Roles.Add(apiRole);
                            await context.SaveChangesAsync();
                        }

                        // get all users in the institution

                        Boolean hasApiUser = false;
                        foreach (var user in users)
                        {
                            var _hasApiUser = await _userManager.IsInRoleAsync(user, CustomRoles.APIUser);
                            if (_hasApiUser)
                            {
                                hasApiUser = true;
                                break;
                            }
                        }

                        if (!hasApiUser)
                        {

                            // time to create one and send the credentials
                            var domainOfInstitution = institution.OrganizationEmail.Split('@')[1];
                            var apiUserEmail = $"api@{domainOfInstitution}";
                            var ApiKey = Guid.NewGuid().ToString();
                            var ApiSecret = Guid.NewGuid().ToString();
                            // create a new user
                            var newUser = new PortalUser
                            {
                                Email = apiUserEmail,
                                UserName = ApiKey,
                                NormalizedEmail = apiUserEmail.ToUpper(),
                                FirstName = "API",
                                LastName = "User",
                                EmailConfirmed = true,
                                PhoneNumberConfirmed = true,
                                InstitutionId = institution.Id,
                                TwoFactorEnabled = false,
                            };
                            var result = await _userManager.CreateAsync(newUser, ApiSecret);
                            if (!result.Succeeded)
                            {
                                // Fetch the error details
                                string errorDetails = "";
                                foreach (var error in result.Errors)
                                {
                                    errorDetails += error.Description + "\n";
                                }
                                return BadRequest(errorDetails);
                            }
                            // set the LockoutEnabled   
                            newUser.LockoutEnabled = false;
                            await _userManager.UpdateAsync(newUser);

                            // add user to role of APIUser
                            var userRole = new IdentityUserRole<string>
                            {
                                RoleId = apiRole.Id,
                                UserId = newUser.Id
                            };
                            context.UserRoles.Add(userRole);
                            await context.SaveChangesAsync();
                            // send the API Key and Secret to the institution
                            var adminSubject = "API Access Granted";

                            var adminMessage = $@"
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
                                                            .credentials {{
                                                                background-color: #f0f0f0;
                                                                padding: 10px;
                                                                border-radius: 5px;
                                                                font-family: monospace;
                                                            }}
                                                        </style>
                                                    </head>
                                                    <body>
                                                        <div class='container'>
                                                            <h2>API Access Granted</h2>
                                                            <p>Hello,</p>
                                                            <p>Your institution has been granted API access to the Quotations Board Platform.</p>
                                                            <p>Here are your API credentials:</p>
                                                            <div class='credentials'>
                                                                <p>Client Id: {ApiKey}</p>
                                                                <p>Client Secret: {ApiSecret}</p>
                                                            </div>
                                                            <p>Please keep these credentials safe and do not share them with unauthorized persons.</p>
                                                            <p>Best regards,</p>
                                                            <p>Nairobi Stock Exchange</p>
                                                        </div>
                                                    </body>
                                                </html>";

                            await UtilityService.SendEmailAsync(InstAdmin.Email, adminSubject, adminMessage);

                            return Ok("API Access Granted");
                        }
                        // can only have one User for API   
                        return BadRequest("Organizations are only limited to one API User");
                    }

                    // api acees was disabled
                    var apiUserRole = await context.Roles.FirstOrDefaultAsync(r => r.Name == CustomRoles.APIUser);
                    if (apiUserRole == null)
                    {
                        return BadRequest("API User Role does not exist");
                    }
                    var apiUser = await context.UserRoles
                                .Join(context.Users, ur => ur.UserId, u => u.Id, (ur, u) => new { ur, u })
                                .Where(x => x.u.InstitutionId == enableApiAccess.InstitutionId && x.ur.RoleId == apiUserRole.Id)
                                .Select(x => x.u)
                                .FirstOrDefaultAsync();
                    if (apiUser != null)
                    {
                        context.Users.Remove(apiUser);
                        await context.SaveChangesAsync();
                        // email to notify the institution that API access has been disabled
                        var adminSubject = "API Access Disabled";
                        var adminMessage = $@"
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
                                                    </style>
                                                </head>
                                                <body>
                                                    <div class='container'>
                                                        <h2>API Access Disabled</h2>
                                                        <p>Hello,</p>
                                                        <p>Your institution's API access to the Quotations Board Platform has been disabled.</p>
                                                        <p>If you have any questions or need assistance, please contact our support team.</p>
                                                        <p>Best regards,</p>
                                                        <p>Nairobi Stock Exchange</p>
                                                    </div>
                                                </body>
                                            </html>";
                        await UtilityService.SendEmailAsync(InstAdmin.Email, adminSubject, adminMessage);
                    }

                    return Ok("API Access Updated");
                }
            }
            catch (Exception Ex)
            {
                UtilityService.LogException(Ex);
                return StatusCode(500, UtilityService.HandleException(Ex));
            }
        }

        // Enables Access to The Widget
        [HttpPost("EnableWidgetAccess")]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [SwaggerOperation(Summary = "Enable Widget Access", Description = "Enables access to the widget for an institution", OperationId = "EnableWidgetAccess")]
        [Authorize(AuthenticationSchemes = "Bearer", Roles = CustomRoles.SuperAdmin)]
        public async Task<IActionResult> EnableWidgetAccessAsync(EnableWidgetAccess enableWidgetAccess)
        {
            if (!ModelState.IsValid)
            {
                return BadRequest(ModelState);
            }
            try
            {
                using (var context = new QuotationsBoardContext())
                {
                    var institution = await context.Institutions.FirstOrDefaultAsync(x => x.Id == enableWidgetAccess.InstitutionId);
                    if (institution == null)
                    {
                        return NotFound();
                    }
                    var Uniqye7DigitWithNoSpecHarsOrSpaces = Guid.NewGuid().ToString().Substring(0, 7);
                    // remove all special characters and spaces
                    Uniqye7DigitWithNoSpecHarsOrSpaces = Regex.Replace(Uniqye7DigitWithNoSpecHarsOrSpaces, "[^a-zA-Z0-9_.]+", "", RegexOptions.Compiled);
                    institution.WidgetKey = Uniqye7DigitWithNoSpecHarsOrSpaces;
                    context.Institutions.Update(institution);

                    // get the institution admin and send the widget key
                    var users = await context.Users.Where(x => x.InstitutionId == enableWidgetAccess.InstitutionId).ToListAsync();
                    PortalUser? InstAdmin = null;
                    foreach (var user in users)
                    {
                        var _hasApiUser = await _userManager.IsInRoleAsync(user, CustomRoles.InstitutionAdmin);
                        if (_hasApiUser)
                        {
                            InstAdmin = user;
                            break;
                        }
                    }
                    if (InstAdmin == null)
                    {
                        return BadRequest("it is required to have an institution admin to enable API access. All communication will be sent to the institution admin");
                    }


                    await context.SaveChangesAsync();

                    // was it enabled or disabled

                    if (enableWidgetAccess.IsApiAccessEnabled)
                    {
                        var adminSubject = "Widget Access Granted";
                        var adminMessage = $@"
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
                                                    </style>
                                                </head>
                                                <body>
                                                    <div class='container'>
                                                        <h2>Widget Access Granted</h2>
                                                        <p>Hello,</p>
                                                        <p>Your institution has been granted access to the Quotations Board Widget.</p>
                                                        <p>Here is your unique widget key:</p>
                                                        <div class='credentials'>
                                                            <p>Widget Key: {Uniqye7DigitWithNoSpecHarsOrSpaces}</p>
                                                        </div>
                                                        <p>Please keep this key safe and do not share it with unauthorized persons.</p>
                                                        <p>Best regards,</p>
                                                        <p>Nairobi Stock Exchange</p>
                                                    </div>
                                                </body>
                                            </html>";
                        await UtilityService.SendEmailAsync(InstAdmin.Email, adminSubject, adminMessage);
                        return Ok(Uniqye7DigitWithNoSpecHarsOrSpaces);
                    }
                    else
                    {
                        var adminSubject = "Widget Access Disabled";
                        var adminMessage = $@"
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
                                                    </style>
                                                </head>
                                                <body>
                                                    <div class='container'>
                                                        <h2>Widget Access Disabled</h2>
                                                        <p>Hello,</p>
                                                        <p>Your institution's access to the Quotations Board Widget has been disabled.</p>
                                                        <p>If you have any questions or need assistance, please contact our support team.</p>
                                                        <p>Best regards,</p>
                                                        <p>Nairobi Stock Exchange</p>
                                                    </div>
                                                </body>
                                            </html>";
                        await UtilityService.SendEmailAsync(InstAdmin.Email, adminSubject, adminMessage);
                        return Ok("Widget Access Disabled");
                    }

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
        public async Task<ActionResult<List<InstitutionTypeDTO>>> GetInstitutionTypesAsync()
        {
            try
            {
                using (var context = new QuotationsBoardContext())
                {
                    var institutionTypes = await context.InstitutionTypes.ToListAsync();
                    // mapper configuration
                    var mapperConfig = new MapperConfiguration(mc =>
                    {
                        mc.CreateMap<InstitutionType, InstitutionTypeDTO>();
                    });
                    IMapper mapper = mapperConfig.CreateMapper();
                    var institutionTypesDTO = mapper.Map<List<InstitutionTypeDTO>>(institutionTypes);
                    return Ok(institutionTypesDTO);
                }
            }
            catch (Exception Ex)
            {
                UtilityService.LogException(Ex);
                return StatusCode(500, UtilityService.HandleException(Ex));
            }
        }

        // List of Institution Applications
        [HttpGet("Applications")]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [SwaggerOperation(Summary = "Get Institution Applications", Description = "Gets all institution applications", OperationId = "GetInstitutionApplications")]
        [Authorize(Roles = CustomRoles.SuperAdmin, AuthenticationSchemes = "Bearer")]
        public async Task<ActionResult<List<InstitutionApplicationDTO>>> GetInstitutionApplicationsAsync()
        {
            try
            {
                using (var context = new QuotationsBoardContext())
                {
                    var institutionApplications = await context.InstitutionApplications.Where(x => x.ApplicationStatus == InstitutionApplicationStatus.Pending).ToListAsync();
                    var mapper = new MapperConfiguration(cfg => cfg.CreateMap<InstitutionApplication, InstitutionApplicationDTO>()).CreateMapper();
                    var institutionApplicationsDTO = mapper.Map<List<InstitutionApplicationDTO>>(institutionApplications);
                    return Ok(institutionApplicationsDTO);
                }
            }
            catch (Exception Ex)
            {
                UtilityService.LogException(Ex);
                return StatusCode(500, UtilityService.HandleException(Ex));
            }
        }

        // Approved Institution Applications
        [HttpGet("ApprovedApplications")]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [SwaggerOperation(Summary = "Get Approved Institution Applications", Description = "Gets all approved institution applications", OperationId = "GetApprovedInstitutionApplications")]
        [Authorize(Roles = CustomRoles.SuperAdmin, AuthenticationSchemes = "Bearer")]

        public async Task<ActionResult<List<InstitutionApplicationDTO>>> GetApprovedInstitutionApplicationsAsync()
        {
            try
            {
                using (var context = new QuotationsBoardContext())
                {
                    List<InstitutionApplicationDTO> institutionApplicationsDTO = new List<InstitutionApplicationDTO>();
                    var institutionApplications = await context.InstitutionApplications.Where(x => x.ApplicationStatus == InstitutionApplicationStatus.Approved).ToListAsync();
                    foreach (var institutionApplication in institutionApplications)
                    {
                        var institution = await context.Institutions.FirstOrDefaultAsync(x => x.OrganizationEmail == institutionApplication.InstitutionEmail);
                        if (institution == null)
                        {
                            continue;
                        }
                        var _type = "";
                        var InstTypes = await context.InstitutionTypes.FirstOrDefaultAsync(x => x.Id == institution.InstitutionType);
                        if (InstTypes != null)
                        {
                            _type = InstTypes.Name;
                        }
                        else
                        {
                            _type = "N/A";
                        }
                        var isActive = institution.Status == InstitutionStatus.Active ? true : false;
                        institutionApplicationsDTO.Add(new InstitutionApplicationDTO
                        {
                            Id = institutionApplication.Id,
                            AdministratorEmail = institutionApplication.AdministratorEmail,
                            AdministratorName = institutionApplication.AdministratorName,
                            AdministratorPhoneNumber = institutionApplication.AdministratorPhoneNumber,
                            ApplicationDate = institutionApplication.ApplicationDate,
                            ApplicationStatus = institutionApplication.ApplicationStatus,
                            InstitutionAddress = institutionApplication.InstitutionAddress,
                            InstitutionEmail = institutionApplication.InstitutionEmail,
                            InstitutionName = institutionApplication.InstitutionName,
                            InstitutionType = _type,
                            InstitutionId = institution.Id,
                            IsActive = isActive,
                            IsAPIAccessEnabled = institution.IsApiAccessEnabled,
                            IsWidgetAccessEnabled = institution.WidgetKey != null ? true : false
                        });
                    }
                    return Ok(institutionApplicationsDTO);
                }
            }
            catch (Exception Ex)
            {
                UtilityService.LogException(Ex);
                return StatusCode(500, UtilityService.HandleException(Ex));
            }
        }

        // Application Details
        [HttpGet("Application/{id}")]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [SwaggerOperation(Summary = "Get Institution Application Details", Description = "Gets details of an institution application", OperationId = "GetInstitutionApplicationDetails")]
        [Authorize(Roles = CustomRoles.SuperAdmin, AuthenticationSchemes = "Bearer")]
        public async Task<ActionResult<InstitutionApplicationDTO>> GetInstitutionApplicationDetailsAsync(string id)
        {
            try
            {
                using (var context = new QuotationsBoardContext())
                {
                    var institutionApplication = await context.InstitutionApplications.FirstOrDefaultAsync(x => x.Id == id);
                    if (institutionApplication == null)
                    {
                        return NotFound();
                    }
                    var institutionApplicationDTO = _mapper.Map<InstitutionApplicationDTO>(institutionApplication);
                    return Ok(institutionApplicationDTO);
                }
            }
            catch (Exception Ex)
            {
                UtilityService.LogException(Ex);
                return StatusCode(500, UtilityService.HandleException(Ex));
            }
        }

        // Approve Application

        private async Task<IActionResult> ApproveInstitutionApplicationAsync(string id)
        {
            try
            {
                using (var context = new QuotationsBoardContext())
                {
                    var institutionApplication = await context.InstitutionApplications.FirstOrDefaultAsync(x => x.Id == id);
                    if (institutionApplication == null)
                    {
                        return NotFound();
                    }
                    institutionApplication.ApplicationStatus = InstitutionApplicationStatus.Approved;
                    context.InstitutionApplications.Update(institutionApplication);

                    // create new institution
                    var newInstitution = new Institution
                    {
                        CreatedAt = DateTime.Now,
                        OrganizationName = institutionApplication.InstitutionName,
                        OrganizationAddress = institutionApplication.InstitutionAddress,
                        OrganizationEmail = institutionApplication.InstitutionEmail,
                        InstitutionType = institutionApplication.InstitutionType
                    };
                    context.Institutions.Add(newInstitution);

                    // create new portal user (Contact Person) will be acting on behalf of the institution
                    var newPortalUser = new PortalUser
                    {
                        Email = institutionApplication.AdministratorEmail,
                        UserName = institutionApplication.AdministratorEmail,
                        PhoneNumber = institutionApplication.AdministratorPhoneNumber,
                        EmailConfirmed = false,
                        PhoneNumberConfirmed = false,
                        FirstName = institutionApplication.AdministratorName,
                        LastName = "",
                        InstitutionId = newInstitution.Id,
                        TwoFactorEnabled = true
                    };
                    context.Users.Add(newPortalUser);



                    //  Fetch the roles available
                    var roles = await context.Roles.ToListAsync();
                    // Esnure all roles in the Roles class exist in the database if not create them
                    foreach (var role in CustomRoles.AllRoles)
                    {

                        // Ensure Role Institution exists
                        if (!await _roleManager.RoleExistsAsync(role))
                        {
                            await _roleManager.CreateAsync(new IdentityRole(role));
                        }

                    }
                    // default role to assign
                    var roleToAssign = CustomRoles.InstitutionAdmin;
                    // is the institution type 7 or 8? (Central Bank or Capital Markets Regulator)
                    switch (institutionApplication.InstitutionType)
                    {
                        case "7":
                            roleToAssign = CustomRoles.CentralBank;
                            break;
                        case "8":
                            roleToAssign = CustomRoles.CapitalMarketsRegulator;
                            break;
                        case "11":
                            roleToAssign = CustomRoles.NseSRO;
                            break;
                        default:
                            roleToAssign = CustomRoles.InstitutionAdmin;
                            break;
                    }

                    var roleOfUser = await context.Roles.FirstOrDefaultAsync(x => x.Name == roleToAssign);
                    if (roleOfUser == null)
                    {
                        return BadRequest("Institution Admin Role does not exist");
                    }
                    // add user to role of InstitutionAdmin
                    var userRole = new IdentityUserRole<string>
                    {
                        RoleId = roleOfUser.Id,
                        UserId = newPortalUser.Id
                    };
                    context.UserRoles.Add(userRole);


                    await context.SaveChangesAsync();
                    // Generate Email Confirmation PasswordResetToken
                    var token = await _userManager.GenerateEmailConfirmationTokenAsync(newPortalUser);
                    var encodedUserId = HttpUtility.UrlEncode(newPortalUser.Id);
                    var encodedCode = HttpUtility.UrlEncode(token);
                    var callbackUrl = $"{_configuration["FrontEndUrl"]}/complete-institution-setup?userId={encodedUserId}&code={encodedCode}";

                    var adminSubject = "Institution Application Approved";
                    var adminMessage = "";

                    var emailHtml = $@"
                                        <html>
                                        <head>
                                            <style>
                                                body {{ font-family: Arial, sans-serif; background-color: #f5f5f5; margin: 0; padding: 0; }}
                                                .container {{ max-width: 600px; margin: 0 auto; background-color: #ffffff; padding: 20px; }}
                                                h1 {{ color: #0052CC; }}
                                                p {{ font-size: 16px; color: #555; }}
                                                a.button {{ 
                                                    text-decoration: none; 
                                                    color: #ffffff; 
                                                    background-color: #0052CC; 
                                                    padding: 10px 20px; 
                                                    border-radius: 5px; 
                                                    display: inline-block; 
                                                }}
                                                a.button:hover {{ background-color: #003E7E; }}
                                                .logo {{ text-align: center; margin-bottom: 20px; }}
                                                .footer {{ background-color: #0052CC; color: #ffffff; text-align: center; padding: 10px; font-size: 12px; }}
                                            </style>
                                        </head>
                                        <body>
                                            <div class='container'>
                                                <h1>Application Approval Notification</h1>
                                                <p>Dear {institutionApplication.AdministratorName},</p>
                                                <p>We are pleased to inform you that your application for access to the Quotation Board has been approved. As the authorized representative of your institution, you play a vital role in leveraging our platform for your institution's success.</p>
                                                <p>Key Information:</p>
                                                <ul>
                                                    <li><strong>Login Credentials:</strong> Please use your email address, {institutionApplication.AdministratorEmail}, to log in to the Quotations Board Platform.</li>
                                                    <li><strong>User Management:</strong> As the primary contact, you are responsible for managing user accounts and ensuring a secure experience for your institution's members.</li>
                                                </ul>
                                                <p>To set up your password and complete your account setup, please click the link below:</p>
                                                <a href='{callbackUrl}' class='button'>Set Up Your Password</a>
                                                <p>Should you require any assistance or have any queries, do not hesitate to reach out to our support team by replying to this email.</p>
                                                <p>We look forward to your institution's active participation on our platform.</p>
                                                <p>Warm regards,</p>
                                                <p> Nairobi Securities Exchange</p>
                                               
                                            </div>
                                        </body>
                                        </html>";


                    await UtilityService.SendEmailAsync(institutionApplication.AdministratorEmail, adminSubject, emailHtml);

                    return Ok("Application Approved");
                }
            }
            catch (Exception Ex)
            {
                UtilityService.LogException(Ex);
                return StatusCode(500, UtilityService.HandleException(Ex));
            }
        }


        // Approve Application
        [HttpPost("ApproveApplication/{id}")]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [Authorize(Roles = CustomRoles.SuperAdmin, AuthenticationSchemes = "Bearer")]
        public async Task<IActionResult> ApproveAsync(string id)
        {
            if (string.IsNullOrEmpty(id))
            {
                return BadRequest("Id is required");
            }

            try
            {
                using (var context = new QuotationsBoardContext())
                {
                    var institutionApplication = await context.InstitutionApplications.FirstOrDefaultAsync(x => x.Id == id);
                    if (institutionApplication == null)
                    {
                        return NotFound();
                    }
                    var ApproveResult = await ApproveInstitutionApplicationAsync(institutionApplication.Id);
                    if (ApproveResult is OkObjectResult)
                    {
                        return Ok("Application Approved");
                    }
                    else
                    {
                        return BadRequest(ApproveResult);
                    }
                }
            }
            catch (Exception Ex)
            {
                UtilityService.LogException(Ex);
                return StatusCode(500, UtilityService.HandleException(Ex));
            }
        }
        // Reject Application
        [HttpPost("RejectApplication")]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [SwaggerOperation(Summary = "Reject Institution Application", Description = "Rejects an institution application", OperationId = "RejectInstitutionApplication")]
        [Authorize(Roles = CustomRoles.SuperAdmin, AuthenticationSchemes = "Bearer")]

        public async Task<IActionResult> RejectInstitutionApplicationAsync(RejectApplication rejectApplication)
        {
            if (!ModelState.IsValid)
            {
                return BadRequest(ModelState);
            }
            try
            {
                using (var context = new QuotationsBoardContext())
                {
                    var institutionApplication = await context.InstitutionApplications.FirstOrDefaultAsync(x => x.Id == rejectApplication.Id);
                    if (institutionApplication == null)
                    {
                        return NotFound();
                    }
                    institutionApplication.ApplicationStatus = InstitutionApplicationStatus.Rejected;
                    context.InstitutionApplications.Update(institutionApplication);
                    await context.SaveChangesAsync();

                    var adminSubject = "Application Rejected";
                    var adminMessage = "<html><head><style>" +
                                        "body { font-family: Arial, sans-serif; background-color: #f5f5f5; margin: 0; padding: 0; }" +
                                        ".container { max-width: 600px; margin: 0 auto; background-color: #ffffff; padding: 20px; }" +
                                        "h1 { color: #333; }" +
                                        "p { font-size: 16px; color: #555; }" +
                                        "a { text-decoration: none; color: #007BFF; font-weight: bold; }" +
                                        "a:hover { text-decoration: underline; }" +
                                        "</style></head><body>" +
                                        "<div class='container'>" +
                                        "<h1> Application Rejected</h1>" +
                                        $"Hello {institutionApplication.AdministratorName}," +
                                        "<p>We are sorry to announce that your institution's application has been rejected. This means that you will not be able to access the Quotation Board." +
                                        "<ol>";
                    if (rejectApplication.RejectionReason != null)
                    {
                        adminMessage += $"<li><strong>Reason for Rejection:</strong> {rejectApplication.RejectionReason}</li>";
                    }
                    adminMessage += "</ol>" +
                                        "<p>If you encounter any issues or have questions, our support team is ready to assist you.";

                    await UtilityService.SendEmailAsync(institutionApplication.AdministratorEmail, adminSubject, adminMessage);

                    return Ok("Application Rejected");

                }

            }
            catch (Exception Ex)
            {
                UtilityService.LogException(Ex);
                return StatusCode(500, UtilityService.HandleException(Ex));
            }
        }

        // Complete Institution Setup
        [HttpPost("CompleteInstitutionSetup")]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [SwaggerOperation(Summary = "Complete Institution Setup", Description = "Completes the setup of an institution after Approval", OperationId = "CompleteInstitutionSetup")]
        [AllowAnonymous]
        public async Task<IActionResult> CompleteInstitutionSetupAsync([FromBody] ResetPasswordDTO resetPasswordDTO)
        {
            // check if model is valid
            if (!ModelState.IsValid)
            {
                return BadRequest(ModelState);
            }

            try
            {
                using (var _context = new QuotationsBoardContext())
                {
                    var user = await _context.Users.FirstOrDefaultAsync(x => x.Id == resetPasswordDTO.UserId);
                    if (user == null)
                    {
                        return NotFound();
                    }
                    // Enusre that the user has not already confirmed their email
                    if (user.EmailConfirmed)
                    {
                        return BadRequest("Your account is already set up");
                    }

                    // Validate if Pasword meets requirements
                    var passwordValidator = new PasswordValidator<PortalUser>();
                    var passwordValidationResult = await passwordValidator.ValidateAsync(_userManager, user, resetPasswordDTO.Password);
                    if (!passwordValidationResult.Succeeded)
                    {
                        // Fetch the error details
                        string errorDetails = "";
                        foreach (var error in passwordValidationResult.Errors)
                        {
                            errorDetails += error.Description + "\n";
                        }
                        return BadRequest(errorDetails);
                    }

                    // Confirm the user's email
                    var confirmEmailResult = await _userManager.ConfirmEmailAsync(user, resetPasswordDTO.Token);
                    if (!confirmEmailResult.Succeeded)
                    {
                        // Fetch the error details
                        string errorDetails = "";
                        foreach (var error in confirmEmailResult.Errors)
                        {
                            errorDetails += error.Description + "\n";
                        }
                        return BadRequest(errorDetails);
                    }
                    var result = await _userManager.AddPasswordAsync(user, resetPasswordDTO.Password);
                    if (!result.Succeeded)
                    {
                        // Fetch the error details
                        string errorDetails = "";
                        foreach (var error in result.Errors)
                        {
                            errorDetails += error.Description + "\n";
                        }
                        return BadRequest(errorDetails);
                    }
                    return Ok("Account Setup Complete");

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