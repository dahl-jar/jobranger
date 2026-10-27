using System.Collections.Frozen;

namespace Jobranger.Cli.Http;

internal sealed record SourceRequestOptions {
    private const long defaultMaxBodyBytes = 10L * 1024 * 1024;

    public IReadOnlyDictionary<string, string> Headers { get; init; } = FrozenDictionary<string, string>.Empty;

    public long MaxBodyBytes { get; init; } = defaultMaxBodyBytes;

    public TimeProvider Time { get; init; } = TimeProvider.System;
}
