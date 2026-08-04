namespace App.Domain;

public class SessionConfig
{
    public Guid Id { get; set; } = Guid.NewGuid();

    public string Name { get; set; } = default!;

    public string? Description { get; set; }

    public int? PlannedDurationSeconds { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

    // ----- Floor plan (optional) -----
    /// <summary>
    /// Web-relative path to a floor-plan raster/vector image, e.g. "/maps/ico_3.png".
    /// When null, the live view falls back to the plain coordinate grid.
    /// </summary>
    public string? FloorPlanImagePath { get; set; }

    /// <summary>World X coordinate (m) that corresponds to the LEFT edge of the image.</summary>
    public double? FloorPlanOriginXMeters { get; set; }

    /// <summary>World Y coordinate (m) that corresponds to the BOTTOM edge of the image.</summary>
    public double? FloorPlanOriginYMeters { get; set; }

    /// <summary>Real-world width (m) that the image spans horizontally.</summary>
    public double? FloorPlanWidthMeters { get; set; }

    /// <summary>Real-world height (m) that the image spans vertically.</summary>
    public double? FloorPlanHeightMeters { get; set; }

    /// <summary>Clockwise rotation of the plan about its center, in degrees. Default 0.</summary>
    public double? FloorPlanRotationDeg { get; set; }

    /// <summary>Rendering opacity 0..1 for the floor plan overlay. Default 0.7.</summary>
    public double? FloorPlanOpacity { get; set; }


    // Navigation properties
    public ICollection<SessionConfigChip> SessionConfigChips { get; set; } = new List<SessionConfigChip>();

    public ICollection<Session> Sessions { get; set; } = new List<Session>();
}