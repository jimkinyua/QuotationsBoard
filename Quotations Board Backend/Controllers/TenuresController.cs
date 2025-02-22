﻿using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace Quotations_Board_Backend.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    [Authorize(Roles = CustomRoles.SuperAdmin, AuthenticationSchemes = "Bearer")]
    public class TenuresController : ControllerBase
    {
        // create a new Tenure 

        [HttpPost]
        [Route("CreateTenure")]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status500InternalServerError)]

        public async Task<IActionResult> CreateTenure([FromBody] AddTenure tenure)
        {
            if (!ModelState.IsValid)
            {
                return BadRequest(ModelState);
            }

            using (var context = new QuotationsBoardContext())
            {
                try
                {
                    LoginTokenDTO TokenContents = UtilityService.GetUserIdFromCurrentRequest(Request);
                    var userId = UtilityService.GetUserIdFromToken(Request);
                    var user = context.Users.FirstOrDefault(u => u.Id == userId);
                    if (user == null)
                    {
                        return Unauthorized();
                    }
                    // does the tenure already exist?
                    var tenureExists = context.Tenures.Any(t => t.Tenor == tenure.Tenor && t.IsDeleted == false);
                    if (tenureExists)
                    {
                        return BadRequest("Tenure already exists");
                    }
                    var newTenure = new Tenure
                    {
                        Name = tenure.Name,
                        Tenor = tenure.Tenor,
                        MaximumAmount = 0,
                        CreatedBy = user.Id,
                        CreatedAt = DateTime.Now,
                        UpdatedAt = DateTime.Now,
                        IsDeleted = false,
                        IsValidationEnabled = true
                    };
                    context.Tenures.Add(newTenure);
                    await context.SaveChangesAsync();
                    return Ok();
                }
                catch (Exception Ex)
                {
                    UtilityService.LogException(Ex);
                    return StatusCode(500, UtilityService.HandleException(Ex));
                }

            }
        }

        // update a Tenure

        [HttpPut]
        [Route("UpdateTenure")]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        public async Task<IActionResult> UpdateTenure([FromBody] EditTenure tenure)
        {
            if (!ModelState.IsValid)
            {
                return BadRequest(ModelState);
            }

            using (var context = new QuotationsBoardContext())
            {
                try
                {
                    LoginTokenDTO TokenContents = UtilityService.GetUserIdFromCurrentRequest(Request);
                    var userId = UtilityService.GetUserIdFromToken(Request);
                    var user = context.Users.FirstOrDefault(u => u.Id == userId);
                    if (user == null)
                    {
                        return Unauthorized();
                    }
                    var tenureToUpdate = context.Tenures.FirstOrDefault(t => t.Id == tenure.Id);
                    if (tenureToUpdate == null)
                    {
                        return NotFound();
                    }
                    tenureToUpdate.Name = tenure.Name;
                    tenureToUpdate.Tenor = tenure.Tenor;
                    tenureToUpdate.UpdatedAt = DateTime.Now;
                    tenureToUpdate.UpdatedBy = user.Id;
                    context.Tenures.Update(tenureToUpdate);
                    await context.SaveChangesAsync();
                    return Ok();
                }
                catch (Exception Ex)
                {
                    UtilityService.LogException(Ex);
                    return StatusCode(500, UtilityService.HandleException(Ex));
                }

            }
        }

        // delete a Tenure

        [HttpDelete]
        [HttpDelete("DeleteTenure/{tenureId}")]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        [ProducesResponseType(StatusCodes.Status500InternalServerError)]
        public async Task<IActionResult> DeleteTenure(string tenureId)
        {
            using (var context = new QuotationsBoardContext())
            {
                try
                {
                    var tenureToDelete = context.Tenures.FirstOrDefault(t => t.Id == tenureId);
                    if (tenureToDelete == null)
                    {
                        return NotFound();
                    }
                    tenureToDelete.IsDeleted = true;
                    tenureToDelete.DeletedAt = DateTime.Now;
                    context.Tenures.Update(tenureToDelete);
                    await context.SaveChangesAsync();
                    return Ok();
                }
                catch (Exception Ex)
                {
                    UtilityService.LogException(Ex);
                    return StatusCode(500, UtilityService.HandleException(Ex));
                }

            }
        }

        // get all Tenures that are not deleted

        [HttpGet]
        [Route("GetTenures")]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status500InternalServerError)]

        public ActionResult<IEnumerable<TenureDTO>> GetTenures()
        {
            using (var context = new QuotationsBoardContext())
            {
                try // get all Tenures that are not deleted
                {
                    var tenures = context.Tenures.Where(t => t.IsDeleted == false).ToList();
                    var tenuresDTO = tenures.Select(t => new TenureDTO
                    {
                        Id = t.Id,
                        Name = t.Name,
                        Tenor = t.Tenor,
                        IsValidationEnabled = t.IsValidationEnabled
                    }).ToList();
                    return Ok(tenuresDTO.OrderBy(t => t.Tenor));
                }
                catch (Exception Ex)
                {
                    UtilityService.LogException(Ex);
                    return StatusCode(500, UtilityService.HandleException(Ex));
                }

            }
        }



        // enable validation for a Tenure

        [HttpPost]
        [Route("DisableEnableValidation")]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        [ProducesResponseType(StatusCodes.Status500InternalServerError)]
        public async Task<IActionResult> EnableValidation([FromBody] DisableEnableValidation disableEnableValidation)
        {
            if (!ModelState.IsValid)
            {
                return BadRequest(ModelState);
            }
            using (var context = new QuotationsBoardContext())
            {
                try
                {
                    var tenure = context.Tenures.FirstOrDefault(t => t.Id == disableEnableValidation.Id);
                    if (tenure == null)
                    {
                        return NotFound();
                    }
                    tenure.IsValidationEnabled = disableEnableValidation.IsValidationEnabled;
                    context.Tenures.Update(tenure);
                    await context.SaveChangesAsync();
                    return Ok();
                }
                catch (Exception Ex)
                {
                    UtilityService.LogException(Ex);
                    return StatusCode(500, UtilityService.HandleException(Ex));
                }

            }
        }


    }
}
