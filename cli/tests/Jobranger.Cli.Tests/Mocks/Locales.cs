namespace Jobranger.Cli.Tests.Mocks;

internal static class Locales {
    public const string English = "en_US.UTF-8";
    public const string Norwegian = "nb_NO.UTF-8";
    public const string LangVariable = "LANG";
    public const string LcAllVariable = "LC_ALL";

    public static Dictionary<string, string> Environment(string lang) => new() { [LangVariable] = lang };
}
