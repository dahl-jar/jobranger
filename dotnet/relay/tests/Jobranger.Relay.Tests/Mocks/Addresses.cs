namespace Jobranger.Relay.Tests.Mocks;

internal static class Addresses {
    private const int addressesPerNetwork = 250;

    private static int next;
    private static int nextIpv4;

    public static string NextBlock() => $"2001:db8:{Interlocked.Increment(ref next):x}";

    public static string Next() => $"{NextBlock()}::1";

    public static string NextIpv4(int secondOctet) {
        int address = Interlocked.Increment(ref nextIpv4);
        return $"100.{secondOctet}.{address / addressesPerNetwork}.{(address % addressesPerNetwork) + 1}";
    }
}
