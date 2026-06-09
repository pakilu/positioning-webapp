using App.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;
namespace App.DAL.EF;

public class AppDbContext : DbContext
{
    public DbSet<Chip> Chips { get; set; } = default!;
    public DbSet<SessionConfig> SessionConfigs { get; set; } = default!;
    public DbSet<SessionConfigChip> SessionConfigChips { get; set; } = default!;
    public DbSet<Session> Sessions { get; set; } = default!;
    public DbSet<PositionResult> PositionResults { get; set; } = default!;
    public DbSet<RawMeasurement> RawMeasurements { get; set; } = default!;

    public AppDbContext(DbContextOptions<AppDbContext> options) : base(options)
    {
    }

    protected override void OnModelCreating(ModelBuilder builder)
    {
        base.OnModelCreating(builder);

        // Configure all DateTime properties to use UTC with timestamp with time zone
        // with automatic conversion to ensure all values are UTC
        var dateTimeConverter = new ValueConverter<DateTime, DateTime>(
            v => v.Kind == DateTimeKind.Utc ? v : v.ToUniversalTime(),
            v => DateTime.SpecifyKind(v, DateTimeKind.Utc));

        var nullableDateTimeConverter = new ValueConverter<DateTime?, DateTime?>(
            v => v.HasValue ? (v.Value.Kind == DateTimeKind.Utc ? v : v.Value.ToUniversalTime()) : null,
            v => v.HasValue ? DateTime.SpecifyKind(v.Value, DateTimeKind.Utc) : null);

        foreach (var entityType in builder.Model.GetEntityTypes())
        {
            foreach (var property in entityType.GetProperties())
            {
                if (property.ClrType == typeof(DateTime))
                {
                    property.SetColumnType("timestamp with time zone");
                    property.SetValueConverter(dateTimeConverter);
                }
                else if (property.ClrType == typeof(DateTime?))
                {
                    property.SetColumnType("timestamp with time zone");
                    property.SetValueConverter(nullableDateTimeConverter);
                }
            }
        }

        builder.Entity<Chip>()
            .HasIndex(x => x.DeviceIdentifier)
            .IsUnique();

        builder.Entity<SessionConfigChip>()
            .HasIndex(x => new { x.SessionConfigId, x.ChipId })
            .IsUnique();

        builder.Entity<SessionConfigChip>()
            .HasOne(x => x.SessionConfig)
            .WithMany(x => x.SessionConfigChips)
            .HasForeignKey(x => x.SessionConfigId);

        builder.Entity<SessionConfigChip>()
            .HasOne(x => x.Chip)
            .WithMany(x => x.SessionConfigChips)
            .HasForeignKey(x => x.ChipId);

        builder.Entity<Session>()
            .HasOne(x => x.SessionConfig)
            .WithMany(x => x.Sessions)
            .HasForeignKey(x => x.SessionConfigId);

        builder.Entity<PositionResult>()
            .HasOne(x => x.Session)
            .WithMany(x => x.PositionResults)
            .HasForeignKey(x => x.SessionId);

        builder.Entity<PositionResult>()
            .HasOne(x => x.TagChip)
            .WithMany(x => x.PositionResultsAsTag)
            .HasForeignKey(x => x.TagChipId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.Entity<RawMeasurement>()
            .HasOne(x => x.Session)
            .WithMany(x => x.RawMeasurements)
            .HasForeignKey(x => x.SessionId);

        builder.Entity<RawMeasurement>()
            .HasOne(x => x.TagChip)
            .WithMany(x => x.RawMeasurementsAsTag)
            .HasForeignKey(x => x.TagChipId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.Entity<RawMeasurement>()
            .HasOne(x => x.AnchorChip)
            .WithMany(x => x.RawMeasurementsAsAnchor)
            .HasForeignKey(x => x.AnchorChipId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
