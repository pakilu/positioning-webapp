namespace App.Domain;

public class PositionResult
{
    public int Id { get; set; }

    public int SessionId { get; set; }

    public int TagChipId { get; set; }

    public DateTime RecordedAt { get; set; }

    public decimal XCoord { get; set; }

    public decimal YCoord { get; set; }

    public decimal? ZCoord { get; set; }

    public decimal? Accuracy { get; set; }

    public DateTime CreatedAt { get; set; }


    // Navigation properties
    public Session Session { get; set; } = default!;

    public Chip TagChip { get; set; } = default!;
}