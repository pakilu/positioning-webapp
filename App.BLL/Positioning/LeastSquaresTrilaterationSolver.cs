namespace App.BLL.Positioning;

/// <summary>
/// Standard two-stage trilateration solver:
///   1. Linear least-squares initial guess (closed-form, anchor 0 as reference).
///   2. Gauss–Newton refinement on the nonlinear distance residuals.
///
/// Handles any N ≥ 3 (2D) or N ≥ 4 (3D). Anchors at different heights are handled
/// correctly in 2D mode by subtracting the vertical leg from each distance.
/// </summary>
public sealed class LeastSquaresTrilaterationSolver : ITrilaterationSolver
{
    public bool TrySolve(
        IReadOnlyList<AnchorMeasurement> measurements,
        TrilaterationOptions? options,
        out PositionFix fix)
    {
        options ??= new TrilaterationOptions();
        fix = default;

        int n = measurements.Count;
        int dof = options.Mode == SolverMode.ThreeD ? 3 : 2;
        if (n < dof + 1) return false;

        // --- Project to 2D distances if needed -----------------------------
        // In 2D mode, the tag lies on a known horizontal plane. The slant
        // distance d_i to an anchor at height z_i decomposes as
        //   d_i² = d_horiz² + (z_i - z_tag)²
        // so we subtract the vertical leg before solving the planar problem.
        double tagZ;
        double[] effDist = new double[n];
        if (options.Mode == SolverMode.TwoD)
        {
            tagZ = options.TagZ ?? MeanAnchorZ(measurements);
            for (int i = 0; i < n; i++)
            {
                double dz = measurements[i].Z - tagZ;
                double d2 = measurements[i].Distance * measurements[i].Distance - dz * dz;
                // Clamp tiny negatives caused by noise (anchor "below" the measured slant).
                effDist[i] = d2 > 0 ? Math.Sqrt(d2) : 0.0;
            }
        }
        else
        {
            tagZ = 0; // unused; solver computes it
            for (int i = 0; i < n; i++) effDist[i] = measurements[i].Distance;
        }

        // --- Stage 1: linear initial guess ---------------------------------
        if (!LinearInit(measurements, effDist, dof, out double[] p0))
            return false;

        // --- Stage 2: Gauss–Newton refinement ------------------------------
        int iters = GaussNewton(measurements, effDist, dof, p0, options);
        if (iters < 0) return false;

        double residual = RmsResidual(measurements, effDist, dof, p0);

        fix = dof == 3
            ? new PositionFix(p0[0], p0[1], p0[2], residual, n, iters)
            : new PositionFix(p0[0], p0[1], tagZ,  residual, n, iters);

        // --- Optional single-outlier rejection -----------------------------
        if (options.OutlierSigma > 0 && n > dof + 1)
        {
            int worst = WorstResidualIndex(measurements, effDist, dof, p0, options.OutlierSigma);
            if (worst >= 0)
            {
                var pruned = new List<AnchorMeasurement>(n - 1);
                for (int i = 0; i < n; i++) if (i != worst) pruned.Add(measurements[i]);

                var optsNoOutlier = new TrilaterationOptions
                {
                    Mode = options.Mode,
                    TagZ = options.Mode == SolverMode.TwoD ? tagZ : null,
                    MaxIterations = options.MaxIterations,
                    ConvergenceTolerance = options.ConvergenceTolerance,
                    OutlierSigma = 0.0, // never recurse
                };
                if (TrySolve(pruned, optsNoOutlier, out var refined))
                    fix = refined;
            }
        }

        return true;
    }

    // -----------------------------------------------------------------------
    // Stage 1: linearize by subtracting anchor-0's squared-distance equation
    // from each of the others. The x², y², z² terms cancel, leaving
    //   2(xᵢ-x₀)x + 2(yᵢ-y₀)y [+ 2(zᵢ-z₀)z]
    //     = (d₀² - dᵢ²) + (xᵢ² - x₀²) + (yᵢ² - y₀²) [+ (zᵢ² - z₀²)]
    // Then we solve the normal equations AᵀA p = Aᵀb.
    // -----------------------------------------------------------------------
    private static bool LinearInit(
        IReadOnlyList<AnchorMeasurement> meas, double[] d, int dof, out double[] p)
    {
        int n = meas.Count;
        int rows = n - 1;
        var a0 = meas[0];
        double d0sq = d[0] * d[0];

        // AᵀA (dof×dof) and Aᵀb (dof)
        double[] ata = new double[dof * dof];
        double[] atb = new double[dof];

        for (int i = 1; i < n + 0; i++) // i = 1..n-1
        {
            var ai = meas[i];
            double rx = 2.0 * (ai.X - a0.X);
            double ry = 2.0 * (ai.Y - a0.Y);
            double rz = dof == 3 ? 2.0 * (ai.Z - a0.Z) : 0.0;

            double bi = (d0sq - d[i] * d[i])
                        + (ai.X * ai.X - a0.X * a0.X)
                        + (ai.Y * ai.Y - a0.Y * a0.Y);
            if (dof == 3) bi += (ai.Z * ai.Z - a0.Z * a0.Z);

            // Accumulate row into normal equations
            ata[0 * dof + 0] += rx * rx;
            ata[0 * dof + 1] += rx * ry;
            ata[1 * dof + 0] += ry * rx;
            ata[1 * dof + 1] += ry * ry;
            atb[0] += rx * bi;
            atb[1] += ry * bi;
            if (dof == 3)
            {
                ata[0 * dof + 2] += rx * rz;
                ata[1 * dof + 2] += ry * rz;
                ata[2 * dof + 0] += rz * rx;
                ata[2 * dof + 1] += rz * ry;
                ata[2 * dof + 2] += rz * rz;
                atb[2] += rz * bi;
            }
        }
        _ = rows; // suppress unused warning in some compilers

        if (!SmallLinearSolver.SolveInPlace(ata, atb, dof))
        {
            p = Array.Empty<double>();
            return false;
        }
        p = atb; // solution written in place
        return true;
    }

    // -----------------------------------------------------------------------
    // Stage 2: Gauss–Newton on residuals rᵢ(p) = ‖p - aᵢ‖ - dᵢ.
    // Jacobian row Jᵢ = (p - aᵢ)ᵀ / ‖p - aᵢ‖. Update: p -= (JᵀJ)⁻¹ Jᵀ r.
    // -----------------------------------------------------------------------
    private static int GaussNewton(
        IReadOnlyList<AnchorMeasurement> meas, double[] d, int dof,
        double[] p, TrilaterationOptions opts)
    {
        int n = meas.Count;
        double[] jtj = new double[dof * dof];
        double[] jtr = new double[dof];

        for (int iter = 0; iter < opts.MaxIterations; iter++)
        {
            Array.Clear(jtj);
            Array.Clear(jtr);

            for (int i = 0; i < n; i++)
            {
                var ai = meas[i];
                double dx = p[0] - ai.X;
                double dy = p[1] - ai.Y;
                double dz = dof == 3 ? p[2] - ai.Z : 0.0;
                double dist = Math.Sqrt(dx * dx + dy * dy + dz * dz);
                if (dist < 1e-9) return -1; // tag sitting on top of an anchor: singular

                double inv = 1.0 / dist;
                double jx = dx * inv;
                double jy = dy * inv;
                double jz = dz * inv;
                double r  = dist - d[i];

                jtj[0 * dof + 0] += jx * jx;
                jtj[0 * dof + 1] += jx * jy;
                jtj[1 * dof + 0] += jy * jx;
                jtj[1 * dof + 1] += jy * jy;
                jtr[0] += jx * r;
                jtr[1] += jy * r;
                if (dof == 3)
                {
                    jtj[0 * dof + 2] += jx * jz;
                    jtj[1 * dof + 2] += jy * jz;
                    jtj[2 * dof + 0] += jz * jx;
                    jtj[2 * dof + 1] += jz * jy;
                    jtj[2 * dof + 2] += jz * jz;
                    jtr[2] += jz * r;
                }
            }

            if (!SmallLinearSolver.SolveInPlace(jtj, jtr, dof))
                return iter; // best effort: stop here

            // Step: p ← p - Δ
            double stepNormSq = 0;
            for (int k = 0; k < dof; k++)
            {
                p[k] -= jtr[k];
                stepNormSq += jtr[k] * jtr[k];
            }
            if (Math.Sqrt(stepNormSq) < opts.ConvergenceTolerance)
                return iter + 1;
        }
        return opts.MaxIterations;
    }

    private static double RmsResidual(
        IReadOnlyList<AnchorMeasurement> meas, double[] d, int dof, double[] p)
    {
        int n = meas.Count;
        double sumSq = 0;
        for (int i = 0; i < n; i++)
        {
            var ai = meas[i];
            double dx = p[0] - ai.X;
            double dy = p[1] - ai.Y;
            double dz = dof == 3 ? p[2] - ai.Z : 0.0;
            double r = Math.Sqrt(dx * dx + dy * dy + dz * dz) - d[i];
            sumSq += r * r;
        }
        // Degrees of freedom: n - dof (clamped to ≥1 for the divisor).
        int denom = Math.Max(1, n - dof);
        return Math.Sqrt(sumSq / denom);
    }

    private static int WorstResidualIndex(
        IReadOnlyList<AnchorMeasurement> meas, double[] d, int dof, double[] p, double sigma)
    {
        int n = meas.Count;
        var abs = new double[n];
        int worst = 0;
        double worstVal = -1;
        for (int i = 0; i < n; i++)
        {
            var ai = meas[i];
            double dx = p[0] - ai.X;
            double dy = p[1] - ai.Y;
            double dz = dof == 3 ? p[2] - ai.Z : 0.0;
            double r = Math.Abs(Math.Sqrt(dx * dx + dy * dy + dz * dz) - d[i]);
            abs[i] = r;
            if (r > worstVal) { worstVal = r; worst = i; }
        }

        // Median of absolute residuals
        var sorted = (double[])abs.Clone();
        Array.Sort(sorted);
        double median = (n % 2 == 1)
            ? sorted[n / 2]
            : 0.5 * (sorted[n / 2 - 1] + sorted[n / 2]);

        // Guard: if median ~ 0 the geometry is essentially perfect; nothing to prune.
        if (median < 1e-6) return -1;
        return worstVal > sigma * median ? worst : -1;
    }

    private static double MeanAnchorZ(IReadOnlyList<AnchorMeasurement> meas)
    {
        double s = 0;
        for (int i = 0; i < meas.Count; i++) s += meas[i].Z;
        return s / meas.Count;
    }
}
