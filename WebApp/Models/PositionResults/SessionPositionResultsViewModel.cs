using App.Domain;

namespace WebApp.Models.PositionResults;

public class SessionPositionResultsViewModel
{
    public Guid? SelectedSessionId { get; set; }

    public IReadOnlyList<SessionPositionResultsGroup> Sessions { get; set; } = [];
}

public class SessionPositionResultsGroup
{
    public Session Session { get; set; } = default!;

    public IReadOnlyList<PositionResult> Results { get; set; } = [];
}
