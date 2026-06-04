namespace App.Domain;

public class SessionConfig
{
    public int Id { get; set; }

    public string Name { get; set; } = default!;

    public string? Description { get; set; }

    public int? PlannedDurationSeconds { get; set; }

    public DateTime CreatedAt { get; set; }

    public DateTime UpdatedAt { get; set; }


    // Navigation properties
    public ICollection<SessionConfigChip> SessionConfigChips { get; set; } = new List<SessionConfigChip>();

    public ICollection<Session> Sessions { get; set; } = new List<Session>();
}