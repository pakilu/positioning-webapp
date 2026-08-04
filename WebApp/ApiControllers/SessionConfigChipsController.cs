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
    public class SessionConfigChipsController : ControllerBase
    {
        private readonly AppDbContext _context;

        public SessionConfigChipsController(AppDbContext context)
        {
            _context = context;
        }

        // GET: api/SessionConfigChips
        [HttpGet]
        public async Task<ActionResult<IEnumerable<SessionConfigChip>>> GetSessionConfigChips()
        {
            return await _context.SessionConfigChips.ToListAsync();
        }

        // GET: api/SessionConfigChips/5
        [HttpGet("{id}")]
        public async Task<ActionResult<SessionConfigChip>> GetSessionConfigChip(Guid id)
        {
            var sessionConfigChip = await _context.SessionConfigChips.FindAsync(id);

            if (sessionConfigChip == null)
            {
                return NotFound();
            }

            return sessionConfigChip;
        }

        // PUT: api/SessionConfigChips/5
        // To protect from overposting attacks, see https://go.microsoft.com/fwlink/?linkid=2123754
        [HttpPut("{id}")]
        public async Task<IActionResult> PutSessionConfigChip(Guid id, SessionConfigChip sessionConfigChip)
        {
            if (id != sessionConfigChip.Id)
            {
                return BadRequest();
            }

            _context.Entry(sessionConfigChip).State = EntityState.Modified;

            try
            {
                await _context.SaveChangesAsync();
            }
            catch (DbUpdateConcurrencyException)
            {
                if (!SessionConfigChipExists(id))
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

        // POST: api/SessionConfigChips
        // To protect from overposting attacks, see https://go.microsoft.com/fwlink/?linkid=2123754
        [HttpPost]
        public async Task<ActionResult<SessionConfigChip>> PostSessionConfigChip(SessionConfigChip sessionConfigChip)
        {
            _context.SessionConfigChips.Add(sessionConfigChip);
            await _context.SaveChangesAsync();

            return CreatedAtAction("GetSessionConfigChip", new { id = sessionConfigChip.Id }, sessionConfigChip);
        }

        public class UpdateCoordsDto
        {
            public decimal? X { get; set; }
            public decimal? Y { get; set; }
            public decimal? Z { get; set; }
        }

        // PATCH: api/SessionConfigChips/{id}/coords
        // Lightweight endpoint used by the live map to persist a dragged
        // anchor's new position without having to round-trip the whole entity.
        [HttpPatch("{id}/coords")]
        public async Task<IActionResult> PatchCoords(Guid id, [FromBody] UpdateCoordsDto dto)
        {
            if (dto == null)
            {
                return BadRequest();
            }

            // The DbContext is configured with NoTrackingWithIdentityResolution
            // (see Program.cs), so FindAsync/regular queries return untracked
            // entities and property changes would be silently ignored on
            // SaveChangesAsync. Force a tracking query so EF picks up the edits.
            var chip = await _context.SessionConfigChips
                .AsTracking()
                .FirstOrDefaultAsync(c => c.Id == id);
            if (chip == null)
            {
                return NotFound();
            }

            if (dto.X.HasValue) chip.XCoord = dto.X.Value;
            if (dto.Y.HasValue) chip.YCoord = dto.Y.Value;
            if (dto.Z.HasValue) chip.ZCoord = dto.Z.Value;
            chip.UpdatedAt = DateTime.UtcNow;

            await _context.SaveChangesAsync();
            return NoContent();
        }

        // DELETE: api/SessionConfigChips/5
        [HttpDelete("{id}")]
        public async Task<IActionResult> DeleteSessionConfigChip(Guid id)
        {
            var sessionConfigChip = await _context.SessionConfigChips.FindAsync(id);
            if (sessionConfigChip == null)
            {
                return NotFound();
            }

            _context.SessionConfigChips.Remove(sessionConfigChip);
            await _context.SaveChangesAsync();

            return NoContent();
        }

        private bool SessionConfigChipExists(Guid id)
        {
            return _context.SessionConfigChips.Any(e => e.Id == id);
        }
    }
}
