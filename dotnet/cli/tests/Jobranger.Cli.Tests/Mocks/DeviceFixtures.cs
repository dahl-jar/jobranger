namespace Jobranger.Cli.Tests.Mocks;

internal static class DeviceFixtures {
    public const string BrowserName = "Firefox on macOS";

    public static readonly byte[] BrowserKey = [.. Enumerable.Repeat((byte)1, 32)];
    public static readonly byte[] OtherKey = [.. Enumerable.Repeat((byte)2, 32)];
    public static readonly byte[] SiblingKey = [.. Enumerable.Repeat((byte)1, 31), 9];
    public static readonly DateTimeOffset PairedAt = new(2026, 10, 25, 9, 30, 0, TimeSpan.Zero);
}
