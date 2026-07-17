namespace WebApp.Models.Serial;

/// <summary>
/// Configuration for reading UWB measurements from a USB serial gateway.
/// Bound from the "Serial" section of appsettings.json.
/// </summary>
public class SerialOptions
{
    public const string SectionName = "Serial";

    /// <summary>If true, the webapp opens <see cref="PortName"/> and ingests JSON lines.</summary>
    public bool Enabled { get; set; } = false;

    /// <summary>Windows COM port exposed by the USB-connected ESP32 gateway.</summary>
    public string PortName { get; set; } = "COM3";

    /// <summary>Serial baud rate. Must match Serial.begin(...) in the firmware.</summary>
    public int BaudRate { get; set; } = 115200;

    /// <summary>Milliseconds to wait for a complete line before checking shutdown/reconnect state.</summary>
    public int ReadTimeoutMs { get; set; } = 1000;

    /// <summary>Delay between reconnect attempts if the serial port is unavailable.</summary>
    public int ReconnectDelaySeconds { get; set; } = 5;

    /// <summary>If true, incoming serial measurements are also persisted to the database.</summary>
    public bool PersistToDatabase { get; set; } = true;
}
