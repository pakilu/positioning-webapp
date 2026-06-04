namespace PositioningSystem.Domain;

public class RawMeasurement
{
    public Guid Id { get; set; } = Guid.NewGuid();

    public Guid SessionId { get; set; }

    public Guid TagChipId { get; set; }

    public Guid AnchorChipId { get; set; }

    public DateTime RecordedAt { get; set; }

    public decimal? Distance { get; set; }

    public decimal? Rssi { get; set; }

    public decimal? Snr { get; set; }

    public decimal? Quality { get; set; }

    public DateTime CreatedAt { get; set; }


    // Navigation properties
    public Session Session { get; set; } = default!;

    public Chip TagChip { get; set; } = default!;

    public Chip AnchorChip { get; set; } = default!;
}