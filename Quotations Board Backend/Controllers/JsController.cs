using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace Quotations_Board_Backend.Controllers
{
    [Route("Js/Content")]
    [ApiController]
    public class JsController : ControllerBase
    {
        [HttpGet("QuotedYieldCurve/{accessCode}")]
        public IActionResult GetQuotedYieldCurve_JsFile(string accessCode)
        {
            if (string.IsNullOrEmpty(accessCode))
            {
                return Unauthorized();
            }
            if (!IsValidAccessCode(accessCode))
            {
                return Unauthorized();
            }
            var jsFilePath = Path.Combine("wwwroot", "js", "QuotedYieldCurve.js");
            var jsFileContent = System.IO.File.ReadAllText(jsFilePath);
            return Content(jsFileContent, "application/javascript");
        }

        [HttpGet("YieldCurve/{accessCode}")]
        public IActionResult GetYieldCurve_JsFile(string accessCode)
        {
            if (string.IsNullOrEmpty(accessCode))
            {
                return Unauthorized();
            }
            if (!IsValidAccessCode(accessCode))
            {
                return Unauthorized();
            }
            var jsFilePath = Path.Combine("wwwroot", "js", "YieldCurve.js");
            var jsFileContent = System.IO.File.ReadAllText(jsFilePath);
            return Content(jsFileContent, "application/javascript");
        }

        // ensures that the access code is valid.
        private bool IsValidAccessCode(string accessCode)
        {
            using (var context = new QuotationsBoardContext())
            {
                return context.Institutions.Any(i => i.WidgetKey == accessCode);
            }
        }
    }


}
