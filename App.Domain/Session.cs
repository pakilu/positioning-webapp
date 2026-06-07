namespace App.Domain;

public class Session
{
    public Guid Id { get; set; } = Guid.NewGuid();

    public Guid SessionConfigId { get; set; }

    public string Name { get; set; } = default!;

    public ESessionStatus Status { get; set; } = ESessionStatus.Created;

    public DateTime? StartedAt { get; set; }

    public DateTime? EndedAt { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;


    // Navigation properties
    public SessionConfig SessionConfig { get; set; } = default!;

    public ICollection<PositionResult> PositionResults { get; set; } = new List<PositionResult>();

    public ICollection<RawMeasurement> RawMeasurements { get; set; } = new List<RawMeasurement>();
}