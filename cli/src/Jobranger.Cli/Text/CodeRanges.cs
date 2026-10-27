namespace Jobranger.Cli.Text;

internal sealed class CodeRanges(IEnumerable<(int First, int Last)> ranges) {
    private static readonly Comparer<(int First, int Last)> overlap = Comparer<(int First, int Last)>.Create(
        (range, probe) => range.Last < probe.First ? -1 : range.First > probe.Last ? 1 : 0);

    private readonly (int First, int Last)[] bounds = [.. ranges.OrderBy(range => range.First)];

    public bool Contains(int code) => Array.BinarySearch(bounds, (code, code), overlap) >= 0;
}
