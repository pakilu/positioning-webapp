namespace WebApp.Models.Mqtt;

/// <summary>
/// JSON payload expected on the raw-measurement MQTT topic.
/// Chip identifiers are matched against <c>Chip.DeviceIdentifier</c>.
/// </summary>
public class RawMeasurementMessage
{
    public Guid? SessionId { get; set; }
    public string? TagDeviceId { get; set; }
    public string? AnchorDeviceId { get; set; }
    public DateTime? RecordedAt { get; set; }
    public decimal? Distance { get; set; }
    public decimal? Rssi { get; set; }
    public decimal? Snr { get; set; }
    public decimal? Quality { get; set; }
}
