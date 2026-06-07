using App.DAL.EF;
using App.Domain;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;

namespace WebApp.Areas.Admin.Controllers
{
    public class RawMeasurementsController : Controller
    {
        private readonly AppDbContext _context;

        public RawMeasurementsController(AppDbContext context)
        {
            _context = context;
        }

        // GET: RawMeasurements
        public async Task<IActionResult> Index()
        {
            var appDbContext = _context.RawMeasurements.Include(r => r.AnchorChip).Include(r => r.Session).Include(r => r.TagChip);
            return View(await appDbContext.ToListAsync());
        }

        // GET: RawMeasurements/Details/5
        public async Task<IActionResult> Details(Guid? id)
        {
            if (id == null)
            {
                return NotFound();
            }

            var rawMeasurement = await _context.RawMeasurements
                .Include(r => r.AnchorChip)
                .Include(r => r.Session)
                .Include(r => r.TagChip)
                .FirstOrDefaultAsync(m => m.Id == id);
            if (rawMeasurement == null)
            {
                return NotFound();
            }

            return View(rawMeasurement);
        }

        // GET: RawMeasurements/Create
        public IActionResult Create()
        {
            ViewData["AnchorChipId"] = new SelectList(_context.Chips, "Id", "DeviceIdentifier");
            ViewData["SessionId"] = new SelectList(_context.Sessions, "Id", "Name");
            ViewData["TagChipId"] = new SelectList(_context.Chips, "Id", "DeviceIdentifier");
            return View();
        }

        // POST: RawMeasurements/Create
        // To protect from overposting attacks, enable the specific properties you want to bind to.
        // For more details, see http://go.microsoft.com/fwlink/?LinkId=317598.
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create([Bind("Id,SessionId,TagChipId,AnchorChipId,RecordedAt,Distance,Rssi,Snr,Quality,CreatedAt")] RawMeasurement rawMeasurement)
        {
            if (ModelState.IsValid)
            {
                rawMeasurement.Id = Guid.NewGuid();
                _context.Add(rawMeasurement);
                await _context.SaveChangesAsync();
                return RedirectToAction(nameof(Index));
            }
            ViewData["AnchorChipId"] = new SelectList(_context.Chips, "Id", "DeviceIdentifier", rawMeasurement.AnchorChipId);
            ViewData["SessionId"] = new SelectList(_context.Sessions, "Id", "Name", rawMeasurement.SessionId);
            ViewData["TagChipId"] = new SelectList(_context.Chips, "Id", "DeviceIdentifier", rawMeasurement.TagChipId);
            return View(rawMeasurement);
        }

        // GET: RawMeasurements/Edit/5
        public async Task<IActionResult> Edit(Guid? id)
        {
            if (id == null)
            {
                return NotFound();
            }

            var rawMeasurement = await _context.RawMeasurements.FindAsync(id);
            if (rawMeasurement == null)
            {
                return NotFound();
            }
            ViewData["AnchorChipId"] = new SelectList(_context.Chips, "Id", "DeviceIdentifier", rawMeasurement.AnchorChipId);
            ViewData["SessionId"] = new SelectList(_context.Sessions, "Id", "Name", rawMeasurement.SessionId);
            ViewData["TagChipId"] = new SelectList(_context.Chips, "Id", "DeviceIdentifier", rawMeasurement.TagChipId);
            return View(rawMeasurement);
        }

        // POST: RawMeasurements/Edit/5
        // To protect from overposting attacks, enable the specific properties you want to bind to.
        // For more details, see http://go.microsoft.com/fwlink/?LinkId=317598.
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(Guid id, [Bind("Id,SessionId,TagChipId,AnchorChipId,RecordedAt,Distance,Rssi,Snr,Quality,CreatedAt")] RawMeasurement rawMeasurement)
        {
            if (id != rawMeasurement.Id)
            {
                return NotFound();
            }

            if (ModelState.IsValid)
            {
                try
                {
                    _context.Update(rawMeasurement);
                    await _context.SaveChangesAsync();
                }
                catch (DbUpdateConcurrencyException)
                {
                    if (!RawMeasurementExists(rawMeasurement.Id))
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
            ViewData["AnchorChipId"] = new SelectList(_context.Chips, "Id", "DeviceIdentifier", rawMeasurement.AnchorChipId);
            ViewData["SessionId"] = new SelectList(_context.Sessions, "Id", "Name", rawMeasurement.SessionId);
            ViewData["TagChipId"] = new SelectList(_context.Chips, "Id", "DeviceIdentifier", rawMeasurement.TagChipId);
            return View(rawMeasurement);
        }

        // GET: RawMeasurements/Delete/5
        public async Task<IActionResult> Delete(Guid? id)
        {
            if (id == null)
            {
                return NotFound();
            }

            var rawMeasurement = await _context.RawMeasurements
                .Include(r => r.AnchorChip)
                .Include(r => r.Session)
                .Include(r => r.TagChip)
                .FirstOrDefaultAsync(m => m.Id == id);
            if (rawMeasurement == null)
            {
                return NotFound();
            }

            return View(rawMeasurement);
        }

        // POST: RawMeasurements/Delete/5
        [HttpPost, ActionName("Delete")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteConfirmed(Guid id)
        {
            var rawMeasurement = await _context.RawMeasurements.FindAsync(id);
            if (rawMeasurement != null)
            {
                _context.RawMeasurements.Remove(rawMeasurement);
            }

            await _context.SaveChangesAsync();
            return RedirectToAction(nameof(Index));
        }

        private bool RawMeasurementExists(Guid id)
        {
            return _context.RawMeasurements.Any(e => e.Id == id);
        }
    }
}
