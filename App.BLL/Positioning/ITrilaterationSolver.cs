namespace App.BLL.Positioning;

/// <summary>
/// Geometry the solver should fit.
/// </summary>
public enum SolverMode
{
    /// <summary>
    /// Solve for (X, Y) on a single horizontal plane.
    /// Caller supplies that plane's Z via <see cref="TrilaterationOptions.TagZ"/>;
    /// the solver subtracts the vertical leg from each distance so anchors at different
    /// heights are handled correctly. Requires at least 3 anchors.
    /// </summary>
    TwoD = 2,

    /// <summary>
    /// Solve for (X, Y, Z). Requires at least 4 non-coplanar anchors.
    /// </summary>
    ThreeD = 3,
}

/// <summary>
/// Knobs for the solver. Defaults are sensible for indoor UWB at ~10 cm ranging noise.
/// </summary>
public sealed class TrilaterationOptions
{
    /// <summary>Geometry to fit.</summary>
    public SolverMode Mode { get; set; } = SolverMode.TwoD;

    /// <summary>
    /// Assumed tag Z plane (m) for <see cref="SolverMode.TwoD"/>. Ignored in 3D mode.
    /// If null in 2D mode, the mean anchor Z is used.
    /// </summary>
    public double? TagZ { get; set; }

    /// <summary>Maximum Gauss–Newton iterations after the linear initial guess.</summary>
    public int MaxIterations { get; set; } = 8;

    /// <summary>Convergence threshold on the update vector norm (m).</summary>
    public double ConvergenceTolerance { get; set; } = 1e-5;

    /// <summary>
    /// If &gt; 0 and there are enough anchors, drop the single anchor whose residual
    /// exceeds <c>OutlierSigma * median(|residual|)</c> after the first solve and re-solve.
    /// Set to 0 to disable.
    /// </summary>
    public double OutlierSigma { get; set; } = 0.0;
}

/// <summary>
/// Computes a tag position from N≥3 (2D) or N≥4 (3D) anchor distance measurements
/// using linear least-squares followed by Gauss–Newton refinement.
/// </summary>
public interface ITrilaterationSolver
{
    /// <summary>
    /// Attempts to compute a position fix.
    /// </summary>
    /// <param name="measurements">Anchor positions and measured distances.</param>
    /// <param name="options">Solver options. <c>null</c> uses defaults (2D, mean anchor Z).</param>
    /// <param name="fix">The computed fix on success.</param>
    /// <returns>
    /// <c>true</c> if a fix was produced; <c>false</c> if the inputs are insufficient
    /// or the geometry is degenerate (e.g. all anchors collinear in 2D).
    /// </returns>
    bool TrySolve(
        IReadOnlyList<AnchorMeasurement> measurements,
        TrilaterationOptions? options,
        out PositionFix fix);
}
