using App.Domain;

namespace WebApp.Models;

/// <summary>
/// Backs <c>Views/Home/Index.cshtml</c>. The four booleans drive the
/// shrinking getting-started wizard; <see cref="LatestActiveSession"/>
/// (if any) is surfaced once the wizard is complete.
/// </summary>
public class HomeIndexViewModel
{
    public bool HasChips { get; set; }
    public bool HasRooms { get; set; }
    public bool HasPlacedAnchors { get; set; }
    public bool HasSessions { get; set; }

    public bool IsFullyConfigured =>
        HasChips && HasRooms && HasPlacedAnchors && HasSessions;

    public Session? LatestActiveSession { get; set; }
}
