namespace App.BLL.Positioning;

/// <summary>
/// (X, Y, Z) coordinates of an anchor in the session's local frame, in meters.
/// </summary>
public readonly record struct AnchorPosition(double X, double Y, double Z);

/// <summary>
/// Looks up the configured anchor coordinates for a session.
///
/// A session points to a <c>SessionConfig</c>, which has a collection of
/// <c>SessionConfigChip</c> rows. The anchor entries (role == Anchor) define
/// where each anchor chip is physically placed for this session.
///
/// Implementations are expected to cache aggressively — anchor layout does not
/// change during a session, but the trilateration pipeline calls this on every
/// incoming measurement (potentially &gt;100 Hz).
/// </summary>
public interface IAnchorPositionProvider
{
    /// <summary>
    /// Returns the anchor map for <paramref name="sessionId"/>: a dictionary
    /// keyed by <c>ChipId</c> (NOT <c>SessionConfigChipId</c>) so callers can
    /// join directly against incoming measurements.
    /// </summary>
    /// <returns>
    /// An empty dictionary if the session does not exist or has no anchors.
    /// </returns>
    Task<IReadOnlyDictionary<Guid, AnchorPosition>> GetAnchorsAsync(
        Guid sessionId, CancellationToken ct = default);

    /// <summary>
    /// Drop the cached entry for this session. Call after the admin edits the
    /// session's configuration so the next solve picks up the new layout.
    /// </summary>
    void Invalidate(Guid sessionId);
}
