namespace App.BLL.Positioning;

/// <summary>
/// Tiny dense linear solver for 2×2 and 3×3 symmetric positive-definite systems
/// (the normal equations Aᵀ A x = Aᵀ b that arise in trilateration).
/// Uses Gaussian elimination with partial pivoting — fine for these sizes.
/// </summary>
internal static class SmallLinearSolver
{
    /// <summary>
    /// Solves <paramref name="a"/> · x = <paramref name="b"/> in place.
    /// <paramref name="a"/> is <paramref name="n"/>×<paramref name="n"/> row-major,
    /// <paramref name="b"/> is length <paramref name="n"/>.
    /// On success the solution is written into <paramref name="b"/>.
    /// </summary>
    /// <returns><c>false</c> if the matrix is (numerically) singular.</returns>
    public static bool SolveInPlace(double[] a, double[] b, int n)
    {
        for (int k = 0; k < n; k++)
        {
            // Partial pivot
            int pivot = k;
            double max = Math.Abs(a[k * n + k]);
            for (int i = k + 1; i < n; i++)
            {
                double v = Math.Abs(a[i * n + k]);
                if (v > max) { max = v; pivot = i; }
            }
            if (max < 1e-12) return false;

            if (pivot != k)
            {
                for (int j = 0; j < n; j++)
                    (a[k * n + j], a[pivot * n + j]) = (a[pivot * n + j], a[k * n + j]);
                (b[k], b[pivot]) = (b[pivot], b[k]);
            }

            // Eliminate
            double akk = a[k * n + k];
            for (int i = k + 1; i < n; i++)
            {
                double factor = a[i * n + k] / akk;
                if (factor == 0.0) continue;
                for (int j = k; j < n; j++)
                    a[i * n + j] -= factor * a[k * n + j];
                b[i] -= factor * b[k];
            }
        }

        // Back-substitute
        for (int i = n - 1; i >= 0; i--)
        {
            double s = b[i];
            for (int j = i + 1; j < n; j++)
                s -= a[i * n + j] * b[j];
            b[i] = s / a[i * n + i];
        }
        return true;
    }
}
