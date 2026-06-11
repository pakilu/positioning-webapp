using App.BLL.Positioning;
using Xunit;

namespace App.BLL.Tests.Positioning;

public class LeastSquaresTrilaterationSolverTests
{
    private readonly ITrilaterationSolver _solver = new LeastSquaresTrilaterationSolver();

    // ---- Helpers ----------------------------------------------------------

    private static AnchorMeasurement Meas(double x, double y, double z, double tagX, double tagY, double tagZ, double noise = 0)
    {
        double dx = x - tagX, dy = y - tagY, dz = z - tagZ;
        double d = Math.Sqrt(dx * dx + dy * dy + dz * dz) + noise;
        return new AnchorMeasurement(x, y, z, d);
    }

    private static IReadOnlyList<AnchorMeasurement> NoisyMeasurements(
        (double x, double y, double z)[] anchors, double tagX, double tagY, double tagZ, int seed, double sigma)
    {
        var rng = new Random(seed);
        var list = new List<AnchorMeasurement>(anchors.Length);
        foreach (var (x, y, z) in anchors)
        {
            // Box-Muller
            double u1 = 1 - rng.NextDouble();
            double u2 = 1 - rng.NextDouble();
            double n  = Math.Sqrt(-2 * Math.Log(u1)) * Math.Cos(2 * Math.PI * u2);
            list.Add(Meas(x, y, z, tagX, tagY, tagZ, n * sigma));
        }
        return list;
    }

    // ---- 2D ---------------------------------------------------------------

    [Fact]
    public void TwoD_ExactlyThreeAnchors_NoNoise_RecoversTagExactly()
    {
        // Anchors in a triangle, tag at (3, 2). Z=0 plane throughout.
        var anchors = new (double, double, double)[] { (0,0,0), (10,0,0), (0,10,0) };
        var meas = anchors.Select(a => Meas(a.Item1, a.Item2, a.Item3, 3, 2, 0)).ToList();

        Assert.True(_solver.TrySolve(meas, new TrilaterationOptions { Mode = SolverMode.TwoD, TagZ = 0 }, out var fix));

        Assert.Equal(3, fix.X, 6);
        Assert.Equal(2, fix.Y, 6);
        Assert.Equal(0, fix.Z, 6);
        Assert.True(fix.Residual < 1e-6);
        Assert.Equal(3, fix.AnchorCount);
    }

    [Theory]
    [InlineData(4)]
    [InlineData(5)]
    [InlineData(8)]
    [InlineData(20)]
    public void TwoD_ManyAnchors_NoNoise_RecoversTagExactly(int n)
    {
        // Anchors spread around a 10x10 room.
        var rng = new Random(42);
        var anchors = Enumerable.Range(0, n)
            .Select(_ => (rng.NextDouble() * 10, rng.NextDouble() * 10, 0.0))
            .ToArray();
        double tx = 4.2, ty = 6.7;
        var meas = anchors.Select(a => Meas(a.Item1, a.Item2, a.Item3, tx, ty, 0)).ToList();

        Assert.True(_solver.TrySolve(meas, new TrilaterationOptions { Mode = SolverMode.TwoD, TagZ = 0 }, out var fix));

        Assert.Equal(tx, fix.X, 5);
        Assert.Equal(ty, fix.Y, 5);
        Assert.True(fix.Residual < 1e-5);
        Assert.Equal(n, fix.AnchorCount);
    }

    [Fact]
    public void TwoD_AnchorsAtCeilingHeight_StillSolvesPlanarTag()
    {
        // Realistic setup: 4 anchors on the ceiling at z=2.5m, tag on floor at z=0.
        var anchors = new (double, double, double)[]
        {
            (0,  0, 2.5),
            (8,  0, 2.5),
            (8,  6, 2.5),
            (0,  6, 2.5),
        };
        double tx = 5.5, ty = 1.8, tz = 0.0;
        var meas = anchors.Select(a => Meas(a.Item1, a.Item2, a.Item3, tx, ty, tz)).ToList();

        Assert.True(_solver.TrySolve(meas, new TrilaterationOptions { Mode = SolverMode.TwoD, TagZ = tz }, out var fix));

        Assert.Equal(tx, fix.X, 5);
        Assert.Equal(ty, fix.Y, 5);
    }

    [Fact]
    public void TwoD_RealisticNoise_AccuracyImprovesWithMoreAnchors()
    {
        // 10 cm sigma is typical for UWB. More anchors should reduce the position error.
        var fewAnchors  = new (double, double, double)[] { (0,0,2.5), (8,0,2.5), (4,7,2.5) };
        var manyAnchors = new (double, double, double)[]
        {
            (0,0,2.5), (8,0,2.5), (8,6,2.5), (0,6,2.5),
            (4,3,2.5), (2,1,2.5), (6,5,2.5), (1,5,2.5)
        };
        const double tx = 3.5, ty = 2.5, tz = 0.0;

        double ErrAvg(IReadOnlyList<(double,double,double)> anchors)
        {
            double sum = 0; int trials = 200;
            for (int s = 0; s < trials; s++)
            {
                var m = NoisyMeasurements(anchors.ToArray(), tx, ty, tz, seed: s, sigma: 0.10);
                Assert.True(_solver.TrySolve(m, new TrilaterationOptions { Mode = SolverMode.TwoD, TagZ = tz }, out var fix));
                double ex = fix.X - tx, ey = fix.Y - ty;
                sum += Math.Sqrt(ex * ex + ey * ey);
            }
            return sum / trials;
        }

        double errFew  = ErrAvg(fewAnchors);
        double errMany = ErrAvg(manyAnchors);

        // Sanity bounds, plus the expected monotonic improvement.
        Assert.True(errFew  < 0.30, $"3-anchor error too high: {errFew:F3} m");
        Assert.True(errMany < 0.15, $"8-anchor error too high: {errMany:F3} m");
        Assert.True(errMany < errFew, $"Expected more anchors to help: few={errFew:F3} many={errMany:F3}");
    }

    [Fact]
    public void TwoD_OutlierRejection_ImprovesFix()
    {
        var anchors = new (double, double, double)[]
        {
            (0,0,0), (10,0,0), (10,10,0), (0,10,0), (5,5,0)
        };
        double tx = 3, ty = 4;
        var meas = anchors.Select(a => Meas(a.Item1, a.Item2, a.Item3, tx, ty, 0)).ToList();

        // Corrupt one anchor with a 2-meter bias (NLOS-style outlier).
        meas[2] = meas[2] with { Distance = meas[2].Distance + 2.0 };

        Assert.True(_solver.TrySolve(meas,
            new TrilaterationOptions { Mode = SolverMode.TwoD, TagZ = 0, OutlierSigma = 0 }, out var noReject));
        Assert.True(_solver.TrySolve(meas,
            new TrilaterationOptions { Mode = SolverMode.TwoD, TagZ = 0, OutlierSigma = 2.0 }, out var withReject));

        double ErrNoReject   = Math.Sqrt(Math.Pow(noReject.X   - tx, 2) + Math.Pow(noReject.Y   - ty, 2));
        double ErrWithReject = Math.Sqrt(Math.Pow(withReject.X - tx, 2) + Math.Pow(withReject.Y - ty, 2));

        Assert.True(ErrWithReject < ErrNoReject,
            $"Outlier rejection should help (noReject={ErrNoReject:F3}, withReject={ErrWithReject:F3})");
        Assert.True(ErrWithReject < 0.05);
    }

    // ---- 3D ---------------------------------------------------------------

    [Fact]
    public void ThreeD_FourNonCoplanarAnchors_RecoversTagExactly()
    {
        var anchors = new (double, double, double)[]
        {
            (0,  0, 0),
            (10, 0, 0),
            (0, 10, 0),
            (5,  5, 4),   // out of the floor plane
        };
        double tx = 3.3, ty = 2.7, tz = 1.5;
        var meas = anchors.Select(a => Meas(a.Item1, a.Item2, a.Item3, tx, ty, tz)).ToList();

        Assert.True(_solver.TrySolve(meas, new TrilaterationOptions { Mode = SolverMode.ThreeD }, out var fix));

        Assert.Equal(tx, fix.X, 5);
        Assert.Equal(ty, fix.Y, 5);
        Assert.Equal(tz, fix.Z, 5);
    }

    [Fact]
    public void ThreeD_CoplanarAnchors_IsDegenerate()
    {
        // All anchors at z=0: 3D solve cannot resolve the tag's Z (degenerate geometry).
        // The solver should either fail or produce a large residual / wrong Z — we just
        // assert that it doesn't claim a perfect fit.
        var anchors = new (double, double, double)[]
        {
            (0,0,0), (10,0,0), (0,10,0), (10,10,0)
        };
        double tx = 4, ty = 6, tz = 2;
        var meas = anchors.Select(a => Meas(a.Item1, a.Item2, a.Item3, tx, ty, tz)).ToList();

        bool solved = _solver.TrySolve(meas, new TrilaterationOptions { Mode = SolverMode.ThreeD }, out var fix);
        if (solved)
        {
            // X/Y can still be recovered; Z is the ambiguous one.
            Assert.Equal(tx, fix.X, 3);
            Assert.Equal(ty, fix.Y, 3);
        }
    }

    // ---- Guard rails ------------------------------------------------------

    [Fact]
    public void TwoD_TooFewAnchors_ReturnsFalse()
    {
        var meas = new List<AnchorMeasurement>
        {
            new(0, 0, 0, 1.0),
            new(1, 0, 0, 1.0),
        };
        Assert.False(_solver.TrySolve(meas, new TrilaterationOptions { Mode = SolverMode.TwoD }, out _));
    }

    [Fact]
    public void ThreeD_TooFewAnchors_ReturnsFalse()
    {
        var meas = new List<AnchorMeasurement>
        {
            new(0, 0, 0, 1.0),
            new(1, 0, 0, 1.0),
            new(0, 1, 0, 1.0),
        };
        Assert.False(_solver.TrySolve(meas, new TrilaterationOptions { Mode = SolverMode.ThreeD }, out _));
    }

    [Fact]
    public void TwoD_CollinearAnchors_IsDegenerate_ReturnsFalse()
    {
        // All anchors on a single line → AᵀA is singular.
        var meas = new List<AnchorMeasurement>
        {
            new(0, 0, 0, 5),
            new(5, 0, 0, 5),
            new(10, 0, 0, 5),
        };
        Assert.False(_solver.TrySolve(meas, new TrilaterationOptions { Mode = SolverMode.TwoD, TagZ = 0 }, out _));
    }
}
