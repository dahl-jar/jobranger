using Jobranger.Relay.Core;

namespace Jobranger.Relay.Security;

internal sealed class RefusalSampler(TimeProvider time) {
    private readonly Dictionary<string, DateTimeOffset> lastLogged = new(StringComparer.Ordinal);
    private readonly Lock gate = new();

    public bool ShouldLog(string key) {
        lock (gate) {
            DateTimeOffset now = time.GetUtcNow();
            if (lastLogged.TryGetValue(key, out DateTimeOffset last) && now - last < RelayLimits.RefusalLogInterval) {
                return false;
            }

            lastLogged[key] = now;
            return true;
        }
    }
}
