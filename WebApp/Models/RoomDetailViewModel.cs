using App.Domain;

namespace WebApp.Models;

/// <summary>
/// Backs the room-detail cockpit view. The room itself carries floor plan
/// and placement data; <see cref="Plane"/> is the shared map partial's
/// view model; <see cref="AvailableChips"/> feeds the "add existing chip"
/// mode of the add-anchor/tag dialogs.
/// </summary>
public class RoomDetailViewModel
{
    public SessionConfig Room { get; set; } = default!;
    public PositionPlaneViewModel Plane { get; set; } = default!;

    public IReadOnlyList<SessionConfigChip> Anchors { get; set; } = Array.Empty<SessionConfigChip>();
    public IReadOnlyList<SessionConfigChip> Tags { get; set; } = Array.Empty<SessionConfigChip>();
    public IReadOnlyList<Session> Sessions { get; set; } = Array.Empty<Session>();

    /// <summary>Chips that are not currently a member of this room.</summary>
    public IReadOnlyList<Chip> AvailableChips { get; set; } = Array.Empty<Chip>();
}
