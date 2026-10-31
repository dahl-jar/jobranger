namespace Jobranger.Relay.Hubs;

internal sealed class HubSocketGate(int capacity, int perNetwork) {
    private readonly Dictionary<string, int> byNetwork = new(StringComparer.Ordinal);
    private readonly Lock gate = new();
    private int open;

    public bool TryEnter(string network) {
        lock (gate) {
            int held = byNetwork.GetValueOrDefault(network);
            if (open >= capacity || held >= perNetwork) {
                return false;
            }

            open++;
            byNetwork[network] = held + 1;
            return true;
        }
    }

    public void Exit(string network) {
        lock (gate) {
            open--;
            int held = byNetwork.GetValueOrDefault(network) - 1;
            if (held > 0) {
                byNetwork[network] = held;
            } else {
                byNetwork.Remove(network);
            }
        }
    }
}
