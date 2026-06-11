namespace App.BLL.Positioning;

/// <summary>
/// Result of a trilateration solve.
/// </summary>
/// <param name="X">Tag X coordinate (m).</param>
/// <param name="Y">Tag Y coordinate (m).</param>
/// <param name="Z">Tag Z coordinate (m). Equal to the caller-supplied plane in 2D mode.</param>
/// <param name="Residual">
/// RMS of per-anchor distance residuals (m). Lower is better.
/// Suitable for populating <c>PositionResult.Accuracy</c>.
/// </param>
/// <param name="AnchorCount">Number of anchors actually used in the solve.</param>
/// <param name="Iterations">Gauss–Newton iterations performed (0 means linear init only).</param>
public readonly record struct PositionFix(
    double X,
    double Y,
    double Z,
    double Residual,
    int AnchorCount,
    int Iterations);
