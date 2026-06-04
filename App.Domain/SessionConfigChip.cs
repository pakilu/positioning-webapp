namespace App.Domain;

public class SessionConfigChip
{
    public Guid Id { get; set; } = Guid.NewGuid();

    public Guid SessionConfigId { get; set; }

    public Guid ChipId { get; set; }

    public ChipRole Role { get; set; }

    // Required for anchors, null for tags
    public decimal? XCoord { get; set; }

    public decimal? YCoord { get; set; }

    public decimal? ZCoord { get; set; }

    public DateTime CreatedAt { get; set; }

    public DateTime UpdatedAt { get; set; }


    // Navigation properties
    public SessionConfig SessionConfig { get; set; } = default!;

    public Chip Chip { get; set; } = default!;
}