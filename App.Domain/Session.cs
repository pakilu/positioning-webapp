namespace App.Domain;

public class Session
{
    public Guid Id { get; set; } = Guid.NewGuid();

    public Guid SessionConfigId { get; set; }

    public string Name { get; set; } = default!;

    public SessionStatus Status { get; set; } = SessionStatus.Created;

    public DateTime? StartedAt { get; set; }

    public DateTime? EndedAt { get; set; }

    public DateTime CreatedAt { get; set; }

    public DateTime UpdatedAt { get; set; }


    // Navigation properties
    public SessionConfig SessionConfig { get; set; } = default!;

    public ICollection<PositionResult> PositionResults { get; set; } = new List<PositionResult>();

    public ICollection<RawMeasurement> RawMeasurements { get; set; } = new List<RawMeasurement>();
}