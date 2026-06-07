using App.DAL.EF;
using App.Domain;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace WebApp.Areas.Admin.Controllers
{
    public class ChipsController : Controller
    {
        private readonly AppDbContext _context;

        public ChipsController(AppDbContext context)
        {
            _context = context;
        }

        // GET: Chips
        public async Task<IActionResult> Index()
        {
            return View(await _context.Chips.ToListAsync());
        }

        // GET: Chips/Details/5
        public async Task<IActionResult> Details(Guid? id)
        {
            if (id == null)
            {
                return NotFound();
            }

            var chip = await _context.Chips
                .FirstOrDefaultAsync(m => m.Id == id);
            if (chip == null)
            {
                return NotFound();
            }

            return View(chip);
        }

        // GET: Chips/Create
        public IActionResult Create()
        {
            return View();
        }

        // POST: Chips/Create
        // To protect from overposting attacks, enable the specific properties you want to bind to.
        // For more details, see http://go.microsoft.com/fwlink/?LinkId=317598.
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create([Bind("Id,Name,DeviceIdentifier,Description,CreatedAt,UpdatedAt")] Chip chip)
        {
            if (ModelState.IsValid)
            {
                chip.Id = Guid.NewGuid();
                _context.Add(chip);
                await _context.SaveChangesAsync();
                return RedirectToAction(nameof(Index));
            }
            return View(chip);
        }

        // GET: Chips/Edit/5
        public async Task<IActionResult> Edit(Guid? id)
        {
            if (id == null)
            {
                return NotFound();
            }

            var chip = await _context.Chips.FindAsync(id);
            if (chip == null)
            {
                return NotFound();
            }
            return View(chip);
        }

        // POST: Chips/Edit/5
        // To protect from overposting attacks, enable the specific properties you want to bind to.
        // For more details, see http://go.microsoft.com/fwlink/?LinkId=317598.
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(Guid id, [Bind("Id,Name,DeviceIdentifier,Description,CreatedAt,UpdatedAt")] Chip chip)
        {
            if (id != chip.Id)
            {
                return NotFound();
            }

            if (ModelState.IsValid)
            {
                try
                {
                    _context.Update(chip);
                    await _context.SaveChangesAsync();
                }
                catch (DbUpdateConcurrencyException)
                {
                    if (!ChipExists(chip.Id))
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
            return View(chip);
        }

        // GET: Chips/Delete/5
        public async Task<IActionResult> Delete(Guid? id)
        {
            if (id == null)
            {
                return NotFound();
            }

            var chip = await _context.Chips
                .FirstOrDefaultAsync(m => m.Id == id);
            if (chip == null)
            {
                return NotFound();
            }

            return View(chip);
        }

        // POST: Chips/Delete/5
        [HttpPost, ActionName("Delete")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteConfirmed(Guid id)
        {
            var chip = await _context.Chips.FindAsync(id);
            if (chip != null)
            {
                _context.Chips.Remove(chip);
            }

            await _context.SaveChangesAsync();
            return RedirectToAction(nameof(Index));
        }

        private bool ChipExists(Guid id)
        {
            return _context.Chips.Any(e => e.Id == id);
        }
    }
}
