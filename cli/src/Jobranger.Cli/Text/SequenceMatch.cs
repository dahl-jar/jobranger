namespace Jobranger.Cli.Text;

internal static class SequenceMatch {
    private const int autojunkMinLength = 200;
    private const int popularDivisor = 100;
    private const double emptyRatio = 1.0;

    public static double Ratio(string left, string right) {
        int[] a = [.. CodePoints.Of(left)];
        int[] b = [.. CodePoints.Of(right)];
        int length = a.Length + b.Length;
        return length == 0 ? emptyRatio : 2.0 * MatchedCount(a, b) / length;
    }

    private static int MatchedCount(int[] a, int[] b) {
        Dictionary<int, List<int>> positions = Positions(b);
        Stack<(int ALow, int AHigh, int BLow, int BHigh)> pending = new();
        pending.Push((0, a.Length, 0, b.Length));
        int matched = 0;
        while (pending.Count > 0) {
            (int aLow, int aHigh, int bLow, int bHigh) = pending.Pop();
            (int i, int j, int size) = LongestMatch(a, b, positions, (aLow, aHigh, bLow, bHigh));
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

    private static Dictionary<int, List<int>> Positions(int[] b) {
        Dictionary<int, List<int>> positions = [];
        for (int index = 0; index < b.Length; index++) {
            if (!positions.TryGetValue(b[index], out List<int>? indices)) {
                indices = [];
                positions[b[index]] = indices;
            }
            indices.Add(index);
        }
        if (b.Length >= autojunkMinLength) {
            int popular = b.Length / popularDivisor + 1;
            int[] popularElements = [.. positions.Where(entry => entry.Value.Count > popular).Select(entry => entry.Key)];
            foreach (int element in popularElements) {
                positions.Remove(element);
            }
        }
        return positions;
    }

    private static (int I, int J, int Size) LongestMatch(
        int[] a, int[] b, Dictionary<int, List<int>> positions, (int ALow, int AHigh, int BLow, int BHigh) range) {
        int bestI = range.ALow;
        int bestJ = range.BLow;
        int bestSize = 0;
        Dictionary<int, int> lengths = [];
        Dictionary<int, int> next = [];
        for (int i = range.ALow; i < range.AHigh; i++) {
            next.Clear();
            foreach (int j in positions.GetValueOrDefault(a[i]) ?? []) {
                if (j < range.BLow) {
                    continue;
                }
                if (j >= range.BHigh) {
                    break;
                }
                int size = lengths.GetValueOrDefault(j - 1) + 1;
                next[j] = size;
                if (size > bestSize) {
                    (bestI, bestJ, bestSize) = (i - size + 1, j - size + 1, size);
                }
            }
            (lengths, next) = (next, lengths);
        }
        while (bestI > range.ALow && bestJ > range.BLow && a[bestI - 1] == b[bestJ - 1]) {
            (bestI, bestJ, bestSize) = (bestI - 1, bestJ - 1, bestSize + 1);
        }
        while (bestI + bestSize < range.AHigh && bestJ + bestSize < range.BHigh
            && a[bestI + bestSize] == b[bestJ + bestSize]) {
            bestSize++;
        }
        return (bestI, bestJ, bestSize);
    }
}
