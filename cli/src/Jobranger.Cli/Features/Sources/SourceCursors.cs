using System.Globalization;
using Jobranger.Cli.Storage;

namespace Jobranger.Cli.Features.Sources;

internal static class SourceCursors {
    private static readonly TimeSpan firstRunWindow = TimeSpan.FromDays(7);
    private static readonly TimeSpan cursorOverlap = TimeSpan.FromDays(1);

    public static DateTimeOffset Start(FetchContext context, string key) =>
        context.Cursors.TryGetValue(key, out string? stored)
        && DateTimeOffset.TryParse(stored, CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal,
            out DateTimeOffset since)
            ? since
            : context.RunStarted - firstRunWindow;

    public static string Advanced(DateTimeOffset runStarted) => StoredTime.Format(runStarted - cursorOverlap);
}
