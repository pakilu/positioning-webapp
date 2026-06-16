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
    public class ChipsController : ControllerBase
    {
        private readonly AppDbContext _context;

        public ChipsController(AppDbContext context)
        {
            _context = context;
        }

        // GET: api/Chips
        [HttpGet]
        public async Task<ActionResult<IEnumerable<Chip>>> GetChips()
        {
            return await _context.Chips.ToListAsync();
        }

        // GET: api/Chips/5
        [HttpGet("{id}")]
        public async Task<ActionResult<Chip>> GetChip(Guid id)
        {
            var chip = await _context.Chips.FindAsync(id);

            if (chip == null)
            {
                return NotFound();
            }

            return chip;
        }

        // PUT: api/Chips/5
        // To protect from overposting attacks, see https://go.microsoft.com/fwlink/?linkid=2123754
        [HttpPut("{id}")]
        public async Task<IActionResult> PutChip(Guid id, Chip chip)
        {
            if (id != chip.Id)
            {
                return BadRequest();
            }

            try
            {
                var existing = await _context.Chips
                    .AsTracking()
                    .FirstOrDefaultAsync(x => x.Id == id);
                if (existing == null)
                {
                    return NotFound();
                }

                existing.Name = chip.Name;
                existing.DeviceIdentifier = chip.DeviceIdentifier.Trim();
                existing.Description = chip.Description;
                existing.UpdatedAt = DateTime.UtcNow;

                await _context.SaveChangesAsync();
            }
            catch (DbUpdateConcurrencyException)
            {
                if (!ChipExists(id))
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

        // POST: api/Chips
        // To protect from overposting attacks, see https://go.microsoft.com/fwlink/?linkid=2123754
        [HttpPost]
        public async Task<ActionResult<Chip>> PostChip(Chip chip)
        {
            _context.Chips.Add(chip);
            await _context.SaveChangesAsync();

            return CreatedAtAction("GetChip", new { id = chip.Id }, chip);
        }

        // DELETE: api/Chips/5
        [HttpDelete("{id}")]
        public async Task<IActionResult> DeleteChip(Guid id)
        {
            var chip = await _context.Chips.FindAsync(id);
            if (chip == null)
            {
                return NotFound();
            }

            _context.Chips.Remove(chip);
            await _context.SaveChangesAsync();

            return NoContent();
        }

        private bool ChipExists(Guid id)
        {
            return _context.Chips.Any(e => e.Id == id);
        }
    }
}
