using System.Globalization;
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
    [Authorize(AuthenticationSchemes = "Bearer")]
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
            // skip matured bonds
            var bonds = await _context.Bonds.Where(b => b.MaturityDate.Date >= DateTime.Now.Date).ToListAsync();
            return bonds;
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

        // returns Bon Categories

        [HttpGet("BondCategories")]
        public async Task<ActionResult<IEnumerable<string>>> GetBondCategories()
        {
            return await Task.FromResult(new List<string> { "IFB", "FXD" });
        }

        // PUT: api/Bonds/5
        [HttpPut("UpdateBond")]
        public async Task<IActionResult> PutBond(UpdateBondDTO bond)
        {
            // Map the DTO to the model
            var mapper = new MapperConfiguration(cfg => cfg.CreateMap<UpdateBondDTO, Bond>()).CreateMapper();
            var bondModel = mapper.Map<Bond>(bond);
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
                return Ok();
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
        [HttpGet("BondIds")]
        public async Task<ActionResult<IEnumerable<BondIds>>> GetBondIds()
        {
            var bonds = await _context.Bonds.ToListAsync();
            var bondIds = new List<BondIds>();
            foreach (var bond in bonds)
            {
                var bondId = new BondIds
                {
                    Id = bond.Id,
                    IssueNumber = bond.IssueNumber
                };
                bondIds.Add(bondId);
            }
            return bondIds;
        }

        // Matured Bonds
        [HttpGet("MaturedBonds")]
        public async Task<ActionResult<IEnumerable<Bond>>> GetMaturedBonds()
        {
            var bonds = await _context.Bonds.Where(b => b.MaturityDate.Date < DateTime.Now.Date).ToListAsync();
            return bonds;
        }

        // get the bond summary (trade aveges, quote averages, etc) given a bond id and date
        [HttpGet("BondPerformanceSummary/{bondId}/{date}")]
        public async Task<ActionResult<BondAverageStatistic>> GetBondPerformanceSummary(string bondId, string? date = "default")
        {
            var parsedDate = DateTime.Now;
            // if no date is specified, use today's date
            if (date == "default" || date == null || string.IsNullOrWhiteSpace(date))
            {
                parsedDate = DateTime.Now;
            }
            else
            {
                string[] formats = { "dd/MM/yyyy", "yyyy-MM-dd", "MM/dd/yyyy", "dd-MM-yyyy", "dd/MM/yyyy HH:mm:ss", "yyyy-MM-dd HH:mm:ss", "MM/dd/yyyy HH:mm:ss", "dd-MM-yyyy HH:mm:ss" };
                DateTime targetTradeDate;
                bool success = DateTime.TryParseExact(date, formats, CultureInfo.InvariantCulture, DateTimeStyles.None, out targetTradeDate);
                if (!success)
                {
                    return BadRequest("The date format is invalid");
                }
                parsedDate = targetTradeDate.Date;
            }
            var bondStatisticsDict = new Dictionary<string, BondAverageStatistic>();
            try
            {
                using (var db = new QuotationsBoardContext())
                {
                    var _quotations = await db.Quotations
                       .Include(x => x.Bond)
                       .Where(q => q.CreatedAt.Date == parsedDate.Date && q.BondId == bondId)
                       .ToListAsync();

                    var groupedQuotations = _quotations.GroupBy(x => x.BondId);

                    foreach (var _quote in groupedQuotations)
                    {
                        if (!bondStatisticsDict.TryGetValue(_quote.Key, out var bondStatistic))
                        {
                            bondStatistic = new BondAverageStatistic
                            {
                                BondId = _quote.Key,
                            };
                            bondStatisticsDict[_quote.Key] = bondStatistic;
                        }

                        var numberOfQuotes = _quote.Count();
                        var totalWeightedQuotedBuyYield = _quote.Where(x => x.BuyingYield > 0).Sum(x => x.BuyingYield * x.BuyVolume);
                        var totalWeightedQuotedSellYield = _quote.Where(x => x.SellingYield > 0).Sum(x => x.SellingYield * x.SellVolume);
                        var totalQuotedBuyVolume = _quote.Where(x => x.BuyingYield > 0).Sum(x => x.BuyVolume);
                        var totalQuotedSellVolume = _quote.Where(x => x.SellingYield > 0).Sum(x => x.SellVolume);
                        var combinedQuotedYield = totalWeightedQuotedBuyYield + totalWeightedQuotedSellYield;
                        var averageQuotedYield = combinedQuotedYield / (totalQuotedBuyVolume + totalQuotedSellVolume);
                        var totalQuotedVolume = _quote.Sum(x => x.BuyVolume + x.SellVolume);
                        var averageQuotedVolume = totalQuotedVolume / numberOfQuotes;
                        var averageQuotedBuyVolume = totalQuotedBuyVolume / numberOfQuotes;
                        var averageQuotedSellVolume = totalQuotedSellVolume / numberOfQuotes;

                        bondStatistic.BondName = _quote.First().Bond.IssueNumber;
                        bondStatistic.AverageWeightedQuotedYield = Math.Round(averageQuotedYield, 4, MidpointRounding.AwayFromZero);
                        bondStatistic.QuotedVolume = totalQuotedVolume;
                        bondStatistic.NumberofQuotes = numberOfQuotes;
                        // bondStatistic.TotalWeightedQuotedBuyYield = Math.Round(totalWeightedQuotedBuyYield, 4, MidpointRounding.AwayFromZero);
                        // bondStatistic.TotalWeightedQuotedSellYield = Math.Round(totalWeightedQuotedSellYield, 4, MidpointRounding.AwayFromZero);
                    }

                    var bondTrade = await db.BondTrades
                    .Include(x => x.BondTradeLines)
                    .Where(t => t.TradeDate.Date == parsedDate.Date)
                    .FirstOrDefaultAsync();

                    if (bondTrade != null)
                    {
                        // Extract the trade lines for the bond with the specified id then group them by the bond id
                        var filteredBondTradeLines = bondTrade.BondTradeLines.Where(x => x.BondId == bondId);
                        var groupedBondTradeLines = filteredBondTradeLines.GroupBy(x => x.BondId);

                        foreach (var bond_trade_line in groupedBondTradeLines)
                        {

                            if (!bondStatisticsDict.TryGetValue(bond_trade_line.Key, out var bondStatistic))
                            {
                                bondStatistic = new BondAverageStatistic
                                {
                                    BondId = bond_trade_line.Key,
                                };
                                bondStatisticsDict[bond_trade_line.Key] = bondStatistic;
                            }

                            //get related bond
                            var bond = await db.Bonds.Where(b => b.Id == bond_trade_line.Key).FirstOrDefaultAsync();

                            if (bond != null)
                            {
                                bondStatistic.BondName = bond.IssueNumber;
                            }
                            else
                            {
                                bondStatistic.BondName = "Bond not found";
                            }


                            var volTraded = bond_trade_line.Sum(x => x.ExecutedSize);
                            var totalweightedBuyYield = bond_trade_line.Where(x => x.Side == "BUY").Sum(x => x.Yield * x.ExecutedSize);
                            var totalweightedSellYield = bond_trade_line.Where(x => x.Side == "SELL").Sum(x => x.Yield * x.ExecutedSize);
                            var totalBuyVolume = bond_trade_line.Where(x => x.Side == "BUY").Sum(x => x.ExecutedSize);
                            var totalSellVolume = bond_trade_line.Where(x => x.Side == "SELL").Sum(x => x.ExecutedSize);
                            var numberofTrades = bond_trade_line.Count();
                            var combinedYield = totalweightedBuyYield + totalweightedSellYield;
                            var averageYield = combinedYield / volTraded;
                            var averageTradeVolume = volTraded / numberofTrades;

                            bondStatistic.AverageWeightedTradeYield = Math.Round(averageYield, 4, MidpointRounding.AwayFromZero);
                            bondStatistic.TradedVolume = volTraded;
                            bondStatistic.NumberofTrades = numberofTrades;
                            // bondStatistic.TotalWeightedTradeBuyYield = Math.Round(totalweightedBuyYield, 4, MidpointRounding.AwayFromZero);
                            // bondStatistic.TotalWeightedTradeSellYield = Math.Round(totalweightedSellYield, 4, MidpointRounding.AwayFromZero);

                        }

                    }
                    var m = bondStatisticsDict.Values.ToList();
                    if (m.Count == 0)
                    {
                        return NotFound("No data found for the specified bond id and date");
                    }

                    return Ok(m[0]);
                }
            }
            catch (Exception Ex)
            {
                UtilityService.LogException(Ex);
                return StatusCode(500, UtilityService.HandleException(Ex));
            }
        }

        // Fetches the Implied yields for all bonds for a given date
        [HttpGet("GetImpliedYieldsForAllBonds/{ForDate}")]
        public async Task<ActionResult<IEnumerable<BondImpliedYield>>> GetImpliedYieldsForAllBonds(string? ForDate = "default")
        {
            var parsedDate = DateTime.Now;
            IEnumerable<BondImpliedYield> bondImpliedYields = new List<BondImpliedYield>();
            // if no date is specified, use today's date
            if (ForDate == "default" || ForDate == null || string.IsNullOrWhiteSpace(ForDate))
            {
                parsedDate = DateTime.Now;
            }
            else
            {
                string[] formats = { "dd/MM/yyyy", "yyyy-MM-dd", "MM/dd/yyyy", "dd-MM-yyyy", "dd/MM/yyyy HH:mm:ss", "yyyy-MM-dd HH:mm:ss", "MM/dd/yyyy HH:mm:ss", "dd-MM-yyyy HH:mm:ss" };
                DateTime targetTradeDate;
                bool success = DateTime.TryParseExact(ForDate, formats, CultureInfo.InvariantCulture, DateTimeStyles.None, out targetTradeDate);
                if (!success)
                {
                    return BadRequest("The date format is invalid");
                }
                parsedDate = targetTradeDate.Date;
            }
            try
            {
                using (var db = new QuotationsBoardContext())
                {
                    var _impliedYields = await db.ImpliedYields
                       .Include(x => x.Bond)
                       .Where(q => q.YieldDate.Date == parsedDate.Date)
                       .ToListAsync();

                    bondImpliedYields = _impliedYields.Select(x => new BondImpliedYield
                    {
                        BondId = x.BondId,
                        Yield = x.Yield,
                        YieldDate = x.YieldDate,
                        IssueNumber = x.Bond.IssueNumber
                    });

                    return Ok(bondImpliedYields);
                }
            }
            catch (Exception Ex)
            {
                UtilityService.LogException(Ex);
                return StatusCode(500, UtilityService.HandleException(Ex));
            }
        }

        // Allows user to Add Implied Yields for a given bond and date
        [HttpPost("AddImpliedYield")]
        [Authorize(CustomRoles.SuperAdmin, AuthenticationSchemes = "Bearer")]
        public async Task<IActionResult> AddImpliedYield(AddImpliedYieldDTO impliedYield)
        {
            // Model is valid?
            if (!ModelState.IsValid)
            {
                return BadRequest(ModelState);
            }

            try
            {
                // Ensure Bond exists
                var bond = await _context.Bonds.FindAsync(impliedYield.BondId);
                if (bond == null)
                {
                    return BadRequest("Bond with this Id does not exist");
                }

                // Ensure Implied Yield does not exist for the specified bond and date
                var impliedYieldExists = await _context.ImpliedYields.AnyAsync(i => i.BondId == impliedYield.BondId && i.YieldDate.Date == impliedYield.YieldDate.Date);
                if (impliedYieldExists)
                {
                    return BadRequest("Implied Yield for this bond and date already exists");
                }

                ImpliedYield impliedYieldModel = new ImpliedYield
                {
                    BondId = impliedYield.BondId,
                    Yield = impliedYield.Yield,
                    YieldDate = impliedYield.YieldDate
                };

                _context.ImpliedYields.Add(impliedYieldModel);
                await _context.SaveChangesAsync();
                return Ok();
            }
            catch (System.Exception)
            {

                throw;
            }
        }
    }
}
