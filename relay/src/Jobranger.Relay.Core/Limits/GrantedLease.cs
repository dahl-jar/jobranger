using System.Threading.RateLimiting;

namespace Jobranger.Relay.Core.Limits;

public sealed class GrantedLease : RateLimitLease {
    public static readonly GrantedLease Instance = new();

    public override bool IsAcquired => true;

    public override IEnumerable<string> MetadataNames => [];

    public override bool TryGetMetadata(string metadataName, out object? metadata) {
        metadata = null;
        return false;
    }
}
