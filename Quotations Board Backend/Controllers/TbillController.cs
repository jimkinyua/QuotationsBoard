using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Quotations_Board_Backend.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class TbillController : ControllerBase
    {
        // Allows Adding a new TBill from DTOs/TBill/NewTbill.cs
        [HttpPost("AddNewTbill")]
        [ProducesResponseType(StatusCodes.Status201Created)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        public async Task<ActionResult> AddNewTbill([FromBody] NewTbill newTbill)
        {
            // validate Model
            if (!ModelState.IsValid)
            {
                return BadRequest(ModelState);
            }

            try
            {
                var IssueNo = Guid.NewGuid().ToString().Substring(0, 6);
                // Maturity date is calculated from Issue date and Tenor
                var maturityDate = newTbill.IssueDate.AddMonths((int)newTbill.Tenor);
                using (var context = new QuotationsBoardContext())
                {
                    TBill newTBill = new TBill
                    {
                        IssueNumber = IssueNo, //newTbill.IssueNumber,
                        IssueDate = newTbill.IssueDate,
                        MaturityDate = maturityDate,
                        Tenor = newTbill.Tenor,
                        CreatedBy = "Admin",
                        CreatedOn = DateTime.Now
                    };
                    context.TBills.Add(newTBill);
                    await context.SaveChangesAsync();
                    return StatusCode(201, newTBill);
                }
            }
            catch (Exception Ex)
            {
                UtilityService.LogException(Ex);
                return StatusCode(500, UtilityService.HandleException(Ex));
            }
        }

        // Allows Editing an existing TBill from DTOs/TBill/EditTbill.cs
        [HttpPut("EditTbill/{id}")]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        public async Task<ActionResult> EditTbill(string id, [FromBody] EditTbill editTbill)
        {
            // validate Model
            if (!ModelState.IsValid)
            {
                return BadRequest(ModelState);
            }

            try
            {
                // Maturity date is calculated from Issue date and Tenor
                var maturityDate = editTbill.IssueDate.AddMonths((int)editTbill.Tenor);
                using (var context = new QuotationsBoardContext())
                {
                    var tbill = await context.TBills.FindAsync(id);
                    if (tbill == null)
                    {
                        return NotFound();
                    }
                    // tbill.IssueNumber = editTbill.IssueNumber;
                    tbill.IssueDate = editTbill.IssueDate;
                    tbill.MaturityDate = maturityDate;
                    tbill.Tenor = editTbill.Tenor;
                    context.Entry(tbill).State = EntityState.Modified;
                    await context.SaveChangesAsync();
                    return StatusCode(200, tbill);
                }
            }
            catch (Exception Ex)
            {
                UtilityService.LogException(Ex);
                return StatusCode(500, UtilityService.HandleException(Ex));
            }
        }

        // Allows Deleting an existing TBill
        [HttpDelete("DeleteTbill/{id}")]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        public async Task<ActionResult> DeleteTbill(string id)
        {
            try
            {
                using (var context = new QuotationsBoardContext())
                {
                    var tbill = await context.TBills.FindAsync(id);
                    if (tbill == null)
                    {
                        return NotFound();
                    }
                    context.TBills.Remove(tbill);
                    await context.SaveChangesAsync();
                    return StatusCode(200, tbill);
                }
            }
            catch (Exception Ex)
            {
                UtilityService.LogException(Ex);
                return StatusCode(500, UtilityService.HandleException(Ex));
            }
        }

        // Allows Getting all TBill from DTOs/TBill/TBillDTO.cs
        [HttpGet("GetAllTbills")]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        public async Task<ActionResult<List<TBillDTO>>> GetAllTbills()
        {
            try
            {
                using (var context = new QuotationsBoardContext())
                {
                    var tbills = await context.TBills
                        .Include(t => t.TBillYields)
                        .ToListAsync();
                    var tbillDTOs = new List<TBillDTO>();
                    foreach (var tbill in tbills)
                    {
                        decimal TBillYiled = 0;
                        var _yield = tbill.TBillYields.OrderByDescending(t => t.YieldDate).FirstOrDefault();
                        if (_yield != null)
                        {
                            TBillYiled = _yield.Yield;
                        }
                        var tbillDTO = new TBillDTO
                        {
                            Id = tbill.Id,
                            IssueNumber = tbill.IssueNumber,
                            IssueDate = tbill.IssueDate,
                            MaturityDate = tbill.MaturityDate,
                            Tenor = tbill.Tenor,
                            CreatedOn = tbill.CreatedOn,
                            Yield = TBillYiled
                        };
                        tbillDTOs.Add(tbillDTO);
                    }
                    return StatusCode(200, tbillDTOs);
                }
            }
            catch (Exception Ex)
            {
                UtilityService.LogException(Ex);
                return StatusCode(500, UtilityService.HandleException(Ex));
            }
        }

        // Allows user to capture the Yield of a TBill. Tbill can only have one Yield
        [HttpPost("AddTbillYield")]
        [ProducesResponseType(StatusCodes.Status201Created)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        public async Task<ActionResult> AddTbillYield([FromBody] NewTbillYield newTbillYield)
        {
            // validate Model
            if (!ModelState.IsValid)
            {
                return BadRequest(ModelState);
            }

            try
            {
                using (var context = new QuotationsBoardContext())
                {
                    var tbill = await context.TBills.Include(x => x.TBillYields).FirstOrDefaultAsync(x => x.Id == newTbillYield.TBillId);
                    if (tbill == null)
                    {
                        return NotFound("TBill not found");
                    }
                    // Ensure that TBill has no Yield 
                    if (tbill.TBillYields.Count > 0)
                    {
                        return BadRequest("TBill already has a Yield");
                    }
                    TBillYield newTBillYield = new TBillYield
                    {
                        YieldDate = tbill.IssueDate,
                        Yield = newTbillYield.Yield,
                        TBillId = newTbillYield.TBillId
                    };
                    context.TBillYields.Add(newTBillYield);
                    await context.SaveChangesAsync();
                    return StatusCode(201, newTBillYield);
                }
            }
            catch (Exception Ex)
            {
                UtilityService.LogException(Ex);
                return StatusCode(500, UtilityService.HandleException(Ex));
            }
        }

        // Allows user to edit the Yield of a TBill. Tbill can only have one Yield
        [HttpPut("EditTbillYield/{id}")]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        public async Task<ActionResult> EditTbillYield([FromBody] NewTbillYield editTbillYield)
        {
            // validate Model
            if (!ModelState.IsValid)
            {
                return BadRequest(ModelState);
            }

            try
            {
                using (var context = new QuotationsBoardContext())
                {
                    var tbill = await context.TBills.Include(x => x.TBillYields).FirstOrDefaultAsync(x => x.Id == editTbillYield.TBillId);
                    if (tbill == null)
                    {
                        return NotFound("TBill not found");
                    }
                    // Ensure that TBill has a Yield 
                    if (tbill.TBillYields.Count == 0)
                    {
                        return BadRequest("TBill has no Yield");
                    }
                    var tbillYield = tbill.TBillYields.FirstOrDefault();
                    if (tbillYield == null)
                    {
                        return NotFound("TBill Yield not found");
                    }
                    tbillYield.Yield = editTbillYield.Yield;
                    context.Entry(tbillYield).State = EntityState.Modified;
                    await context.SaveChangesAsync();
                    return StatusCode(200, tbillYield);
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
