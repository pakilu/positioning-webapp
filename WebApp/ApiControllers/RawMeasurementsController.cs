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
    public class RawMeasurementsController : ControllerBase
    {
        private readonly AppDbContext _context;

        public RawMeasurementsController(AppDbContext context)
        {
            _context = context;
        }

        // GET: api/RawMeasurements
        [HttpGet]
        public async Task<ActionResult<IEnumerable<RawMeasurement>>> GetRawMeasurements()
        {
            return await _context.RawMeasurements.ToListAsync();
        }

        // GET: api/RawMeasurements/5
        [HttpGet("{id}")]
        public async Task<ActionResult<RawMeasurement>> GetRawMeasurement(Guid id)
        {
            var rawMeasurement = await _context.RawMeasurements.FindAsync(id);

            if (rawMeasurement == null)
            {
                return NotFound();
            }

            return rawMeasurement;
        }

        // PUT: api/RawMeasurements/5
        // To protect from overposting attacks, see https://go.microsoft.com/fwlink/?linkid=2123754
        [HttpPut("{id}")]
        public async Task<IActionResult> PutRawMeasurement(Guid id, RawMeasurement rawMeasurement)
        {
            if (id != rawMeasurement.Id)
            {
                return BadRequest();
            }

            _context.Entry(rawMeasurement).State = EntityState.Modified;

            try
            {
                await _context.SaveChangesAsync();
            }
            catch (DbUpdateConcurrencyException)
            {
                if (!RawMeasurementExists(id))
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

        // POST: api/RawMeasurements
        // To protect from overposting attacks, see https://go.microsoft.com/fwlink/?linkid=2123754
        [HttpPost]
        public async Task<ActionResult<RawMeasurement>> PostRawMeasurement(RawMeasurement rawMeasurement)
        {
            _context.RawMeasurements.Add(rawMeasurement);
            await _context.SaveChangesAsync();

            return CreatedAtAction("GetRawMeasurement", new { id = rawMeasurement.Id }, rawMeasurement);
        }

        // DELETE: api/RawMeasurements/5
        [HttpDelete("{id}")]
        public async Task<IActionResult> DeleteRawMeasurement(Guid id)
        {
            var rawMeasurement = await _context.RawMeasurements.FindAsync(id);
            if (rawMeasurement == null)
            {
                return NotFound();
            }

            _context.RawMeasurements.Remove(rawMeasurement);
            await _context.SaveChangesAsync();

            return NoContent();
        }

        private bool RawMeasurementExists(Guid id)
        {
            return _context.RawMeasurements.Any(e => e.Id == id);
        }
    }
}
