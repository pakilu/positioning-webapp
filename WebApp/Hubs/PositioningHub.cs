using Microsoft.AspNetCore.SignalR;

namespace WebApp.Hubs;

/// <summary>
/// SignalR hub used as the WebSocket endpoint for live positioning data.
/// Clients connect to <c>/hubs/positioning</c> and receive:
///   - "RawMeasurement"  -> RawMeasurementMessage
///   - "PositionResult"  -> PositionResultMessage
///
/// The server pushes; clients only need to listen. Optionally clients may
/// join a per-session group via <see cref="JoinSession"/> so they only
/// receive events for that session.
/// </summary>
public class PositioningHub : Hub
{
    public Task JoinSession(Guid sessionId) =>
        Groups.AddToGroupAsync(Context.ConnectionId, GroupName(sessionId));

    public Task LeaveSession(Guid sessionId) =>
        Groups.RemoveFromGroupAsync(Context.ConnectionId, GroupName(sessionId));

    public static string GroupName(Guid sessionId) => $"session:{sessionId}";
}
