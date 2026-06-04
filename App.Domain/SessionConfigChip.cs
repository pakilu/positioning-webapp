namespace App.Domain;

public class SessionConfigChip
{
    public int Id { get; set; }

    public int SessionConfigId { get; set; }

    public int ChipId { get; set; }

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