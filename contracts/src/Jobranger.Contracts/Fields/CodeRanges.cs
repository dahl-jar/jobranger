using System.Diagnostics.CodeAnalysis;

namespace Jobranger.Contracts.Fields;

internal sealed class CodeRanges<T> {
    private static readonly Comparer<(int First, int Last)> overlap = Comparer<(int First, int Last)>.Create((range, probe) =>
        range.Last < probe.First ? -1 : range.First > probe.Last ? 1 : 0);

    private readonly (int First, int Last)[] bounds;
    private readonly T[] values;

    public CodeRanges(IEnumerable<(int First, int Last, T Value)> ranges) {
        (int First, int Last, T Value)[] sorted = [.. ranges.OrderBy(range => range.First)];
        bounds = [.. sorted.Select(range => (range.First, range.Last))];
        values = [.. sorted.Select(range => range.Value)];
    }

    public bool Contains(int code) => Array.BinarySearch(bounds, (code, code), overlap) >= 0;

    public bool TryFind(int code, [MaybeNullWhen(false)] out T value) {
        int index = Array.BinarySearch(bounds, (code, code), overlap);
        value = index >= 0 ? values[index] : default;
        return index >= 0;
    }
}
