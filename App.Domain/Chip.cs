namespace App.Domain;

public class Chip
{
    public int Id { get; set; }

    public string Name { get; set; } = default!;

    public string DeviceIdentifier { get; set; } = default!;

    public string? Description { get; set; }

    public DateTime CreatedAt { get; set; }

    public DateTime UpdatedAt { get; set; }


    // Navigation properties
    public ICollection<SessionConfigChip> SessionConfigChips { get; set; } = new List<SessionConfigChip>();

    public ICollection<PositionResult> PositionResultsAsTag { get; set; } = new List<PositionResult>();

    public ICollection<RawMeasurement> RawMeasurementsAsTag { get; set; } = new List<RawMeasurement>();

    public ICollection<RawMeasurement> RawMeasurementsAsAnchor { get; set; } = new List<RawMeasurement>();
}
