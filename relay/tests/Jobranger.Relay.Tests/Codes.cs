namespace Jobranger.Relay.Tests;

internal static class Codes {
    private const string filler = "BBBB";
    private const string otherFiller = "CCCC";

    public static string WithWrongVerifier(string code) => $"{code[..4]}-{Other(code[5..])}";

    public static string WithUnknownSelector(string code) => $"{Other(code[..4])}-{code[5..]}";

    private static string Other(string part) => part == filler ? otherFiller : filler;
}
