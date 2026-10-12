namespace Jobranger.Relay.Core;

public sealed class HandleKeys
{
    public HandleKeys(byte[] current, byte[]? previous)
    {
        Current = RequireKey(current, nameof(current));
        Previous = previous is null ? null : RequireKey(previous, nameof(previous));
    }

    internal byte[] Current { get; }

    internal byte[]? Previous { get; }

    private static byte[] RequireKey(byte[] key, string name)
    {
        ArgumentNullException.ThrowIfNull(key, name);
        ArgumentOutOfRangeException.ThrowIfLessThan(key.Length, RelayLimits.MinHandleKeyBytes, name);
        return [.. key];
    }
}
