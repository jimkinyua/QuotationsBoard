using AutoMapper;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Quotations_Board_Backend.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    [Authorize(Roles = CustomRoles.SuperAdmin, AuthenticationSchemes = "Bearer")]
    public class BondsController : ControllerBase
    {
        private readonly QuotationsBoardContext _context;
        private readonly IMapper _mapper;

        public BondsController(QuotationsBoardContext context)
        {
            _context = context;
        }

        // GET: api/Bonds
        [HttpGet]
        public async Task<ActionResult<IEnumerable<Bond>>> GetBonds()
        {
            return await _context.Bonds.ToListAsync();
        }

        // GET: api/Bonds/5
        [HttpGet("BondDetails/{id}")]
        public async Task<ActionResult<Bond>> GetBond(string id)
        {
            var bond = await _context.Bonds.FindAsync(id);

            if (bond == null)
            {
                return NotFound();
            }

            return bond;
        }

        // PUT: api/Bonds/5
        [HttpPut("UpdateBond")]
        public async Task<IActionResult> PutBond(UpdateBondDTO bond)
        {
            // Map the DTO to the model
            var bondModel = _mapper.Map<Bond>(bond);
            _context.Entry(bondModel).State = EntityState.Modified;
            try
            {
                await _context.SaveChangesAsync();
            }
            catch (Exception Ex)
            {
                UtilityService.LogException(Ex);
                return StatusCode(500, UtilityService.HandleException(Ex));
            }

            return NoContent();
        }

        // POST: api/Bonds
        [HttpPost]
        public async Task<ActionResult<Bond>> PostBond(NewBondDTO bond)
        {
            // Model is valid?
            if (!ModelState.IsValid)
            {
                return BadRequest(ModelState);
            }
            if (await _context.Bonds.AnyAsync(b => b.Isin == bond.Isin))
            {
                return BadRequest("Bond with this ISIN already exists");
            }
            // Map the DTO to the model
            var bondModel = _mapper.Map<Bond>(bond);
            _context.Bonds.Add(bondModel);
            await _context.SaveChangesAsync();

            return CreatedAtAction("GetBond", new { id = bondModel.Id });
        }

        // DELETE: api/Bonds/5
        [HttpDelete("{id}")]
        public async Task<ActionResult<Bond>> DeleteBond(string id)
        {
            var bond = await _context.Bonds.FindAsync(id);
            if (bond == null)
            {
                return NotFound();
            }

            _context.Bonds.Remove(bond);
            await _context.SaveChangesAsync();

            return bond;
        }

        private bool BondExists(string id)
        {
            return _context.Bonds.Any(e => e.Id == id);
        }
    }
}
