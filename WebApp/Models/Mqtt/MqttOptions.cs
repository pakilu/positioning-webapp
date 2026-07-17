namespace WebApp.Models.Mqtt;

/// <summary>
/// Configuration for the MQTT ingest background service.
/// Bound from the "Mqtt" section of appsettings.json.
/// </summary>
public class MqttOptions
{
    public const string SectionName = "Mqtt";

    /// <summary>If true, the webapp connects to the configured MQTT broker.</summary>
    public bool Enabled { get; set; } = true;

    /// <summary>Mosquitto broker host (e.g. "localhost").</summary>
    public string Host { get; set; } = "localhost";

    /// <summary>Mosquitto broker TCP port (default 1883, or 8883 with TLS).</summary>
    public int Port { get; set; } = 1883;

    /// <summary>Client identifier used when connecting to the broker.</summary>
    public string ClientId { get; set; } = "positioning-webapp";

    public string? Username { get; set; }
    public string? Password { get; set; }
    public bool UseTls { get; set; } = false;

    /// <summary>
    /// Topic for raw distance/RSSI measurements coming from tags/anchors.
    /// Payload must be JSON matching <see cref="RawMeasurementMessage"/>.
    /// MQTT wildcards (+, #) are allowed.
    /// </summary>
    public string RawMeasurementTopic { get; set; } = "uwb/+/measurement";

    /// <summary>
    /// Topic chips publish their MAC address to on boot so the server can
    /// auto-register them. Payload is the MAC as a plain UTF-8 string
    /// (JSON <c>{"mac":"..."}</c> is also accepted).
    /// </summary>
    public string ChipRegistrationTopic { get; set; } = "uwb/chips/registration";

    /// <summary>If true, incoming messages are also persisted to the database.</summary>
    public bool PersistToDatabase { get; set; } = true;

    /// <summary>
    /// Retained topic template the webapp publishes to so each tag learns which
    /// anchors it should range against. <c>{tagDeviceId}</c> is substituted
    /// with the tag's <see cref="App.Domain.Chip.DeviceIdentifier"/>
    /// (e.g. <c>0x01</c>). Payload is a JSON array of anchor DeviceIdentifier
    /// hex strings, e.g. <c>["0x02","0x03","0x04"]</c>. Publishing an empty
    /// array tells the tag to stop ranging.
    /// </summary>
    public string AnchorListTopicTemplate { get; set; } = "uwb/tags/{tagDeviceId}/anchors";
}
