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
                session.Id = Guid.NewGuid();
                StartSession(session);
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
        public async Task<IActionResult> Edit(Guid id, [Bind("Id,SessionConfigId,Name")] Session session)
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
                    var existing = await GetTrackedSessionAsync(id);
                    if (existing == null)
                    {
                        return NotFound();
                    }

                    existing.SessionConfigId = session.SessionConfigId;
                    existing.Name = session.Name;
                    existing.UpdatedAt = DateTime.UtcNow;
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

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Activate(Guid id)
        {
            var session = await GetTrackedSessionAsync(id);
            if (session == null)
            {
                return NotFound();
            }

            StartSession(session);
            await _context.SaveChangesAsync();
            return RedirectToAction(nameof(Live), new { id });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Finish(Guid id)
        {
            var session = await GetTrackedSessionAsync(id);
            if (session == null)
            {
                return NotFound();
            }

            EndSession(session, ESessionStatus.Finished);
            await _context.SaveChangesAsync();
            return RedirectToAction(nameof(Live), new { id });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Cancel(Guid id)
        {
            var session = await GetTrackedSessionAsync(id);
            if (session == null)
            {
                return NotFound();
            }

            EndSession(session, ESessionStatus.Cancelled);
            await _context.SaveChangesAsync();
            return RedirectToAction(nameof(Live), new { id });
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

        private Task<Session?> GetTrackedSessionAsync(Guid id)
        {
            return _context.Sessions
                .AsTracking()
                .FirstOrDefaultAsync(s => s.Id == id);
        }

        private static void StartSession(Session session)
        {
            var now = DateTime.UtcNow;
            session.Status = ESessionStatus.Active;
            session.StartedAt ??= now;
            session.EndedAt = null;
            if (session.CreatedAt == default)
            {
                session.CreatedAt = now;
            }
            session.UpdatedAt = now;
        }

        private static void EndSession(Session session, ESessionStatus status)
        {
            var now = DateTime.UtcNow;
            session.Status = status;
            session.StartedAt ??= now;
            session.EndedAt = now;
            session.UpdatedAt = now;
        }
    }
}
