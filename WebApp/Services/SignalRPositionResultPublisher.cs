using App.BLL.Positioning;
using Microsoft.AspNetCore.SignalR;
using WebApp.Hubs;

namespace WebApp.Services;

/// <summary>
/// Bridges <see cref="IPositionResultPublisher"/> to the SignalR
/// <see cref="PositioningHub"/>. Broadcasts to all clients plus the per-session
/// group, matching the existing MQTT-driven raw-measurement path.
/// </summary>
public sealed class SignalRPositionResultPublisher : IPositionResultPublisher
{
    private readonly IHubContext<PositioningHub> _hub;

    public SignalRPositionResultPublisher(IHubContext<PositioningHub> hub)
    {
        _hub = hub;
    }

    public async Task PublishAsync(PositionResultBroadcast b, CancellationToken ct = default)
    {
        // All connected clients (useful for a global dashboard).
        await _hub.Clients.All.SendAsync("PositionResult", b, ct);

        // Per-session subscribers.
        await _hub.Clients
            .Group(PositioningHub.GroupName(b.SessionId))
            .SendAsync("PositionResult", b, ct);
    }
}
