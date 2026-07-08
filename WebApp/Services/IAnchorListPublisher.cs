namespace WebApp.Services;

/// <summary>
/// Publishes retained per-tag anchor lists over MQTT so tags can dynamically
/// learn which anchors to range against. Each tag firmware subscribes to
/// <c>uwb/tags/{tagDeviceId}/anchors</c> (topic template configurable via
/// <see cref="WebApp.Models.Mqtt.MqttOptions.AnchorListTopicTemplate"/>) and
/// reconciles its internal session list whenever a new retained message
/// arrives.
/// </summary>
public interface IAnchorListPublisher
{
    /// <summary>
    /// Publishes a retained JSON array of anchor DeviceIdentifiers for the
    /// given tag. Passing an empty list clears the tag's anchor set (tag
    /// will stop ranging until a non-empty list is published).
    /// </summary>
    Task PublishAnchorListAsync(
        string tagDeviceIdentifier,
        IEnumerable<string> anchorDeviceIdentifiers,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Convenience: for a given session, look up its Tag/Anchor chips via the
    /// SessionConfig and publish anchor lists to each of the session's tags.
    /// If <paramref name="clear"/> is true, publishes an empty list to each
    /// tag instead (used on Finish/Cancel).
    /// </summary>
    Task PublishForSessionAsync(Guid sessionId, bool clear = false, CancellationToken cancellationToken = default);
}
