﻿using System.Web;
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



        public InstitutionController(IMapper mapper, IConfiguration configuration, UserManager<PortalUser> userManager)
        {
            _mapper = mapper;
            _configuration = configuration;
            _userManager = userManager;
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
                    var institutionApplications = await context.InstitutionApplications.ToListAsync();
                    var institutionApplicationsDTO = _mapper.Map<List<InstitutionApplicationDTO>>(institutionApplications);
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
        [HttpPost("ApproveApplication/{id}")]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [SwaggerOperation(Summary = "Approve Institution Application", Description = "Approves an institution application", OperationId = "ApproveInstitutionApplication")]
        [Authorize(Roles = CustomRoles.SuperAdmin, AuthenticationSchemes = "Bearer")]
        public async Task<IActionResult> ApproveInstitutionApplicationAsync(string id)
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

                    // create new portal user (Contact Person) will be acting on behalf of the institution
                    var newPortalUser = new PortalUser
                    {
                        Email = institutionApplication.AdministratorEmail,
                        UserName = institutionApplication.AdministratorEmail,
                        PhoneNumber = institutionApplication.AdministratorPhoneNumber,
                        EmailConfirmed = false,
                        PhoneNumberConfirmed = false,
                    };
                    context.Users.Add(newPortalUser);

                    //  Fetch the roles available
                    var roles = await context.Roles.ToListAsync();
                    // Usure all roles in the Roles class exist in the database if not create them
                    foreach (var role in CustomRoles.AllRoles)
                    {
                        if (!roles.Any(x => x.Name == role))
                        {
                            var newRole = new IdentityRole
                            {
                                Name = role,
                                NormalizedName = role.ToUpper()
                            };
                            context.Roles.Add(newRole);
                        }
                    }

                    // add user to role of InstitutionAdmin
                    var userRole = new IdentityUserRole<string>
                    {
                        RoleId = CustomRoles.InstitutionAdmin,
                        UserId = newPortalUser.Id
                    };
                    context.UserRoles.Add(userRole);

                    // Add them to the institution users
                    var newInstitutionUser = new InstitutionUser
                    {
                        InstitutionId = newInstitution.Id,
                        PortalUserId = newPortalUser.Id
                    };
                    context.InstitutionUsers.Add(newInstitutionUser);

                    await context.SaveChangesAsync();
                    // Generate Email Confirmation PasswordResetToken
                    var token = await _userManager.GenerateEmailConfirmationTokenAsync(newPortalUser);
                    var encodedUserId = HttpUtility.UrlEncode(newPortalUser.Id);
                    var encodedCode = HttpUtility.UrlEncode(token);
                    var callbackUrl = $"{_configuration["FrontEndUrl"]}/complete-institution-setup?userId={encodedUserId}&code={encodedCode}";

                    var adminSubject = "Institution Application Approved";
                    var adminMessage = "<html><head><style>" +
                                        "body { font-family: Arial, sans-serif; background-color: #f5f5f5; margin: 0; padding: 0; }" +
                                        ".container { max-width: 600px; margin: 0 auto; background-color: #ffffff; padding: 20px; }" +
                                        "h1 { color: #333; }" +
                                        "p { font-size: 16px; color: #555; }" +
                                        "a { text-decoration: none; color: #007BFF; font-weight: bold; }" +
                                        "a:hover { text-decoration: underline; }" +
                                        "</style></head><body>" +
                                        "<div class='container'>" +
                                        "<h1>School Application Approved</h1>" +
                                        $"Hello {institutionApplication.AdministratorName}," +
                                        "<p>We are delighted to announce that your institution's application has been approved, granting you access to the Quotation Board. Your role as the authorized representative of the institution is crucial for the successful use of our platform.</p>" +
                                        "<p>Here's what to expect as you embark on this journey:</p>" +
                                        "<ol>" +
                                        $"<li><strong>Login Credentials:</strong> You will use your  email address, {institutionApplication.AdministratorEmail}, for logging in to the Quotations Board Platform.</li>" +
                                        "<li><strong>Managing Institution Users:</strong> As the designated representative, you will have the responsibility to manage users on behalf of the institution. This includes handling account management, and ensuring a safe experience for your institutions's participants.</li>" +
                                        "</ol>" +
                                        "<p>To complete the setup of your account and set your password, please click the link below:</p>" +
                                        $"<p><a href='{callbackUrl}'>Set Up Your Password</a> (You will be redirected to a page where you can create your new password)</p>" +
                                        "<p>If you encounter any issues or have questions, our support team is ready to assist you. Simply reply to this email or contact us at <a href='mailto:support@agilebiz.co.ke'>support@yourcompany.com</a>.</p>" +
                                        "<p>Thank you for choosing us as your partner. We're excited to see your institution thrive on our platform!</p>" +
                                        "<p>Best regards,</p>" +
                                        "<p>Agile Business Solutions</p>" +
                                        "</div></body></html>";
                    await UtilityService.SendEmailAsync(institutionApplication.AdministratorEmail, adminSubject, adminMessage);

                    return Ok("Application Approved");
                }
            }
            catch (Exception Ex)
            {
                UtilityService.LogException(Ex);
                return StatusCode(500, UtilityService.HandleException(Ex));
            }
        }

        // Reject Application
        [HttpPost("RejectApplication/{id}")]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [SwaggerOperation(Summary = "Reject Institution Application", Description = "Rejects an institution application", OperationId = "RejectInstitutionApplication")]
        [Authorize(Roles = CustomRoles.SuperAdmin, AuthenticationSchemes = "Bearer")]
        public async Task<IActionResult> RejectInstitutionApplicationAsync(string id)
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
                                        "<h1>School Application Rejected</h1>" +
                                        $"Hello {institutionApplication.AdministratorName}," +
                                        "<p>We are sorry to announce that your institution's application has been rejected. This means that you will not be able to access the Quotation Board." +
                                        "<ol>";
                    if (institutionApplication.RejectionReason != null)
                    {
                        adminMessage += $"<li><strong>Reason for Rejection:</strong> {institutionApplication.RejectionReason}</li>";
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
    }
}