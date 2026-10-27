namespace Jobranger.Cli.Tests.Mocks;

internal static class ConfigFixtures {
    public const string Home = "/home/user";
    public const string Secret = "pass-1234";

    public static string ConfigTestData(params string[] parts) =>
        Path.Combine([AppContext.BaseDirectory, "TestData", "Config", .. parts]);
}
