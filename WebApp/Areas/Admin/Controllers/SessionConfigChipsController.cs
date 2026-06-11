using App.DAL.EF;
using App.Domain;
using App.BLL.Positioning;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;

namespace WebApp.Areas.Admin.Controllers
{
    public class SessionConfigChipsController : Controller
    {
        private readonly AppDbContext _context;
        private readonly IAnchorPositionProvider _anchorPositionProvider;

        public SessionConfigChipsController(AppDbContext context, IAnchorPositionProvider anchorPositionProvider)
        {
            _context = context;
            _anchorPositionProvider = anchorPositionProvider;
        }

        // GET: SessionConfigChips
        public async Task<IActionResult> Index()
        {
            var appDbContext = _context.SessionConfigChips
                .Include(s => s.Chip)
                .Include(s => s.SessionConfig)
                .OrderBy(s => s.SessionConfig.Name)
                .ThenBy(s => s.Role)
                .ThenBy(s => s.Chip.Name);
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
            PopulateSelectLists();
            return View();
        }

        // POST: SessionConfigChips/Create
        // To protect from overposting attacks, enable the specific properties you want to bind to.
        // For more details, see http://go.microsoft.com/fwlink/?LinkId=317598.
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create([Bind("Id,SessionConfigId,ChipId,Role,XCoord,YCoord,ZCoord,CreatedAt,UpdatedAt")] SessionConfigChip sessionConfigChip)
        {
            ModelState.Remove(nameof(SessionConfigChip.SessionConfig));
            ModelState.Remove(nameof(SessionConfigChip.Chip));
            ValidateRoleCoordinates(sessionConfigChip);
            if (ModelState.IsValid)
            {
                sessionConfigChip.Id = Guid.NewGuid();
                sessionConfigChip.CreatedAt = DateTime.UtcNow;
                sessionConfigChip.UpdatedAt = DateTime.UtcNow;
                if (sessionConfigChip.Role == EChipRole.Tag)
                {
                    sessionConfigChip.XCoord = null;
                    sessionConfigChip.YCoord = null;
                    sessionConfigChip.ZCoord = null;
                }

                _context.Add(sessionConfigChip);
                await _context.SaveChangesAsync();
                await InvalidateSessionsUsingConfig(sessionConfigChip.SessionConfigId);
                return RedirectToAction(nameof(Index));
            }
            PopulateSelectLists(sessionConfigChip.SessionConfigId, sessionConfigChip.ChipId);
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
            PopulateSelectLists(sessionConfigChip.SessionConfigId, sessionConfigChip.ChipId);
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

            ModelState.Remove(nameof(SessionConfigChip.SessionConfig));
            ModelState.Remove(nameof(SessionConfigChip.Chip));
            ValidateRoleCoordinates(sessionConfigChip);
            if (ModelState.IsValid)
            {
                try
                {
                    var existing = await _context.SessionConfigChips.FindAsync(id);
                    if (existing == null)
                    {
                        return NotFound();
                    }

                    var oldSessionConfigId = existing.SessionConfigId;
                    existing.SessionConfigId = sessionConfigChip.SessionConfigId;
                    existing.ChipId = sessionConfigChip.ChipId;
                    existing.Role = sessionConfigChip.Role;
                    existing.XCoord = sessionConfigChip.Role == EChipRole.Anchor ? sessionConfigChip.XCoord : null;
                    existing.YCoord = sessionConfigChip.Role == EChipRole.Anchor ? sessionConfigChip.YCoord : null;
                    existing.ZCoord = sessionConfigChip.Role == EChipRole.Anchor ? sessionConfigChip.ZCoord : null;
                    existing.UpdatedAt = DateTime.UtcNow;
                    await _context.SaveChangesAsync();
                    if (oldSessionConfigId != existing.SessionConfigId)
                    {
                        await InvalidateSessionsUsingConfig(oldSessionConfigId);
                    }
                    await InvalidateSessionsUsingConfig(existing.SessionConfigId);
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
            PopulateSelectLists(sessionConfigChip.SessionConfigId, sessionConfigChip.ChipId);
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

        private void PopulateSelectLists(Guid? selectedConfigId = null, Guid? selectedChipId = null)
        {
            var chips = _context.Chips
                .OrderBy(c => c.Name)
                .Select(c => new
                {
                    c.Id,
                    Label = c.Name + " (" + c.DeviceIdentifier + ")"
                })
                .ToList();

            ViewData["ChipId"] = new SelectList(chips, "Id", "Label", selectedChipId);
            ViewData["SessionConfigId"] = new SelectList(
                _context.SessionConfigs.OrderBy(c => c.Name),
                "Id",
                "Name",
                selectedConfigId);
        }

        private void ValidateRoleCoordinates(SessionConfigChip sessionConfigChip)
        {
            if (sessionConfigChip.Role == EChipRole.Anchor)
            {
                if (sessionConfigChip.XCoord == null)
                {
                    ModelState.AddModelError(nameof(SessionConfigChip.XCoord), "Anchors need an X coordinate.");
                }
                if (sessionConfigChip.YCoord == null)
                {
                    ModelState.AddModelError(nameof(SessionConfigChip.YCoord), "Anchors need a Y coordinate.");
                }
            }
            else if (sessionConfigChip.Role == EChipRole.Tag
                     && (sessionConfigChip.XCoord != null || sessionConfigChip.YCoord != null || sessionConfigChip.ZCoord != null))
            {
                ModelState.AddModelError(nameof(SessionConfigChip.Role), "Tags are positioned live, so leave fixed coordinates empty.");
            }
        }

        private async Task InvalidateSessionsUsingConfig(Guid sessionConfigId)
        {
            var sessionIds = await _context.Sessions
                .Where(s => s.SessionConfigId == sessionConfigId)
                .Select(s => s.Id)
                .ToListAsync();

            foreach (var sessionId in sessionIds)
            {
                _anchorPositionProvider.Invalidate(sessionId);
            }
        }
    }
}
