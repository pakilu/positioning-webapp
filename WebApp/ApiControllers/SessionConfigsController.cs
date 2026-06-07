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
    public class SessionConfigsController : ControllerBase
    {
        private readonly AppDbContext _context;

        public SessionConfigsController(AppDbContext context)
        {
            _context = context;
        }

        // GET: api/SessionConfigs
        [HttpGet]
        public async Task<ActionResult<IEnumerable<SessionConfig>>> GetSessionConfigs()
        {
            return await _context.SessionConfigs.ToListAsync();
        }

        // GET: api/SessionConfigs/5
        [HttpGet("{id}")]
        public async Task<ActionResult<SessionConfig>> GetSessionConfig(Guid id)
        {
            var sessionConfig = await _context.SessionConfigs.FindAsync(id);

            if (sessionConfig == null)
            {
                return NotFound();
            }

            return sessionConfig;
        }

        // PUT: api/SessionConfigs/5
        // To protect from overposting attacks, see https://go.microsoft.com/fwlink/?linkid=2123754
        [HttpPut("{id}")]
        public async Task<IActionResult> PutSessionConfig(Guid id, SessionConfig sessionConfig)
        {
            if (id != sessionConfig.Id)
            {
                return BadRequest();
            }

            _context.Entry(sessionConfig).State = EntityState.Modified;

            try
            {
                await _context.SaveChangesAsync();
            }
            catch (DbUpdateConcurrencyException)
            {
                if (!SessionConfigExists(id))
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

        // POST: api/SessionConfigs
        // To protect from overposting attacks, see https://go.microsoft.com/fwlink/?linkid=2123754
        [HttpPost]
        public async Task<ActionResult<SessionConfig>> PostSessionConfig(SessionConfig sessionConfig)
        {
            _context.SessionConfigs.Add(sessionConfig);
            await _context.SaveChangesAsync();

            return CreatedAtAction("GetSessionConfig", new { id = sessionConfig.Id }, sessionConfig);
        }

        // DELETE: api/SessionConfigs/5
        [HttpDelete("{id}")]
        public async Task<IActionResult> DeleteSessionConfig(Guid id)
        {
            var sessionConfig = await _context.SessionConfigs.FindAsync(id);
            if (sessionConfig == null)
            {
                return NotFound();
            }

            _context.SessionConfigs.Remove(sessionConfig);
            await _context.SaveChangesAsync();

            return NoContent();
        }

        private bool SessionConfigExists(Guid id)
        {
            return _context.SessionConfigs.Any(e => e.Id == id);
        }
    }
}
