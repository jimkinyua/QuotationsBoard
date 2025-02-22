﻿using Microsoft.AspNetCore.Authorization;
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
        //[Authorize(Roles = CustomRoles.SuperAdmin, AuthenticationSchemes = "Bearer")]

        public async Task<ActionResult> AddNewTbill([FromBody] NewTbill newTbill)
        {
            // validate Model
            if (!ModelState.IsValid)
            {
                return BadRequest(ModelState);
            }

            try
            {
                var (startOfCycle, endOfCycle) = TBillHelper.GetCurrentTBillCycle(newTbill.IssueDate);
                var IssueNo = Guid.NewGuid().ToString().Substring(0, 6);
                var maturityDate = newTbill.IssueDate.AddDays((int)newTbill.Tenor);
                using (var context = new QuotationsBoardContext())
                {


                    var existingTbill = await context.TBills
                    .AnyAsync(t => t.Tenor == newTbill.Tenor &&
                    t.IssueDate >= startOfCycle &&
                    t.IssueDate <= endOfCycle);

                    if (existingTbill)
                    {
                        // If a T-Bill with the same tenor in the same cycle exists, return an error
                        return BadRequest("A T-Bill with the same tenor for the week beginning " + startOfCycle.ToString("dd/MM/yyyy") + " and ending " + endOfCycle.ToString("dd/MM/yyyy") + " already exists.");
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
        [Authorize(Roles = CustomRoles.SuperAdmin, AuthenticationSchemes = "Bearer")]

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
                    tbill.Yield = editTbill.Yield;
                    context.Update(tbill);
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
        [Authorize(Roles = CustomRoles.SuperAdmin, AuthenticationSchemes = "Bearer")]

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
        // [Authorize(AuthenticationSchemes = "Bearer")]

        public async Task<ActionResult<TBillDTO>> GetAllTbills()
        {

            try
            {
                var Today = DateTime.Now;
                var (startOfCycle, endOfCycle) = TBillHelper.GetCurrentTBillCycle(Today);
                var (startOfLastWeek, endOfLastWeek) = TBillHelper.GetPreviousTBillCycle(Today);


                using (var context = new QuotationsBoardContext())
                {
                    var tbills = await context.TBills
                    .OrderByDescending(x => x.IssueDate)
                    .ToListAsync();
                    Dictionary<string, CurrentTbill> currentTbills = new Dictionary<string, CurrentTbill>();
                    // foreach tbill tenor pick the one for the most recent


                    // most recent tbills are within startOfLastWeek and endOfLastWeek
                    var mostRecentTbills = tbills.Where(x => x.IssueDate.Date >= startOfCycle.Date && x.IssueDate.Date <= endOfCycle.Date).ToList();

                    if (mostRecentTbills.Count > 0)
                    {
                        foreach (var tbill in mostRecentTbills)
                        {
                            if (!currentTbills.ContainsKey(tbill.Id))
                            {
                                var MostRecentTBillBeforeThis = await context.TBills
                                .Where(x => x.Tenor == tbill.Tenor && x.IssueDate < tbill.IssueDate)
                                .OrderByDescending(x => x.IssueDate)
                                .FirstOrDefaultAsync();
                                var ThenYield = MostRecentTBillBeforeThis != null ? MostRecentTBillBeforeThis.Yield : 0;
                                var Variance = tbill.Yield - ThenYield;

                                currentTbills.Add(tbill.Id, new CurrentTbill
                                {
                                    Id = tbill.Id,
                                    IssueDate = tbill.IssueDate,
                                    MaturityDate = tbill.MaturityDate,
                                    Tenor = tbill.Tenor,
                                    Yield = tbill.Yield,
                                    CreatedOn = tbill.CreatedOn,
                                    Variance = Variance,
                                    LastAuction = ThenYield
                                });
                            }
                        }
                    }

                    var tbillDTOs = new TBillDTO(
                    );
                    List<CurrentTbill> curr = new List<CurrentTbill>();
                    List<HistoricalTbill> hist = new List<HistoricalTbill>();
                    foreach (var item in currentTbills)
                    {
                        curr.Add(item.Value);
                    }

                    tbillDTOs.CurrentTbills = curr;


                    foreach (var tbill in tbills)
                    {
                        // if it has been added to currentTbills, skip it
                        if (currentTbills.ContainsKey(tbill.Id))
                        {
                            continue;
                        }

                        var MostRecentTBillBeforeThis = await context.TBills
                        .Where(x => x.Tenor == tbill.Tenor && x.IssueDate < tbill.IssueDate)
                        .OrderByDescending(x => x.IssueDate)
                        .FirstOrDefaultAsync();
                        var ThenYield = MostRecentTBillBeforeThis != null ? MostRecentTBillBeforeThis.Yield : 0;
                        var Variance = tbill.Yield - ThenYield;

                        HistoricalTbill billDTO = new HistoricalTbill
                        {
                            Id = tbill.Id,
                            IssueDate = tbill.IssueDate,
                            MaturityDate = tbill.MaturityDate,
                            Tenor = tbill.Tenor,
                            Yield = tbill.Yield,
                            CreatedOn = tbill.CreatedOn,
                            Variance = Variance,
                            LastAuction = ThenYield
                        };
                        hist.Add(billDTO);
                    }
                    tbillDTOs.HistoricalTbills = hist;
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
