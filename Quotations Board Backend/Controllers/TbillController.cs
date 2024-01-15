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
                    // Make sure that for this IssueDate, there is no TBill with the same Tenor
                    var _existingTbill = await context.TBills.FirstOrDefaultAsync(x => x.IssueDate.Date == newTbill.IssueDate.Date && x.Tenor == newTbill.Tenor);
                    if (_existingTbill != null)
                    {
                        return BadRequest("TBill with same Tenor already exists for this Issue Date");
                    }
                    TBill newTBill = new TBill
                    {
                        IssueNumber = IssueNo, //newTbill.IssueNumber,
                        IssueDate = newTbill.IssueDate,
                        MaturityDate = newTbill.IssueDate.AddDays((int)newTbill.Tenor),
                        Tenor = newTbill.Tenor,
                        CreatedBy = "Admin",
                        CreatedOn = DateTime.Now,
                        Yield = newTbill.Yield
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
                    .OrderByDescending(x => x.IssueDate)
                    .ToListAsync();
                    var tbillDTOs = new List<TBillDTO>();
                    foreach (var tbill in tbills)
                    {
                        var MostRecentTBillBeforeThis = await context.TBills
                        .Where(x => x.Tenor == tbill.Tenor && x.IssueDate < tbill.IssueDate)
                        .OrderByDescending(x => x.IssueDate)
                        .FirstOrDefaultAsync();
                        var ThenYield = MostRecentTBillBeforeThis != null ? MostRecentTBillBeforeThis.Yield : 0;
                        var Variance = tbill.Yield - ThenYield;

                        TBillDTO billDTO = new TBillDTO
                        {
                            Id = tbill.Id,
                            IssueDate = tbill.IssueDate,
                            MaturityDate = tbill.MaturityDate,
                            Tenor = tbill.Tenor,
                            Yield = tbill.Yield,
                            CreatedOn = tbill.CreatedOn,
                            Variance = Variance
                        };
                        tbillDTOs.Add(billDTO);
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


    }
}
