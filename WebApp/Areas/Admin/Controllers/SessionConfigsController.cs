using App.DAL.EF;
using App.Domain;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace WebApp.Areas.Admin.Controllers
{
    public class SessionConfigsController : Controller
    {
        private const string FloorPlanUploadFolder = "maps";
        private static readonly string[] AllowedFloorPlanExtensions =
            { ".png", ".jpg", ".jpeg", ".svg", ".webp", ".gif" };

        private readonly AppDbContext _context;
        private readonly IWebHostEnvironment _env;

        public SessionConfigsController(AppDbContext context, IWebHostEnvironment env)
        {
            _context = context;
            _env = env;
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
        public async Task<IActionResult> Edit(
            Guid id,
            [Bind("Id,Name,Description,PlannedDurationSeconds,FloorPlanImagePath,FloorPlanOriginXMeters,FloorPlanOriginYMeters,FloorPlanWidthMeters,FloorPlanHeightMeters,FloorPlanScale,FloorPlanRotationDeg,FloorPlanOpacity")] SessionConfig sessionConfig,
            IFormFile? floorPlanFile,
            bool removeFloorPlan = false)
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

                    // Floor plan handling ---------------------------------
                    if (removeFloorPlan)
                    {
                        TryDeleteFloorPlanFile(existing.FloorPlanImagePath);
                        existing.FloorPlanImagePath = null;
                        existing.FloorPlanOriginXMeters = null;
                        existing.FloorPlanOriginYMeters = null;
                        existing.FloorPlanWidthMeters = null;
                        existing.FloorPlanHeightMeters = null;
                        existing.FloorPlanScale = null;
                        existing.FloorPlanRotationDeg = null;
                        existing.FloorPlanOpacity = null;
                    }
                    else
                    {
                        if (floorPlanFile is { Length: > 0 })
                        {
                            var savedPath = await SaveFloorPlanFileAsync(floorPlanFile, existing.Id);
                            if (savedPath == null)
                            {
                                ModelState.AddModelError(nameof(floorPlanFile),
                                    "Unsupported floor plan file type. Allowed: " +
                                    string.Join(", ", AllowedFloorPlanExtensions));
                                return View(sessionConfig);
                            }
                            TryDeleteFloorPlanFile(existing.FloorPlanImagePath);
                            existing.FloorPlanImagePath = savedPath;
                        }
                        else if (!string.IsNullOrWhiteSpace(sessionConfig.FloorPlanImagePath))
                        {
                            existing.FloorPlanImagePath = sessionConfig.FloorPlanImagePath;
                        }

                        existing.FloorPlanOriginXMeters = sessionConfig.FloorPlanOriginXMeters;
                        existing.FloorPlanOriginYMeters = sessionConfig.FloorPlanOriginYMeters;
                        existing.FloorPlanWidthMeters   = sessionConfig.FloorPlanWidthMeters;
                        existing.FloorPlanHeightMeters  = sessionConfig.FloorPlanHeightMeters;
                        existing.FloorPlanScale         = sessionConfig.FloorPlanScale;
                        existing.FloorPlanRotationDeg   = sessionConfig.FloorPlanRotationDeg;
                        existing.FloorPlanOpacity       = sessionConfig.FloorPlanOpacity;
                    }

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

        /// <summary>
        /// Saves an uploaded floor plan image under wwwroot/maps/ and returns the
        /// web-relative path (e.g. "/maps/&lt;id&gt;-floorplan.png"), or null if the
        /// file extension is not allowed.
        /// </summary>
        private async Task<string?> SaveFloorPlanFileAsync(IFormFile file, Guid sessionConfigId)
        {
            var ext = Path.GetExtension(file.FileName).ToLowerInvariant();
            if (string.IsNullOrEmpty(ext) || Array.IndexOf(AllowedFloorPlanExtensions, ext) < 0)
            {
                return null;
            }

            var webRoot = _env.WebRootPath ?? Path.Combine(Directory.GetCurrentDirectory(), "wwwroot");
            var folder = Path.Combine(webRoot, FloorPlanUploadFolder);
            Directory.CreateDirectory(folder);

            var fileName = $"{sessionConfigId}-floorplan{ext}";
            var absolutePath = Path.Combine(folder, fileName);

            await using (var stream = System.IO.File.Create(absolutePath))
            {
                await file.CopyToAsync(stream);
            }

            return $"/{FloorPlanUploadFolder}/{fileName}";
        }

        private void TryDeleteFloorPlanFile(string? webPath)
        {
            if (string.IsNullOrWhiteSpace(webPath) || !webPath.StartsWith('/'))
            {
                return;
            }
            // Only remove files we clearly own (uploaded via this controller):
            // filename must look like "<guid>-floorplan.<ext>".
            var fileName = Path.GetFileName(webPath);
            if (!fileName.Contains("-floorplan", StringComparison.OrdinalIgnoreCase))
            {
                return;
            }
            var webRoot = _env.WebRootPath ?? Path.Combine(Directory.GetCurrentDirectory(), "wwwroot");
            var absolute = Path.Combine(webRoot, webPath.TrimStart('/').Replace('/', Path.DirectorySeparatorChar));
            try
            {
                if (System.IO.File.Exists(absolute)) System.IO.File.Delete(absolute);
            }
            catch
            {
                // best-effort cleanup only
            }
        }
    }
}
