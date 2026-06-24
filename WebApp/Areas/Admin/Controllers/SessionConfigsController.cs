using App.DAL.EF;
using App.Domain;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace WebApp.Areas.Admin.Controllers
{
    public class SessionConfigsController : Controller
    {
        private readonly AppDbContext _context;

        public SessionConfigsController(AppDbContext context)
        {
            _context = context;
        }

        // GET: SessionConfigs
        public async Task<IActionResult> Index()
        {
            return View(await _context.SessionConfigs.ToListAsync());
        }

        // GET: SessionConfigs/Details/5
        public async Task<IActionResult> Details(Guid? id)
        {
            if (id == null)
            {
                return NotFound();
            }

            var sessionConfig = await _context.SessionConfigs
                .FirstOrDefaultAsync(m => m.Id == id);
            if (sessionConfig == null)
            {
                return NotFound();
            }

            return View(sessionConfig);
        }

        // GET: SessionConfigs/Create
        public IActionResult Create()
        {
            return View();
        }

        // POST: SessionConfigs/Create
        // To protect from overposting attacks, enable the specific properties you want to bind to.
        // For more details, see http://go.microsoft.com/fwlink/?LinkId=317598.
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create([Bind("Name,Description,PlannedDurationSeconds")] SessionConfig sessionConfig)
        {
            if (ModelState.IsValid)
            {
                var now = DateTime.UtcNow;
                sessionConfig.Id = Guid.NewGuid();
                sessionConfig.CreatedAt = now;
                sessionConfig.UpdatedAt = now;
                _context.Add(sessionConfig);
                await _context.SaveChangesAsync();
                return RedirectToAction(nameof(Index));
            }
            return View(sessionConfig);
        }

        // GET: SessionConfigs/Edit/5
        public async Task<IActionResult> Edit(Guid? id)
        {
            if (id == null)
            {
                return NotFound();
            }

            var sessionConfig = await _context.SessionConfigs.FindAsync(id);
            if (sessionConfig == null)
            {
                return NotFound();
            }
            return View(sessionConfig);
        }

        // POST: SessionConfigs/Edit/5
        // To protect from overposting attacks, enable the specific properties you want to bind to.
        // For more details, see http://go.microsoft.com/fwlink/?LinkId=317598.
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(Guid id, [Bind("Id,Name,Description,PlannedDurationSeconds")] SessionConfig sessionConfig)
        {
            if (id != sessionConfig.Id)
            {
                return NotFound();
            }

            if (ModelState.IsValid)
            {
                try
                {
                    var existing = await _context.SessionConfigs
                        .AsTracking()
                        .FirstOrDefaultAsync(x => x.Id == id);
                    if (existing == null)
                    {
                        return NotFound();
                    }

                    existing.Name = sessionConfig.Name;
                    existing.Description = sessionConfig.Description;
                    existing.PlannedDurationSeconds = sessionConfig.PlannedDurationSeconds;
                    existing.UpdatedAt = DateTime.UtcNow;
                    await _context.SaveChangesAsync();
                }
                catch (DbUpdateConcurrencyException)
                {
                    if (!SessionConfigExists(sessionConfig.Id))
                    {
                        return NotFound();
                    }
                    else
                    {
                        throw;
                    }
                }
                return RedirectToAction(nameof(Index));
            }
            return View(sessionConfig);
        }

        // GET: SessionConfigs/Delete/5
        public async Task<IActionResult> Delete(Guid? id)
        {
            if (id == null)
            {
                return NotFound();
            }

            var sessionConfig = await _context.SessionConfigs
                .FirstOrDefaultAsync(m => m.Id == id);
            if (sessionConfig == null)
            {
                return NotFound();
            }

            return View(sessionConfig);
        }

        // POST: SessionConfigs/Delete/5
        [HttpPost, ActionName("Delete")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteConfirmed(Guid id)
        {
            var sessionConfig = await _context.SessionConfigs.FindAsync(id);
            if (sessionConfig != null)
            {
                _context.SessionConfigs.Remove(sessionConfig);
            }

            await _context.SaveChangesAsync();
            return RedirectToAction(nameof(Index));
        }

        private bool SessionConfigExists(Guid id)
        {
            return _context.SessionConfigs.Any(e => e.Id == id);
        }
    }
}
