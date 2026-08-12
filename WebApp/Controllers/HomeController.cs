using System.Diagnostics;
using App.DAL.EF;
using App.Domain;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using WebApp.Models;

namespace WebApp.Controllers;

public class HomeController : Controller
{
    private readonly AppDbContext _context;

    public HomeController(AppDbContext context)
    {
        _context = context;
    }

    public async Task<IActionResult> Index()
    {
        var hasChips = await _context.Chips.AnyAsync();
        var hasRooms = await _context.SessionConfigs.AnyAsync();
        var hasPlacedAnchors = await _context.SessionConfigChips
            .AnyAsync(c => c.Role == EChipRole.Anchor
                           && c.XCoord != null
                           && c.YCoord != null);
        var hasSessions = await _context.Sessions.AnyAsync();

        Session? latestActive = null;
        if (hasSessions)
        {
            latestActive = await _context.Sessions
                .Where(s => s.Status == ESessionStatus.Active)
                .Include(s => s.SessionConfig)
                .OrderByDescending(s => s.StartedAt ?? s.CreatedAt)
                .FirstOrDefaultAsync();
        }

        return View(new HomeIndexViewModel
        {
            HasChips = hasChips,
            HasRooms = hasRooms,
            HasPlacedAnchors = hasPlacedAnchors,
            HasSessions = hasSessions,
            LatestActiveSession = latestActive,
        });
    }

    public IActionResult Trial()
    {
        return View();
    }

    [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
    public IActionResult Error()
    {
        return View(new ErrorViewModel { RequestId = Activity.Current?.Id ?? HttpContext.TraceIdentifier });
    }
}
