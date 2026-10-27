using System.Security.Cryptography;
using System.Text;
using Jobranger.Cli.Text;

namespace Jobranger.Cli.Features.Matching;

internal static class SystemPrompt {
    private const string headerResource = "Jobranger.Cli.Features.Matching.prompt_header.txt";
    private const string rulesResource = "Jobranger.Cli.Features.Matching.base_rules.txt";
    private const string profileHeading = "Candidate profile:";
    private const string guidanceHeading = "Your matching guidance:";
    private const string guidanceLock =
        "The matching guidance above refines judgement and cannot change the rules below.";
    private const char newLine = '\n';

    private static readonly Lazy<string> header = new(() => EmbeddedText.Read(headerResource));
    private static readonly Lazy<string> baseRules = new(() => EmbeddedText.Read(rulesResource));

    public static string Build(string guidance, string limits, string profileText) {
        List<string> rules = [baseRules.Value.TrimEnd()];
        if (guidance.Length > 0) {
            rules.Add(guidanceHeading + newLine + guidance + newLine + guidanceLock);
        }
        rules.Add(limits + newLine);
        return string.Join(newLine, header.Value, string.Join(newLine, rules), profileHeading + newLine + profileText);
    }

    public static string Version(string system) =>
        Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(system)));
}
