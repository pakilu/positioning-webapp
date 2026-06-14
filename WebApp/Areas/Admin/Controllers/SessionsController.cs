using App.DAL.EF;
using App.Domain;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;

namespace WebApp.Areas.Admin.Controllers
{
    public class SessionsController : Controller
    {
        private readonly AppDbContext _context;

        public SessionsController(AppDbContext context)
        {
            _context = context;
        }

        // GET: Sessions
        public async Task<IActionResult> Index()
        {
            var appDbContext = _context.Sessions.Include(s => s.SessionConfig);
            return View(await appDbContext.ToListAsync());
        }

        // GET: Sessions/Details/5
        public async Task<IActionResult> Details(Guid? id)
        {
            if (id == null)
            {
                return NotFound();
            }

            var session = await _context.Sessions
                .Include(s => s.SessionConfig)
                .FirstOrDefaultAsync(m => m.Id == id);
            if (session == null)
            {
                return NotFound();
            }

            return View(session);
        }

        // GET: Sessions/Create
        public IActionResult Create()
        {
            ViewData["SessionConfigId"] = new SelectList(_context.SessionConfigs, "Id", "Name");
            return View();
        }

        // POST: Sessions/Create
        // To protect from overposting attacks, enable the specific properties you want to bind to.
        // For more details, see http://go.microsoft.com/fwlink/?LinkId=317598.
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create([Bind("SessionConfigId,Name")] Session session)
        {
            ModelState.Remove(nameof(Session.SessionConfig));
            if (ModelState.IsValid)
            {
                var now = DateTime.UtcNow;
                session.Id = Guid.NewGuid();
                session.Status = ESessionStatus.Active;
                session.StartedAt = now;
                session.EndedAt = null;
                session.CreatedAt = now;
                session.UpdatedAt = now;
                _context.Add(session);
                await _context.SaveChangesAsync();
                return RedirectToAction(nameof(Live), new { id = session.Id });
            }
            ViewData["SessionConfigId"] = new SelectList(_context.SessionConfigs, "Id", "Name", session.SessionConfigId);
            return View(session);
        }

        // GET: Sessions/Live/5
        public async Task<IActionResult> Live(Guid? id)
        {
            if (id == null)
            {
                return NotFound();
            }

            var session = await _context.Sessions
                .Include(s => s.SessionConfig)
                    .ThenInclude(sc => sc.SessionConfigChips)
                        .ThenInclude(scc => scc.Chip)
                .FirstOrDefaultAsync(m => m.Id == id);
            if (session == null)
            {
                return NotFound();
            }

            return View(session);
        }

        // GET: Sessions/Edit/5
        public async Task<IActionResult> Edit(Guid? id)
        {
            if (id == null)
            {
                return NotFound();
            }

            var session = await _context.Sessions.FindAsync(id);
            if (session == null)
            {
                return NotFound();
            }
            ViewData["SessionConfigId"] = new SelectList(_context.SessionConfigs, "Id", "Name", session.SessionConfigId);
            return View(session);
        }

        // POST: Sessions/Edit/5
        // To protect from overposting attacks, enable the specific properties you want to bind to.
        // For more details, see http://go.microsoft.com/fwlink/?LinkId=317598.
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(Guid id, [Bind("Id,SessionConfigId,Name,Status,StartedAt,EndedAt,CreatedAt,UpdatedAt")] Session session)
        {
            if (id != session.Id)
            {
                return NotFound();
            }

            ModelState.Remove(nameof(Session.SessionConfig));
            if (ModelState.IsValid)
            {
                try
                {
                    _context.Update(session);
                    await _context.SaveChangesAsync();
                }
                catch (DbUpdateConcurrencyException)
                {
                    if (!SessionExists(session.Id))
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
            ViewData["SessionConfigId"] = new SelectList(_context.SessionConfigs, "Id", "Name", session.SessionConfigId);
            return View(session);
        }

        // GET: Sessions/Delete/5
        public async Task<IActionResult> Delete(Guid? id)
        {
            if (id == null)
            {
                return NotFound();
            }

            var session = await _context.Sessions
                .Include(s => s.SessionConfig)
                .FirstOrDefaultAsync(m => m.Id == id);
            if (session == null)
            {
                return NotFound();
            }

            return View(session);
        }

        // POST: Sessions/Delete/5
        [HttpPost, ActionName("Delete")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteConfirmed(Guid id)
        {
            var session = await _context.Sessions.FindAsync(id);
            if (session != null)
            {
                _context.Sessions.Remove(session);
            }

            await _context.SaveChangesAsync();
            return RedirectToAction(nameof(Index));
        }

        private bool SessionExists(Guid id)
        {
            return _context.Sessions.Any(e => e.Id == id);
        }
    }
}
