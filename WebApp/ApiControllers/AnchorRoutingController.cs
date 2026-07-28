using App.BLL.Positioning;
using Microsoft.AspNetCore.Mvc;

namespace WebApp.ApiControllers;

[Route("api/anchor-routing")]
[ApiController]
public sealed class AnchorRoutingController : ControllerBase
{
    private readonly IAnchorRoutingService _routing;

    public AnchorRoutingController(IAnchorRoutingService routing)
    {
        _routing = routing;
    }

    [HttpGet("sessions/{sessionId:guid}/tags/{tagDeviceIdentifier}/next-anchors")]
    public async Task<ActionResult<AnchorRoutingDecision>> GetNextAnchors(
        Guid sessionId,
        string tagDeviceIdentifier,
        [FromQuery] int count = AnchorRoutingService.DefaultAnchorCount,
        CancellationToken ct = default)
    {
        var decision = await _routing.GetNextAnchorsAsync(sessionId, tagDeviceIdentifier, count, ct);
        return decision is null ? NotFound() : Ok(decision);
    }
}
