using App.DAL.EF;
using App.Domain;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;

namespace WebApp.Areas.Admin.Controllers
{
    public class SessionConfigChipsController : Controller
    {
        private readonly AppDbContext _context;

        public SessionConfigChipsController(AppDbContext context)
        {
            _context = context;
        }

        // GET: SessionConfigChips
        public async Task<IActionResult> Index()
        {
            var appDbContext = _context.SessionConfigChips.Include(s => s.Chip).Include(s => s.SessionConfig);
            return View(await appDbContext.ToListAsync());
        }

        // GET: SessionConfigChips/Details/5
        public async Task<IActionResult> Details(Guid? id)
        {
            if (id == null)
            {
                return NotFound();
            }

            var sessionConfigChip = await _context.SessionConfigChips
                .Include(s => s.Chip)
                .Include(s => s.SessionConfig)
                .FirstOrDefaultAsync(m => m.Id == id);
            if (sessionConfigChip == null)
            {
                return NotFound();
            }

            return View(sessionConfigChip);
        }

        // GET: SessionConfigChips/Create
        public IActionResult Create()
        {
            ViewData["ChipId"] = new SelectList(_context.Chips, "Id", "DeviceIdentifier");
            ViewData["SessionConfigId"] = new SelectList(_context.SessionConfigs, "Id", "Name");
            return View();
        }

        // POST: SessionConfigChips/Create
        // To protect from overposting attacks, enable the specific properties you want to bind to.
        // For more details, see http://go.microsoft.com/fwlink/?LinkId=317598.
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create([Bind("Id,SessionConfigId,ChipId,Role,XCoord,YCoord,ZCoord,CreatedAt,UpdatedAt")] SessionConfigChip sessionConfigChip)
        {
            if (ModelState.IsValid)
            {
                sessionConfigChip.Id = Guid.NewGuid();
                _context.Add(sessionConfigChip);
                await _context.SaveChangesAsync();
                return RedirectToAction(nameof(Index));
            }
            ViewData["ChipId"] = new SelectList(_context.Chips, "Id", "DeviceIdentifier", sessionConfigChip.ChipId);
            ViewData["SessionConfigId"] = new SelectList(_context.SessionConfigs, "Id", "Name", sessionConfigChip.SessionConfigId);
            return View(sessionConfigChip);
        }

        // GET: SessionConfigChips/Edit/5
        public async Task<IActionResult> Edit(Guid? id)
        {
            if (id == null)
            {
                return NotFound();
            }

            var sessionConfigChip = await _context.SessionConfigChips.FindAsync(id);
            if (sessionConfigChip == null)
            {
                return NotFound();
            }
            ViewData["ChipId"] = new SelectList(_context.Chips, "Id", "DeviceIdentifier", sessionConfigChip.ChipId);
            ViewData["SessionConfigId"] = new SelectList(_context.SessionConfigs, "Id", "Name", sessionConfigChip.SessionConfigId);
            return View(sessionConfigChip);
        }

        // POST: SessionConfigChips/Edit/5
        // To protect from overposting attacks, enable the specific properties you want to bind to.
        // For more details, see http://go.microsoft.com/fwlink/?LinkId=317598.
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(Guid id, [Bind("Id,SessionConfigId,ChipId,Role,XCoord,YCoord,ZCoord,CreatedAt,UpdatedAt")] SessionConfigChip sessionConfigChip)
        {
            if (id != sessionConfigChip.Id)
            {
                return NotFound();
            }

            if (ModelState.IsValid)
            {
                try
                {
                    _context.Update(sessionConfigChip);
                    await _context.SaveChangesAsync();
                }
                catch (DbUpdateConcurrencyException)
                {
                    if (!SessionConfigChipExists(sessionConfigChip.Id))
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
            ViewData["ChipId"] = new SelectList(_context.Chips, "Id", "DeviceIdentifier", sessionConfigChip.ChipId);
            ViewData["SessionConfigId"] = new SelectList(_context.SessionConfigs, "Id", "Name", sessionConfigChip.SessionConfigId);
            return View(sessionConfigChip);
        }

        // GET: SessionConfigChips/Delete/5
        public async Task<IActionResult> Delete(Guid? id)
        {
            if (id == null)
            {
                return NotFound();
            }

            var sessionConfigChip = await _context.SessionConfigChips
                .Include(s => s.Chip)
                .Include(s => s.SessionConfig)
                .FirstOrDefaultAsync(m => m.Id == id);
            if (sessionConfigChip == null)
            {
                return NotFound();
            }

            return View(sessionConfigChip);
        }

        // POST: SessionConfigChips/Delete/5
        [HttpPost, ActionName("Delete")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteConfirmed(Guid id)
        {
            var sessionConfigChip = await _context.SessionConfigChips.FindAsync(id);
            if (sessionConfigChip != null)
            {
                _context.SessionConfigChips.Remove(sessionConfigChip);
            }

            await _context.SaveChangesAsync();
            return RedirectToAction(nameof(Index));
        }

        private bool SessionConfigChipExists(Guid id)
        {
            return _context.SessionConfigChips.Any(e => e.Id == id);
        }
    }
}
