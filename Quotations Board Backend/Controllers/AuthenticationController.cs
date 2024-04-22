using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text.Encodings.Web;
using System.Web;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Quotations_Board_Backend.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    [AllowAnonymous]
    public class AuthenticationController : ControllerBase
    {
        private readonly UserManager<PortalUser> _userManager;
        private readonly SignInManager<PortalUser> _signInManager;
        private readonly IConfiguration _configuration;
        private readonly RoleManager<IdentityRole> _roleManager;

        public AuthenticationController(UserManager<PortalUser> userManager, SignInManager<PortalUser> signInManager, IConfiguration configuration, RoleManager<IdentityRole> roleManager)
        {
            _userManager = userManager;
            _signInManager = signInManager;
            _configuration = configuration;
            _roleManager = roleManager;
        }

        // Login
        [AllowAnonymous]
        [HttpPost("Login")]
        public async Task<IActionResult> Login(LoginDTO login)
        {
            try
            {
                if (!ModelState.IsValid)
                {
                    return BadRequest(ModelState);
                }
                using (var context = new QuotationsBoardContext())
                {
                    PortalUser? user = await context.Users
                    .FirstOrDefaultAsync(x => x.Email == login.Email);

                    if (user == null)
                    {
                        return BadRequest("Invalid login attempt.");
                    }

                    // is account disabled ?
                    if (await _userManager.IsLockedOutAsync(user))
                    {
                        return BadRequest("User account disabled.");
                    }

                    Institution? institution = await context.Institutions.FirstOrDefaultAsync(x => x.Id == user.InstitutionId);
                    if (institution == null)
                    {
                        return BadRequest("Invalid login attempt. Can't Find Your Insiti");
                    }
                    if (!await _userManager.IsEmailConfirmedAsync(user))
                    {
                        var token1 = await _userManager.GenerateEmailConfirmationTokenAsync(user);
                        var encodedUserId = HttpUtility.UrlEncode(user.Id);
                        var encodedCode = HttpUtility.UrlEncode(token1);
                        var callbackUrl = $"{_configuration["FrontEndUrl"]}/complete-institution-setup?userId={encodedUserId}&code={encodedCode}";

                        var adminSubject = "Confirm your email (Resend)";

                        var adminBody = $@"
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
                                                    .button {{
                                                        display: inline-block;
                                                        padding: 10px 20px;
                                                        background-color: #007bff;
                                                        color: #ffffff;
                                                        text-decoration: none;
                                                        border-radius: 5px;
                                                    }}
                                                    .button:hover {{
                                                        background-color: #0056b3;
                                                    }}
                                                </style>
                                            </head>
                                            <body>
                                                <div class='container'>
                                                    <h2>Confirm Your Email</h2>
                                                    <p>Please confirm your account by clicking the button below.</p>
                                                    <a href='{HtmlEncoder.Default.Encode(callbackUrl)}' class='button'>Confirm Email</a>
                                                </div>
                                            </body>
                                        </html>";


                        await UtilityService.SendEmailAsync(user.Email, adminSubject, adminBody);
                        return BadRequest("Email not confirmed. Please check your email for a confirmation link.");
                    }

                    //var result = await _signInManager.CheckPasswordSignInAsync(user, login.Password, false);
                    var passwordCorrect = await _userManager.CheckPasswordAsync(user, login.Password);
                    if (!passwordCorrect)
                    {
                        return BadRequest("Invalid login attempt. Pass Wrong");
                    }

                    // Has user been locked out?
                    if (await _userManager.IsLockedOutAsync(user))
                    {
                        return BadRequest("User account locked out.");
                    }
                    // get role of current user. If SuperAdmin No Need for 2FA. Just return token
                    var roles = await _userManager.GetRolesAsync(user);
                    var TwoFactor = await _userManager.GetTwoFactorEnabledAsync(user);
                    if (TwoFactor)
                    {
                        var token = await _userManager.GenerateTwoFactorTokenAsync(user, "Email");
                        var subject = "Your Login Code";
                        var body = $@"
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
                                                .code {{
                                                    display: inline-block;
                                                    padding: 10px 20px;
                                                    background-color: #007bff;
                                                    color: #ffffff;
                                                    font-weight: bold;
                                                    border-radius: 5px;
                                                }}
                                            </style>
                                        </head>
                                        <body>
                                            <div class='container'>
                                                <h2>Your Login Code</h2>
                                                <p>Your login code is:</p>
                                                <span class='code'>{token}</span>
                                                <p>Please use this code to complete your login process.</p>
                                            </div>
                                        </body>
                                    </html>";


                        await UtilityService.SendEmailAsync(user.Email, subject, body);
                        return Ok("Please check your email for a two factor login code.");
                    }

                    LoginTokenDTO loginTokenDTO = new LoginTokenDTO();
                    if (roles.Contains(CustomRoles.SuperAdmin))
                    {
                        loginTokenDTO.IsSuperAdmin = true;
                        loginTokenDTO.Role = CustomRoles.SuperAdmin;
                        loginTokenDTO.InstitutionId = "0";
                        loginTokenDTO.InstitutionName = institution.OrganizationName;
                        loginTokenDTO.Name = user.FirstName + " " + user.LastName;
                        loginTokenDTO.Email = user.Email;
                    }
                    else if (roles.Contains(CustomRoles.InstitutionAdmin))
                    {
                        loginTokenDTO.Role = CustomRoles.InstitutionAdmin;
                        loginTokenDTO.InstitutionId = institution.Id;
                        loginTokenDTO.InstitutionName = institution.OrganizationName;
                        loginTokenDTO.Name = user.FirstName + " " + user.LastName;
                        loginTokenDTO.Email = user.Email;
                    }
                    else if (roles.Contains(CustomRoles.Dealer))
                    {
                        loginTokenDTO.IsSuperAdmin = false;
                        loginTokenDTO.Role = CustomRoles.Dealer;
                        loginTokenDTO.InstitutionId = institution.Id;
                        loginTokenDTO.InstitutionName = institution.OrganizationName;
                        loginTokenDTO.Name = user.FirstName + " " + user.LastName;
                        loginTokenDTO.Email = user.Email;
                    }
                    else if (roles.Contains(CustomRoles.ChiefDealer))
                    {
                        loginTokenDTO.IsSuperAdmin = false;
                        loginTokenDTO.Role = CustomRoles.ChiefDealer;
                        loginTokenDTO.InstitutionId = institution.Id;
                        loginTokenDTO.InstitutionName = institution.OrganizationName;
                        loginTokenDTO.Name = user.FirstName + " " + user.LastName;
                        loginTokenDTO.Email = user.Email;
                    }
                    else if (roles.Contains(CustomRoles.NseSRO))
                    {
                        loginTokenDTO.IsSuperAdmin = false;
                        loginTokenDTO.Role = CustomRoles.NseSRO;
                        loginTokenDTO.InstitutionId = institution.Id;
                        loginTokenDTO.InstitutionName = institution.OrganizationName;
                        loginTokenDTO.Name = user.FirstName + " " + user.LastName;
                        loginTokenDTO.Email = user.Email;
                    }
                    else if (roles.Contains(CustomRoles.CentralBank))
                    {
                        loginTokenDTO.IsSuperAdmin = false;
                        loginTokenDTO.Role = CustomRoles.CentralBank;
                        loginTokenDTO.InstitutionId = institution.Id;
                        loginTokenDTO.InstitutionName = institution.OrganizationName;
                        loginTokenDTO.Name = user.FirstName + " " + user.LastName;
                        loginTokenDTO.Email = user.Email;
                    }
                    else if (roles.Contains(CustomRoles.CapitalMarketsRegulator))
                    {
                        loginTokenDTO.IsSuperAdmin = false;
                        loginTokenDTO.Role = CustomRoles.CapitalMarketsRegulator;
                        loginTokenDTO.InstitutionId = institution.Id;
                        loginTokenDTO.InstitutionName = institution.OrganizationName;
                        loginTokenDTO.Name = user.FirstName + " " + user.LastName;
                        loginTokenDTO.Email = user.Email;
                    }
                    var claims = new List<Claim>
                                 {
                                     new Claim(ClaimTypes.NameIdentifier, user.Id),
                                     new Claim(ClaimTypes.Email, user.Email),
                                     new Claim(ClaimTypes.Role, string.Join(",", roles)),
                                     new Claim("InstitutionId", institution.Id),
                                     new Claim("InstitutionName", institution.OrganizationName),
                                     new Claim("IsSuperAdmin", loginTokenDTO.IsSuperAdmin.ToString())
                                 };
                    JwtSecurityToken jwtSecurityToken = UtilityService.GenerateToken(claims);
                    loginTokenDTO.token = new JwtSecurityTokenHandler().WriteToken(jwtSecurityToken);
                    return Ok(loginTokenDTO);
                }
            }
            catch (Exception Ex)
            {
                UtilityService.LogException(Ex);
                return StatusCode(500, UtilityService.HandleException(Ex));
            }
        }

        // Forgot Password
        [AllowAnonymous]
        [HttpPost("ForgotPassword")]
        public async Task<IActionResult> ForgotPassword(ForgotPasswordDTO forgotPasswordDTO)
        {
            try
            {
                if (!ModelState.IsValid)
                {
                    return BadRequest(ModelState);
                }
                using (var context = new QuotationsBoardContext())
                {
                    PortalUser? user = await context.Users.FirstOrDefaultAsync(x => x.Email == forgotPasswordDTO.Email);
                    if (user == null)
                    {
                        return BadRequest("Invalid login attempt.");
                    }
                    // locked out ?
                    if (await _userManager.IsLockedOutAsync(user))
                    {
                        return BadRequest("User account locked out.");
                    }
                    var token = await _userManager.GeneratePasswordResetTokenAsync(user);
                    var encodedUserId = HttpUtility.UrlEncode(user.Id);
                    var encodedCode = HttpUtility.UrlEncode(token);
                    var callbackUrl = $"{_configuration["FrontEndUrl"]}/reset-password?userId={encodedUserId}&code={encodedCode}";
                    var adminSubject = "Reset Password";
                    // var adminBody = $"Please reset your password by <a href='{HtmlEncoder.Default.Encode(callbackUrl)}'>clicking here</a>.";

                    var adminBody = $@"
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
                                                    .button {{
                                                        display: inline-block;
                                                        padding: 10px 20px;
                                                        background-color: #007bff;
                                                        color: #ffffff;
                                                        text-decoration: none;
                                                        border-radius: 5px;
                                                    }}
                                                    .button:hover {{
                                                        background-color: #0056b3;
                                                    }}
                                                </style>
                                            </head>
                                            <body>
                                                <div class='container'>
                                                    <h2>Reset Your Password</h2>
                                                    <p>Please click the button below to reset your password.</p>
                                                    <a href='{HtmlEncoder.Default.Encode(callbackUrl)}' class='button'>Reset Password</a>
                                                </div>
                                            </body>
                                        </html>
                                    ";
                    await UtilityService.SendEmailAsync(user.Email, adminSubject, adminBody);
                    return Ok("Please check your email for a password reset link.");
                }
            }
            catch (Exception Ex)
            {
                UtilityService.LogException(Ex);
                return StatusCode(500, UtilityService.HandleException(Ex));
            }
        }

        // Vaidate 2FA Token and Login
        [AllowAnonymous]
        [HttpPost("TwoFactorLogin")]
        public async Task<IActionResult> TwoFactorLogin(TwoFactorLoginDTO twoFactorLoginDTO)
        {
            try
            {
                if (!ModelState.IsValid)
                {
                    return BadRequest(ModelState);
                }
                using (var context = new QuotationsBoardContext())
                {
                    PortalUser? user = await context.Users.Include(x => x.Institution).FirstOrDefaultAsync(x => x.Email == twoFactorLoginDTO.Email);
                    if (user == null)
                    {
                        return BadRequest("Seems like you provided an invalid login attempt.");
                    }
                    //var result = await _signInManager.TwoFactorSignInAsync("Email", twoFactorLoginDTO.TwoFactorCode, false, false);
                    var isTwoFactorTokenValid = await _userManager.VerifyTwoFactorTokenAsync(user, "Email", twoFactorLoginDTO.TwoFactorCode);

                    if (!isTwoFactorTokenValid)
                    {
                        return BadRequest("Seems like you provided an invalid login attempt.");
                    }

                    if (isTwoFactorTokenValid)
                    {
                        LoginTokenDTO loginTokenDTO = new LoginTokenDTO();
                        var roles = await _userManager.GetRolesAsync(user);
                        if (roles.Contains(CustomRoles.SuperAdmin))
                        {
                            loginTokenDTO.IsSuperAdmin = true;
                            loginTokenDTO.Role = CustomRoles.SuperAdmin;
                            loginTokenDTO.InstitutionId = "0";
                            loginTokenDTO.InstitutionName = "Agile Business Solutions";
                            loginTokenDTO.Name = user.FirstName + " " + user.LastName;
                            loginTokenDTO.Email = user.Email;
                        }
                        else if (roles.Contains(CustomRoles.InstitutionAdmin))
                        {
                            loginTokenDTO.Role = CustomRoles.InstitutionAdmin;
                            loginTokenDTO.InstitutionId = user.InstitutionId;
                            loginTokenDTO.InstitutionName = user.Institution.OrganizationName;
                            loginTokenDTO.Name = user.FirstName + " " + user.LastName;
                            loginTokenDTO.Email = user.Email;
                        }
                        else if (roles.Contains(CustomRoles.Dealer))
                        {
                            loginTokenDTO.IsSuperAdmin = false;
                            loginTokenDTO.Role = CustomRoles.Dealer;
                            loginTokenDTO.InstitutionId = user.InstitutionId;
                            loginTokenDTO.InstitutionName = user.Institution.OrganizationName;
                            loginTokenDTO.Name = user.FirstName + " " + user.LastName;
                            loginTokenDTO.Email = user.Email;
                        }
                        else if (roles.Contains(CustomRoles.ChiefDealer))
                        {
                            loginTokenDTO.IsSuperAdmin = false;
                            loginTokenDTO.Role = CustomRoles.ChiefDealer;
                            loginTokenDTO.InstitutionId = user.InstitutionId;
                            loginTokenDTO.InstitutionName = user.Institution.OrganizationName;
                            loginTokenDTO.Name = user.FirstName + " " + user.LastName;
                            loginTokenDTO.Email = user.Email;
                        }
                        else if (roles.Contains(CustomRoles.NseSRO))
                        {
                            loginTokenDTO.IsSuperAdmin = false;
                            loginTokenDTO.Role = CustomRoles.NseSRO;
                            loginTokenDTO.InstitutionId = user.InstitutionId;
                            loginTokenDTO.InstitutionName = user.Institution.OrganizationName;
                            loginTokenDTO.Name = user.FirstName + " " + user.LastName;
                            loginTokenDTO.Email = user.Email;
                        }
                        else if (roles.Contains(CustomRoles.CentralBank))
                        {
                            loginTokenDTO.IsSuperAdmin = false;
                            loginTokenDTO.Role = CustomRoles.CentralBank;
                            loginTokenDTO.InstitutionId = user.InstitutionId;
                            loginTokenDTO.InstitutionName = user.Institution.OrganizationName;
                            loginTokenDTO.Name = user.FirstName + " " + user.LastName;
                            loginTokenDTO.Email = user.Email;
                        }
                        else if (roles.Contains(CustomRoles.CapitalMarketsRegulator))
                        {
                            loginTokenDTO.IsSuperAdmin = false;
                            loginTokenDTO.Role = CustomRoles.CapitalMarketsRegulator;
                            loginTokenDTO.InstitutionId = user.InstitutionId;
                            loginTokenDTO.InstitutionName = user.Institution.OrganizationName;
                            loginTokenDTO.Name = user.FirstName + " " + user.LastName;
                            loginTokenDTO.Email = user.Email;
                        }

                        var claims = new List<Claim>
                        {
                            new Claim(ClaimTypes.NameIdentifier, user.Id),
                            new Claim(ClaimTypes.Email, user.Email),
                            new Claim(ClaimTypes.Role, string.Join(",", roles)),
                            new Claim("InstitutionId", user.InstitutionId),
                            new Claim("InstitutionName", user.Institution.OrganizationName),
                            new Claim("IsSuperAdmin", loginTokenDTO.IsSuperAdmin.ToString())
                        };
                        JwtSecurityToken jwtSecurityToken = UtilityService.GenerateToken(claims);
                        loginTokenDTO.token = new JwtSecurityTokenHandler().WriteToken(jwtSecurityToken);
                        return Ok(loginTokenDTO);
                    }


                    return BadRequest("Invalid login attempt.");
                }
            }
            catch (Exception Ex)
            {
                UtilityService.LogException(Ex);
                return StatusCode(500, UtilityService.HandleException(Ex));
            }
        }

        [HttpGet]
        [AllowAnonymous]
        [Route("ValidatePasswordResetToken")]

        public async Task<IActionResult> ValidatePasswordResetToken([FromQuery] string userId, [FromQuery] string code)
        {
            if (!ModelState.IsValid)
            {
                return BadRequest(ModelState);
            }

            // Find User by ResetToken.
            var user = await _userManager.FindByIdAsync(userId);

            if (user != null)
            {
                // Verify there PasswordResetToken

                var isTokenValid = await _userManager.VerifyUserTokenAsync(
                        user,
                        _userManager.Options.Tokens.PasswordResetTokenProvider,
                        UserManager<PortalUser>.ResetPasswordTokenPurpose,
                        code);

                if (isTokenValid)
                {
                    return Ok(new { Status = "Success", Message = "Password has been reset!", });

                }

                return StatusCode(StatusCodes.Status401Unauthorized, new
                {
                    Status = "Error",
                    Message = "The Link has expired. Please try again!"
                });
            }

            return StatusCode(StatusCodes.Status404NotFound, new
            {
                Status = "Error",
                Message = "The Link has expired. Please try again!"
            });
        }

        [HttpPost("ResetPassword")]
        public async Task<IActionResult> ResetPassword(ResetPasswordDTO resetPassword)
        {
            using (var context = new QuotationsBoardContext())
            {
                if (UtilityService.IsDTOValid(resetPassword) == true)
                {

                    var user = await _userManager.FindByIdAsync(resetPassword.UserId);
                    if (user == null)
                    {
                        return NotFound($"Unable to load user with ID '{resetPassword.UserId}'.");
                    }

                    // is account locked out?
                    if (await _userManager.IsLockedOutAsync(user))
                    {
                        return BadRequest("User account locked out.");
                    }

                    // Calidate Password
                    var passwordValidator = new PasswordValidator<PortalUser>();
                    var passwordValidationResult = await passwordValidator.ValidateAsync(_userManager, user, resetPassword.Password);

                    if (!passwordValidationResult.Succeeded)
                    {
                        var Passerrors = passwordValidationResult.Errors.Select(result => result.Description);
                        return BadRequest(Passerrors);
                    }


                    var result = await _userManager.ResetPasswordAsync(user, resetPassword.Token, resetPassword.Password);
                    if (result.Succeeded)
                    {
                        // update the user and set TwoFactorEnabled to true
                        user.TwoFactorEnabled = true;
                        await _userManager.UpdateAsync(user);
                        return Ok();
                    }
                    // did it fail due to invalid token?
                    if (result.Errors.Any(x => x.Code == "InvalidToken"))
                    {
                        //get user details 
                        var institutionUser = await _userManager.Users.FirstOrDefaultAsync(x => x.Id == resetPassword.UserId);
                        if (institutionUser == null)
                        {
                            return BadRequest("Invalid login attempt.");
                        }
                        var UserRoles = await _userManager.GetRolesAsync(institutionUser);
                        // has user set up password? if not and they have role inst admin send them a link to set up password
                        if (UserRoles.Contains(CustomRoles.InstitutionAdmin))
                        {
                            var instAdmin = await _userManager.Users.FirstOrDefaultAsync(x => x.Id == resetPassword.UserId);
                            if (instAdmin == null)
                            {
                                return BadRequest("Invalid login attempt.");
                            }
                            var actualInst = await context.Institutions.FirstOrDefaultAsync(x => x.Id == instAdmin.InstitutionId);
                            if (actualInst == null)
                            {
                                return BadRequest("Invalid login attempt. Can't Find Your Insititution");
                            }
                            var InstApplication = await context.InstitutionApplications.FirstOrDefaultAsync(x => x.InstitutionName == actualInst.OrganizationName);

                            if (InstApplication == null)
                            {
                                return BadRequest("Invalid login attempt. Can't Find Your Insititution");
                            }
                            // is the passoword field null?
                            if (string.IsNullOrEmpty(instAdmin.PasswordHash))
                            {
                                var token = await _userManager.GenerateEmailConfirmationTokenAsync(instAdmin);
                                var encodedUserId = HttpUtility.UrlEncode(instAdmin.Id);
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
                                                <p>Dear {InstApplication.AdministratorName},</p>
                                                <p>We are pleased to inform you that your application for access to the Quotation Board has been approved. As the authorized representative of your institution, you play a vital role in leveraging our platform for your institution's success.</p>
                                                <p>Key Information:</p>
                                                <ul>
                                                    <li><strong>Login Credentials:</strong> Please use your email address, {InstApplication.AdministratorEmail}, to log in to the Quotations Board Platform.</li>
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

                                await UtilityService.SendEmailAsync(instAdmin.Email, adminSubject, emailHtml);
                                return Ok("Please check your email for a password reset link.");
                            }
                        }

                    }

                    // If we got this far, something failed. Fetch the error list and display it.
                    var errors = result.Errors.Select(result => result.Description);
                    return BadRequest(errors);

                }
                else
                {
                    var errors = UtilityService.FetchDataAnnotationErrors(resetPassword);
                    return BadRequest(errors);
                }
            }

        }

        // Intentionally Change Password
        [Authorize(AuthenticationSchemes = "Bearer")]
        [HttpPost]
        [Route("ChangePassword")]

        public async Task<IActionResult> ChangePassword(ChangePasswordDTO model)
        {
            if (!ModelState.IsValid)
            {
                return BadRequest(ModelState);
            }

            LoginTokenDTO TokenDetails = UtilityService.GetUserIdFromCurrentRequest(Request);
            var UserId = UtilityService.GetUserIdFromToken(Request);
            if (TokenDetails == null)
            {
                return Unauthorized();
            }

            var user = await _userManager.FindByIdAsync(UserId);

            if (user != null)
            {
                var result = await _userManager.ChangePasswordAsync(user, model.CurrentPassword, model.NewPassword);

                if (result.Succeeded)
                {
                    user.TwoFactorEnabled = true;
                    await _userManager.UpdateAsync(user);
                    return Ok("Password changed successfully");
                }

                return StatusCode(StatusCodes.Status500InternalServerError, "Password change failed. Please try again later.");
            }

            return StatusCode(StatusCodes.Status404NotFound, "User not found!");
        }

        // Return User Details 
        [Authorize(AuthenticationSchemes = "Bearer")]
        [HttpGet]
        [Route("GetUserDetails")]
        public async Task<ActionResult<UserDetailsDTO>> GetUserDetails()
        {
            try
            {
                LoginTokenDTO TokenDetails = UtilityService.GetUserIdFromCurrentRequest(Request);
                var UserId = UtilityService.GetUserIdFromToken(Request);
                if (TokenDetails == null)
                {
                    return Unauthorized();
                }
                using (var context = new QuotationsBoardContext())
                {
                    PortalUser? user = await context.Users.FirstOrDefaultAsync(x => x.Id == UserId);
                    if (user == null)
                    {
                        return BadRequest("Invalid login attempt.");
                    }
                    Institution? institution = await context.Institutions.FirstOrDefaultAsync(x => x.Id == user.InstitutionId);
                    if (institution == null)
                    {
                        return BadRequest("Invalid login attempt. Can't Find Your Insiti");
                    }
                    UserDetailsDTO userDetailsDTO = new UserDetailsDTO();
                    userDetailsDTO.Email = user.Email;
                    userDetailsDTO.FirstName = user.FirstName;
                    userDetailsDTO.LastName = user.LastName;
                    userDetailsDTO.PhoneNumber = user.PhoneNumber;
                    userDetailsDTO.InstitutionName = institution.OrganizationName;
                    userDetailsDTO.Role = TokenDetails.Role;
                    return Ok(userDetailsDTO);
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
