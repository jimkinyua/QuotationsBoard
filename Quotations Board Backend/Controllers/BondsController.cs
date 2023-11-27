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
    // [Authorize(Roles = CustomRoles.SuperAdmin, AuthenticationSchemes = "Bearer")]
    [AllowAnonymous]
    public class BondsController : ControllerBase
    {
        private readonly QuotationsBoardContext _context;
        private readonly IMapper _mapper;

        public BondsController(QuotationsBoardContext context, IMapper mapper)
        {
            _context = context;
            _mapper = mapper;
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
        [HttpPost("CreateBond")]
        [AllowAnonymous]
        public async Task<ActionResult<Bond>> PostBond(NewBondDTO bond)
        {
            // Model is valid?
            if (!ModelState.IsValid)
            {
                return BadRequest(ModelState);
            }

            try
            {
                if (await _context.Bonds.AnyAsync(b => b.Isin == bond.Isin))
                {
                    return BadRequest("Bond with this ISIN already exists");
                }
                // Map the DTO to the model
                var mapper = new MapperConfiguration(cfg => cfg.CreateMap<NewBondDTO, Bond>()).CreateMapper();
                var bondModel = mapper.Map<Bond>(bond);
                _context.Bonds.Add(bondModel);
                await _context.SaveChangesAsync();
                return CreatedAtAction("GetBond", new { id = bondModel.Id });
            }
            catch (System.Exception)
            {

                throw;
            }
        }

        // DELETE: api/Bonds/5
        [HttpDelete("DeleteBond/{id}")]
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

        // Fetched Bond Types (Hard code values)
        [HttpGet("BondTypes")]
        public async Task<ActionResult<IEnumerable<string>>> GetBondTypes()
        {
            return await Task.FromResult(new List<string> { "Corporate", "Government" });
        }
    }
}
