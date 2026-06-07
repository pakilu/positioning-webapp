namespace App.Domain;

public class SessionConfig
{
    public Guid Id { get; set; } = Guid.NewGuid();

    public string Name { get; set; } = default!;

    public string? Description { get; set; }

    public int? PlannedDurationSeconds { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;


    // Navigation properties
    public ICollection<SessionConfigChip> SessionConfigChips { get; set; } = new List<SessionConfigChip>();

    public ICollection<Session> Sessions { get; set; } = new List<Session>();
}