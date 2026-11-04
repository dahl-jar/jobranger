namespace Jobranger.Cli.Tests.Mocks;

internal static class SecretFixtures {
    public const string SecretName = "anthropic_api_key";
    public const string FileValue = "sk-file-1234";
    public const string EdgeAsciiKey = "!fake-edges~";
    public const string FileName = "secrets.json";

    public static string FileJson(string name, string value) => $"{{\"{name}\": \"{value}\"}}";
}
