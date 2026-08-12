namespace WebApp.Models;

/// <summary>
/// View model for the shared <c>_PositionPlane.cshtml</c> partial. Rendered
/// on both the live session view and the room-detail placement cockpit.
/// </summary>
public class PositionPlaneViewModel
{
    public IReadOnlyList<PositionPlaneAnchor> Anchors { get; set; } = Array.Empty<PositionPlaneAnchor>();
    public IReadOnlyList<PositionPlaneTag> Tags { get; set; } = Array.Empty<PositionPlaneTag>();
    public PositionPlaneFloorPlan? FloorPlan { get; set; }

    /// <summary>"live" or "layout". Controls which auxiliary UI the calling
    /// view will attach around the shared plane.</summary>
    public string Mode { get; set; } = "layout";

    /// <summary>DOM id for the plane root, so callers can host multiple planes
    /// or reference the mount point from their own scripts.</summary>
    public string DomId { get; set; } = "position-plane";
}

public class PositionPlaneAnchor
{
    public Guid Id { get; set; }
    public Guid ChipId { get; set; }
    public string Name { get; set; } = "";
    public double X { get; set; }
    public double Y { get; set; }
    public double Z { get; set; }
}

public class PositionPlaneTag
{
    public Guid ChipId { get; set; }
    public string? DeviceIdentifier { get; set; }
    public string Name { get; set; } = "";
}

public class PositionPlaneFloorPlan
{
    public string Url { get; set; } = "";
    public double Ox { get; set; }
    public double Oy { get; set; }
    public double W { get; set; }
    public double H { get; set; }
    public double Scale { get; set; } = 1.0;
    public double Rot { get; set; }
    public double Opacity { get; set; } = 0.7;
}
