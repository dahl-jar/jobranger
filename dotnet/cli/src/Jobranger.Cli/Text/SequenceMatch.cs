namespace Jobranger.Cli.Text;

internal static class SequenceMatch {
    private const int autojunkMinLength = 200;
    private const int popularDivisor = 100;
    private const double emptyRatio = 1.0;

    public static double Ratio(string left, string right) =>
        Ratio([.. CodePoints.Of(left)], [.. CodePoints.Of(right)]);

    public static double Ratio(int[] a, int[] b) => Score(MatchedCount(a, b), a.Length + b.Length);

    public static Dictionary<int, int> Counts(int[] codes) {
        Dictionary<int, int> counts = [];
        foreach (int code in codes) {
            counts[code] = counts.GetValueOrDefault(code) + 1;
        }
        return counts;
    }

    public static double QuickRatio(Dictionary<int, int> left, Dictionary<int, int> right) {
        int shared = 0;
        foreach ((int code, int count) in left) {
            shared += Math.Min(count, right.GetValueOrDefault(code));
        }
        return Score(shared, left.Values.Sum() + right.Values.Sum());
    }

    private static double Score(int matched, int length) => length == 0 ? emptyRatio : 2.0 * matched / length;

    private static int MatchedCount(int[] a, int[] b) {
        Dictionary<int, int[]> positions = Positions(b);
        int[] lengths = new int[RowWidth(b) * 2];
        Stack<(int ALow, int AHigh, int BLow, int BHigh)> pending = new();
        pending.Push((0, a.Length, 0, b.Length));
        int matched = 0;
        while (pending.Count > 0) {
            (int aLow, int aHigh, int bLow, int bHigh) = pending.Pop();
            (int i, int j, int size) = LongestMatch(a, b, positions, lengths, (aLow, aHigh, bLow, bHigh));
            if (size == 0) {
                continue;
            }
            matched += size;
            if (aLow < i && bLow < j) {
                pending.Push((aLow, i, bLow, j));
            }
            if (i + size < aHigh && j + size < bHigh) {
                pending.Push((i + size, aHigh, j + size, bHigh));
            }
        }
        return matched;
    }

    private static Dictionary<int, int[]> Positions(int[] b) {
        Dictionary<int, List<int>> positions = [];
        for (int index = 0; index < b.Length; index++) {
            if (!positions.TryGetValue(b[index], out List<int>? indices)) {
                indices = [];
                positions[b[index]] = indices;
            }
            indices.Add(index);
        }
        int popular = b.Length >= autojunkMinLength ? b.Length / popularDivisor + 1 : int.MaxValue;
        return positions.Where(entry => entry.Value.Count <= popular)
            .ToDictionary(entry => entry.Key, entry => entry.Value.ToArray());
    }

    private static (int I, int J, int Size) LongestMatch(
        int[] a,
        int[] b,
        Dictionary<int, int[]> positions,
        int[] lengths,
        (int ALow, int AHigh, int BLow, int BHigh) range) {
        int bestI = range.ALow;
        int bestJ = range.BLow;
        int bestSize = 0;
        int width = RowWidth(b);
        int previous = 0;
        List<int> written = [];
        List<int> writing = [];
        for (int i = range.ALow; i < range.AHigh; i++) {
            int current = width - previous;
            int[] indices = positions.GetValueOrDefault(a[i]) ?? [];
            int end = LowerBound(indices, range.BHigh);
            for (int index = LowerBound(indices, range.BLow); index < end; index++) {
                int j = indices[index];
                int size = lengths[previous + j] + 1;
                lengths[current + j + 1] = size;
                writing.Add(current + j + 1);
                if (size > bestSize) {
                    (bestI, bestJ, bestSize) = (i - size + 1, j - size + 1, size);
                }
            }
            Clear(lengths, written);
            (written, writing) = (writing, written);
            previous = current;
        }
        Clear(lengths, written);
        while (bestI > range.ALow && bestJ > range.BLow && a[bestI - 1] == b[bestJ - 1]) {
            (bestI, bestJ, bestSize) = (bestI - 1, bestJ - 1, bestSize + 1);
        }
        while (bestI + bestSize < range.AHigh && bestJ + bestSize < range.BHigh
            && a[bestI + bestSize] == b[bestJ + bestSize]) {
            bestSize++;
        }
        return (bestI, bestJ, bestSize);
    }

    private static int LowerBound(int[] indices, int value) {
        int found = Array.BinarySearch(indices, value);
        return found < 0 ? ~found : found;
    }

    private static void Clear(int[] lengths, List<int> written) {
        foreach (int index in written) {
            lengths[index] = 0;
        }
        written.Clear();
    }

    private static int RowWidth(int[] b) => b.Length + 1;
}
