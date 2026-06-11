namespace App.BLL.Positioning;

/// <summary>
/// A single anchor-to-tag distance sample, as consumed by <see cref="ITrilaterationSolver"/>.
/// Coordinates are in meters in the session's local coordinate frame.
/// </summary>
/// <param name="X">Anchor X coordinate (m).</param>
/// <param name="Y">Anchor Y coordinate (m).</param>
/// <param name="Z">Anchor Z coordinate (m). Ignored in 2D mode.</param>
/// <param name="Distance">Measured anchor-to-tag distance (m).</param>
public readonly record struct AnchorMeasurement(double X, double Y, double Z, double Distance);
