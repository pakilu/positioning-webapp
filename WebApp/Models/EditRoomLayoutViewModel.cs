using App.Domain;

namespace WebApp.Models;

/// <summary>
/// Backs the room-layout Edit view. Wraps the underlying <see cref="SessionConfig"/>
/// (bound directly by the POST action) and enriches it with the anchors the room
/// already has, so the live floor-plan preview can render those anchors as
/// dots — giving the user a visual reference for where the image needs to sit.
/// </summary>
public class EditRoomLayoutViewModel
{
    public SessionConfig Room { get; set; } = default!;

    /// <summary>Anchor points (X, Y in meters) to draw on the preview.</summary>
    public IReadOnlyList<PreviewAnchor> Anchors { get; set; } = Array.Empty<PreviewAnchor>();

    public class PreviewAnchor
    {
        public string Name { get; set; } = "";
        public double X { get; set; }
        public double Y { get; set; }
    }
}
