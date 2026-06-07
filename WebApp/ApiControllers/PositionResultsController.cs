using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using App.DAL.EF;
using App.Domain;

namespace WebApp.ApiControllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class PositionResultsController : ControllerBase
    {
        private readonly AppDbContext _context;

        public PositionResultsController(AppDbContext context)
        {
            _context = context;
        }

        // GET: api/PositionResults
        [HttpGet]
        public async Task<ActionResult<IEnumerable<PositionResult>>> GetPositionResults()
        {
            return await _context.PositionResults.ToListAsync();
        }

        // GET: api/PositionResults/5
        [HttpGet("{id}")]
        public async Task<ActionResult<PositionResult>> GetPositionResult(Guid id)
        {
            var positionResult = await _context.PositionResults.FindAsync(id);

            if (positionResult == null)
            {
                return NotFound();
            }

            return positionResult;
        }

        // PUT: api/PositionResults/5
        // To protect from overposting attacks, see https://go.microsoft.com/fwlink/?linkid=2123754
        [HttpPut("{id}")]
        public async Task<IActionResult> PutPositionResult(Guid id, PositionResult positionResult)
        {
            if (id != positionResult.Id)
            {
                return BadRequest();
            }

            _context.Entry(positionResult).State = EntityState.Modified;

            try
            {
                await _context.SaveChangesAsync();
            }
            catch (DbUpdateConcurrencyException)
            {
                if (!PositionResultExists(id))
                {
                    return NotFound();
                }
                else
                {
                    throw;
                }
            }

            return NoContent();
        }

        // POST: api/PositionResults
        // To protect from overposting attacks, see https://go.microsoft.com/fwlink/?linkid=2123754
        [HttpPost]
        public async Task<ActionResult<PositionResult>> PostPositionResult(PositionResult positionResult)
        {
            _context.PositionResults.Add(positionResult);
            await _context.SaveChangesAsync();

            return CreatedAtAction("GetPositionResult", new { id = positionResult.Id }, positionResult);
        }

        // DELETE: api/PositionResults/5
        [HttpDelete("{id}")]
        public async Task<IActionResult> DeletePositionResult(Guid id)
        {
            var positionResult = await _context.PositionResults.FindAsync(id);
            if (positionResult == null)
            {
                return NotFound();
            }

            _context.PositionResults.Remove(positionResult);
            await _context.SaveChangesAsync();

            return NoContent();
        }

        private bool PositionResultExists(Guid id)
        {
            return _context.PositionResults.Any(e => e.Id == id);
        }
    }
}
