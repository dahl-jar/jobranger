namespace Jobranger.Cli.Tests.Mocks;

internal static class Locales {
    public const string English = "en_US.UTF-8";
    public const string Norwegian = "nb_NO.UTF-8";
    public const string LangVariable = "LANG";
    public const string LcAllVariable = "LC_ALL";

    public const string ConfigDirVariable = "JOBRANGER_CONFIG_DIR";

    private static readonly string absentConfigDir =
        Path.Combine(Path.GetTempPath(), "jobranger-absent-" + Guid.NewGuid().ToString("N"));

    public static Dictionary<string, string> EnvironmentWithoutConfig(string lang) =>
        new() { [LangVariable] = lang, [ConfigDirVariable] = absentConfigDir };
}
