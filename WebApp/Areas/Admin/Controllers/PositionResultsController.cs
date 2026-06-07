using App.DAL.EF;
using App.Domain;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;

namespace WebApp.Areas.Admin.Controllers
{
    public class PositionResultsController : Controller
    {
        private readonly AppDbContext _context;

        public PositionResultsController(AppDbContext context)
        {
            _context = context;
        }

        // GET: PositionResults
        public async Task<IActionResult> Index()
        {
            var appDbContext = _context.PositionResults.Include(p => p.Session).Include(p => p.TagChip);
            return View(await appDbContext.ToListAsync());
        }

        // GET: PositionResults/Details/5
        public async Task<IActionResult> Details(Guid? id)
        {
            if (id == null)
            {
                return NotFound();
            }

            var positionResult = await _context.PositionResults
                .Include(p => p.Session)
                .Include(p => p.TagChip)
                .FirstOrDefaultAsync(m => m.Id == id);
            if (positionResult == null)
            {
                return NotFound();
            }

            return View(positionResult);
        }

        // GET: PositionResults/Create
        public IActionResult Create()
        {
            ViewData["SessionId"] = new SelectList(_context.Sessions, "Id", "Name");
            ViewData["TagChipId"] = new SelectList(_context.Chips, "Id", "DeviceIdentifier");
            return View();
        }

        // POST: PositionResults/Create
        // To protect from overposting attacks, enable the specific properties you want to bind to.
        // For more details, see http://go.microsoft.com/fwlink/?LinkId=317598.
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create([Bind("Id,SessionId,TagChipId,RecordedAt,XCoord,YCoord,ZCoord,Accuracy,CreatedAt")] PositionResult positionResult)
        {
            ModelState.Remove(nameof(PositionResult.Session));
            ModelState.Remove(nameof(PositionResult.TagChip));
            if (ModelState.IsValid)
            {
                positionResult.Id = Guid.NewGuid();
                _context.Add(positionResult);
                await _context.SaveChangesAsync();
                return RedirectToAction(nameof(Index));
            }
            ViewData["SessionId"] = new SelectList(_context.Sessions, "Id", "Name", positionResult.SessionId);
            ViewData["TagChipId"] = new SelectList(_context.Chips, "Id", "DeviceIdentifier", positionResult.TagChipId);
            return View(positionResult);
        }

        // GET: PositionResults/Edit/5
        public async Task<IActionResult> Edit(Guid? id)
        {
            if (id == null)
            {
                return NotFound();
            }

            var positionResult = await _context.PositionResults.FindAsync(id);
            if (positionResult == null)
            {
                return NotFound();
            }
            ViewData["SessionId"] = new SelectList(_context.Sessions, "Id", "Name", positionResult.SessionId);
            ViewData["TagChipId"] = new SelectList(_context.Chips, "Id", "DeviceIdentifier", positionResult.TagChipId);
            return View(positionResult);
        }

        // POST: PositionResults/Edit/5
        // To protect from overposting attacks, enable the specific properties you want to bind to.
        // For more details, see http://go.microsoft.com/fwlink/?LinkId=317598.
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(Guid id, [Bind("Id,SessionId,TagChipId,RecordedAt,XCoord,YCoord,ZCoord,Accuracy,CreatedAt")] PositionResult positionResult)
        {
            if (id != positionResult.Id)
            {
                return NotFound();
            }

            ModelState.Remove(nameof(PositionResult.Session));
            ModelState.Remove(nameof(PositionResult.TagChip));
            if (ModelState.IsValid)
            {
                try
                {
                    _context.Update(positionResult);
                    await _context.SaveChangesAsync();
                }
                catch (DbUpdateConcurrencyException)
                {
                    if (!PositionResultExists(positionResult.Id))
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
            ViewData["SessionId"] = new SelectList(_context.Sessions, "Id", "Name", positionResult.SessionId);
            ViewData["TagChipId"] = new SelectList(_context.Chips, "Id", "DeviceIdentifier", positionResult.TagChipId);
            return View(positionResult);
        }

        // GET: PositionResults/Delete/5
        public async Task<IActionResult> Delete(Guid? id)
        {
            if (id == null)
            {
                return NotFound();
            }

            var positionResult = await _context.PositionResults
                .Include(p => p.Session)
                .Include(p => p.TagChip)
                .FirstOrDefaultAsync(m => m.Id == id);
            if (positionResult == null)
            {
                return NotFound();
            }

            return View(positionResult);
        }

        // POST: PositionResults/Delete/5
        [HttpPost, ActionName("Delete")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteConfirmed(Guid id)
        {
            var positionResult = await _context.PositionResults.FindAsync(id);
            if (positionResult != null)
            {
                _context.PositionResults.Remove(positionResult);
            }

            await _context.SaveChangesAsync();
            return RedirectToAction(nameof(Index));
        }

        private bool PositionResultExists(Guid id)
        {
            return _context.PositionResults.Any(e => e.Id == id);
        }
    }
}
