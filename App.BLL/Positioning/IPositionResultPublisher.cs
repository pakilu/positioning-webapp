namespace App.BLL.Positioning;

/// <summary>
/// Payload emitted by the positioning pipeline whenever a fresh fix is produced.
/// </summary>
public sealed record PositionResultBroadcast(
    Guid SessionId,
    Guid TagId,
    DateTime RecordedAt,
    double X,
    double Y,
    double Z,
    double Accuracy,
    int AnchorCount);

/// <summary>
/// Sink for computed position results. The pipeline pushes each fix here;
/// the concrete implementation decides how to deliver it (SignalR, MQTT,
/// logging, etc.). Keeping this an abstraction lets <c>App.BLL</c> stay
/// independent of any specific transport.
/// </summary>
public interface IPositionResultPublisher
{
    Task PublishAsync(PositionResultBroadcast broadcast, CancellationToken ct = default);
}
