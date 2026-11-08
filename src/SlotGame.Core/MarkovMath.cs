namespace SlotGame.Core;

public static class MarkovMath
{
    // One balance equation is replaced by sum(pi) = 1; direct solve, no iteration.
    public static double[] StationaryDistribution(double[,] transition)
    {
        var n = transition.GetLength(0);
        var a = new double[n, n];
        var b = new double[n];
        for (var row = 0; row < n - 1; row++)
        for (var column = 0; column < n; column++)
            a[row, column] = transition[column, row] - (row == column ? 1 : 0);
        for (var column = 0; column < n; column++) a[n - 1, column] = 1;
        b[n - 1] = 1;
        // Transient (unreachable-in-the-long-run) states have pi = 0; clear round-off such as -1e-18.
        return Solve(a, b).Select(p => Math.Abs(p) < 1e-12 ? 0 : p).ToArray();
    }

    // Z = (I - P + 1 pi)^-1. For pi.r = 0, sum_k P^k r = Z r.
    public static double[,] FundamentalMatrix(double[,] transition, IReadOnlyList<double> stationary)
    {
        var n = transition.GetLength(0);
        var m = new double[n, n];
        for (var row = 0; row < n; row++)
        for (var column = 0; column < n; column++)
            m[row, column] = (row == column ? 1 : 0) - transition[row, column] + stationary[column];
        return Invert(m);
    }

    public static double[] Multiply(double[,] matrix, IReadOnlyList<double> vector)
    {
        var n = matrix.GetLength(0);
        var result = new double[n];
        for (var row = 0; row < n; row++)
        for (var column = 0; column < vector.Count; column++)
            result[row] += matrix[row, column] * vector[column];
        return result;
    }

    public static double[] Solve(double[,] matrix, double[] rhs)
    {
        var n = rhs.Length;
        var a = (double[,])matrix.Clone();
        var b = (double[])rhs.Clone();
        for (var pivot = 0; pivot < n; pivot++)
        {
            var best = pivot;
            for (var row = pivot + 1; row < n; row++)
                if (Math.Abs(a[row, pivot]) > Math.Abs(a[best, pivot])) best = row;
            if (Math.Abs(a[best, pivot]) < 1e-300) throw new InvalidOperationException("Singular linear system.");
            if (best != pivot)
            {
                for (var column = 0; column < n; column++) (a[pivot, column], a[best, column]) = (a[best, column], a[pivot, column]);
                (b[pivot], b[best]) = (b[best], b[pivot]);
            }
            for (var row = 0; row < n; row++)
            {
                if (row == pivot) continue;
                var factor = a[row, pivot] / a[pivot, pivot];
                if (factor == 0) continue;
                for (var column = pivot; column < n; column++) a[row, column] -= factor * a[pivot, column];
                b[row] -= factor * b[pivot];
            }
        }
        var x = new double[n];
        for (var i = 0; i < n; i++) x[i] = b[i] / a[i, i];
        return x;
    }

    public static double[,] Invert(double[,] matrix)
    {
        var n = matrix.GetLength(0);
        var inverse = new double[n, n];
        for (var column = 0; column < n; column++)
        {
            var unit = new double[n];
            unit[column] = 1;
            var solved = Solve(matrix, unit);
            for (var row = 0; row < n; row++) inverse[row, column] = solved[row];
        }
        return inverse;
    }

    public static IReadOnlySet<int> Reachable(double[,] transition, int start)
    {
        var seen = new HashSet<int> { start };
        var queue = new Queue<int>([start]);
        while (queue.Count > 0)
        {
            var from = queue.Dequeue();
            for (var to = 0; to < transition.GetLength(1); to++)
                if (transition[from, to] > 0 && seen.Add(to)) queue.Enqueue(to);
        }
        return seen;
    }
}
