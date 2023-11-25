﻿using System.IdentityModel.Tokens.Jwt;
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
                    .Include(x => x.InstitutionUsers)
                    .FirstOrDefaultAsync(x => x.Email == login.Email);

                    if (user == null)
                    {
                        return BadRequest(new { message = "Invalid login attempt." });
                    }
                    Institution? institution = await context.Institutions.FirstOrDefaultAsync(x => x.Id == user.InstitutionUsers.FirstOrDefault().InstitutionId);
                    if (institution == null)
                    {
                        return BadRequest(new { message = "Invalid login attempt. Can't Find Your Insiti" });
                    }
                    if (!await _userManager.IsEmailConfirmedAsync(user))
                    {
                        var token = await _userManager.GenerateEmailConfirmationTokenAsync(user);
                        var encodedUserId = HttpUtility.UrlEncode(user.Id);
                        var encodedCode = HttpUtility.UrlEncode(token);
                        var callbackUrl = $"{_configuration["FrontEndUrl"]}/complete-institution-setup?userId={encodedUserId}&code={encodedCode}";
                        var adminSubject = "Confirm your email (Resend)";
                        var adminBody = $"Please confirm your account by <a href='{HtmlEncoder.Default.Encode(callbackUrl)}'>clicking here</a>.";
                        await UtilityService.SendEmailAsync(user.Email, adminSubject, adminBody);
                        return BadRequest(new { message = "Email not confirmed. Please check your email for a confirmation link." });
                    }

                    var result = await _signInManager.PasswordSignInAsync(user, login.Password, false, false);
                    if (result.Succeeded)
                    {
                        LoginTokenDTO loginTokenDTO = new LoginTokenDTO();
                        var roles = await _userManager.GetRolesAsync(user);
                        if (roles.Contains(CustomRoles.SuperAdmin))
                        {
                            loginTokenDTO.IsSuperAdmin = true;
                            loginTokenDTO.Role = CustomRoles.SuperAdmin;
                            loginTokenDTO.InstitutionId = "0";
                            loginTokenDTO.InstitutionName = "Agile Business Solutions";
                            loginTokenDTO.Name = user.UserName;
                            loginTokenDTO.Email = user.Email;
                        }
                        else if (roles.Contains(CustomRoles.InstitutionAdmin))
                        {
                            loginTokenDTO.Role = CustomRoles.InstitutionAdmin;
                            loginTokenDTO.InstitutionId = institution.Id;
                            loginTokenDTO.InstitutionName = institution.OrganizationName;
                            loginTokenDTO.Name = user.UserName;
                            loginTokenDTO.Email = user.Email;
                        }
                        else if (roles.Contains(CustomRoles.Dealer))
                        {
                            loginTokenDTO.IsSuperAdmin = false;
                            loginTokenDTO.Role = CustomRoles.Dealer;
                            loginTokenDTO.InstitutionId = institution.Id;
                            loginTokenDTO.InstitutionName = institution.OrganizationName;
                            loginTokenDTO.Name = user.UserName;
                            loginTokenDTO.Email = user.Email;
                        }
                        else if (roles.Contains(CustomRoles.ChiefDealer))
                        {
                            loginTokenDTO.IsSuperAdmin = false;
                            loginTokenDTO.Role = CustomRoles.ChiefDealer;
                            loginTokenDTO.InstitutionId = institution.Id;
                            loginTokenDTO.InstitutionName = institution.OrganizationName;
                            loginTokenDTO.Name = user.UserName;
                            loginTokenDTO.Email = user.Email;
                        }
                        else
                        {
                            return BadRequest(new { message = "Invalid login attempt. No Role" });
                        }

                        var claims = new List<Claim>
                        {
                            new Claim(ClaimTypes.NameIdentifier, user.Id),
                            new Claim(ClaimTypes.Email, user.Email),
                            new Claim(ClaimTypes.Role, string.Join(",", roles)),
                        };
                        JwtSecurityToken jwtSecurityToken = UtilityService.GenerateToken(claims);
                        loginTokenDTO.token = new JwtSecurityTokenHandler().WriteToken(jwtSecurityToken);
                        return Ok(loginTokenDTO);
                    }
                    return BadRequest(new { message = "Invalid login attempt. Pass Wong" });
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
                        return BadRequest(new { message = "Invalid login attempt." });
                    }
                    var token = await _userManager.GeneratePasswordResetTokenAsync(user);
                    var encodedUserId = HttpUtility.UrlEncode(user.Id);
                    var encodedCode = HttpUtility.UrlEncode(token);
                    var callbackUrl = $"{_configuration["FrontEndUrl"]}/reset-password?userId={encodedUserId}&code={encodedCode}";
                    var adminSubject = "Reset Password";
                    var adminBody = $"Please reset your password by <a href='{HtmlEncoder.Default.Encode(callbackUrl)}'>clicking here</a>.";
                    await UtilityService.SendEmailAsync(user.Email, adminSubject, adminBody);
                    return Ok(new { message = "Please check your email for a password reset link." });
                }
            }
            catch (Exception Ex)
            {
                UtilityService.LogException(Ex);
                return StatusCode(500, UtilityService.HandleException(Ex));
            }
        }

        [HttpPost("ResetPassword")]
        public async Task<IActionResult> ResetPassword(ResetPasswordDTO resetPassword)
        {
            if (UtilityService.IsDTOValid(resetPassword) == true)
            {

                var user = await _userManager.FindByIdAsync(resetPassword.UserId);
                if (user == null)
                {
                    return NotFound($"Unable to load user with ID '{resetPassword.UserId}'.");
                }

                var result = await _userManager.ResetPasswordAsync(user, resetPassword.Token, resetPassword.Password);
                if (result.Succeeded)
                {
                    return Ok();
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

            var UserId = UtilityService.GetUserIdFromToken(Request);
            if (UserId == null)
            {
                return Unauthorized();
            }

            var user = await _userManager.FindByIdAsync(UserId);

            if (user != null)
            {
                var result = await _userManager.ChangePasswordAsync(user, model.CurrentPassword, model.NewPassword);

                if (result.Succeeded)
                {
                    return Ok("Password changed successfully");
                }

                return StatusCode(StatusCodes.Status500InternalServerError, "Password change failed. Please try again later.");
            }

            return StatusCode(StatusCodes.Status404NotFound, "User not found!");
        }
    }
}
