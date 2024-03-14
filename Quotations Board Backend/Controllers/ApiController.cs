using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using QuotationsBoardBackend.DTOs.API;

namespace Quotations_Board_Backend.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    [Produces("application/json")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status500InternalServerError)]

    public class ApiController : ControllerBase
    {
        private readonly UserManager<PortalUser> _userManager;
        private readonly SignInManager<PortalUser> _signInManager;
        private readonly IConfiguration _configuration;
        private readonly RoleManager<IdentityRole> _roleManager;

        public ApiController(UserManager<PortalUser> userManager, SignInManager<PortalUser> signInManager, IConfiguration configuration, RoleManager<IdentityRole> roleManager)
        {
            _userManager = userManager;
            _signInManager = signInManager;
            _configuration = configuration;
            _roleManager = roleManager;
        }

        protected async Task<PortalUser> GetCurrentUserAsync()
        {
            return await _userManager.GetUserAsync(HttpContext.User);
        }

        protected async Task<string> GenerateJwtToken(PortalUser user)
        {
            using (var context = new QuotationsBoardContext())
            {
                var roles = await _userManager.GetRolesAsync(user);
                var InstitutionId = user.InstitutionId;
                var institution = await context.Institutions.FirstOrDefaultAsync(i => i.Id == InstitutionId);
                if (institution == null)
                {
                    throw new Exception("Institution not found");
                }

                var claims = new List<Claim>
                                 {
                                     new Claim(ClaimTypes.NameIdentifier, user.Id),
                                     new Claim(ClaimTypes.Email, user.Email),
                                     new Claim(ClaimTypes.Role, string.Join(",", roles)),
                                     new Claim("InstitutionId", institution.Id),
                                     new Claim("InstitutionName", institution.OrganizationName),
                                     new Claim("IsSuperAdmin", roles.Contains("SuperAdmin").ToString())
                                 };

                JwtSecurityToken token = UtilityService.GenerateToken(claims, true);
                return new JwtSecurityTokenHandler().WriteToken(token);
            }
        }

        [AllowAnonymous]
        [HttpPost("authenticate")]
        public async Task<IActionResult> Authenticate([FromBody] APILogin model)
        {
            if (!ModelState.IsValid)
            {
                return BadRequest("Invalid client request");
            }

            var user = await _userManager.FindByNameAsync(model.ClientId);

            if (user != null)
            {
                var result = await _signInManager.CheckPasswordSignInAsync(user, model.ClientSecret, false);

                if (result.Succeeded)
                {
                    var token = await GenerateJwtToken(user);
                    return Ok(new { token });
                }

                // get reason for failure
                var reason = result.IsNotAllowed ? "User is not allowed to sign in" : result.IsLockedOut ? "User is locked out" : "Invalid username or password";
                return BadRequest(reason);
            }

            return BadRequest("Could not authenticate user");
        }

        // get Yield Curve for a specific date
        [HttpPost("yieldcurve")]
        [Authorize(Roles = CustomRoles.APIUser, AuthenticationSchemes = "Bearer")]
        public async Task<ActionResult<IEnumerable<FinalYieldCurveData>>> GetYieldCurve([FromBody] APIYieldCurveRequest ApiRequest)
        {
            if (!ModelState.IsValid)
            {
                return BadRequest("Invalid client request. The date provided is not valid. The date should be in the format: yyyy-MM-dd");
            }

            try
            {
                // var user = await GetCurrentUserAsync();
                var userId = UtilityService.GetUserIdFromToken(Request);
                var user = await _userManager.FindByIdAsync(userId);
                if (user == null)
                {
                    return BadRequest("User not found");
                }
                DateTime parsedDate = new DateTime(ApiRequest.Date.Year, ApiRequest.Date.Month, ApiRequest.Date.Day);
                if (parsedDate == DateTime.MinValue)
                {
                    return BadRequest($"Invalid date provided: {ApiRequest.Date}");
                }

                using (var context = new QuotationsBoardContext())
                {
                    Dictionary<int, (double, double)> benchmarkRanges = YieldCurveHelper.GetBenchmarkRanges(parsedDate);
                    HashSet<double> tenuresThatRequireInterPolation = new HashSet<double>();
                    HashSet<double> tenuresThatDoNotRequireInterpolation = new HashSet<double>();
                    HashSet<string> usedBondIds = new HashSet<string>();
                    List<FinalYieldCurveData> YieldCurveToPlot = new List<FinalYieldCurveData>();
                    List<YieldCurveDataSet> yieldCurveCalculations = new List<YieldCurveDataSet>();
                    List<FinalYieldCurveData> previousCurve = new List<FinalYieldCurveData>();

                    AddOneYearTBillResult addResult = YieldCurveHelper.AddOneYearTBillToYieldCurve(parsedDate, tenuresThatDoNotRequireInterpolation, yieldCurveCalculations);
                    if (addResult.Success == false)
                    {
                        return BadRequest(addResult.ErrorMessage);
                    }
                    ProcessBenchmarkResult Mnaoes = YieldCurveHelper.ProcessYieldCurve(parsedDate, context, yieldCurveCalculations, benchmarkRanges, tenuresThatRequireInterPolation, tenuresThatDoNotRequireInterpolation, usedBondIds);
                    if (Mnaoes.Success == false)
                    {
                        return BadRequest(Mnaoes.ErrorMessage);
                    }
                    yieldCurveCalculations.AddRange(Mnaoes.YieldCurveCalculations);
                    YieldCurveHelper.InterpolateWhereNecessary(yieldCurveCalculations, tenuresThatRequireInterPolation, previousCurve);
                    YieldCurveToPlot = YieldCurveHelper.GenerateYieldCurves(tenuresThatRequireInterPolation, tenuresThatDoNotRequireInterpolation, yieldCurveCalculations);
                    return Ok(YieldCurveToPlot);

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
