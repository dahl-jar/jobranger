using System.Threading.RateLimiting;

namespace Jobranger.Relay.Core;

internal sealed class RefusedLease(TimeSpan retryAfter) : RateLimitLease
{
    public override bool IsAcquired => false;

    public override IEnumerable<string> MetadataNames => [MetadataName.RetryAfter.Name];

    public override bool TryGetMetadata(string metadataName, out object? metadata)
    {
        if (metadataName == MetadataName.RetryAfter.Name)
        {
            metadata = retryAfter;
            return true;
        }

        metadata = null;
        return false;
    }
}
