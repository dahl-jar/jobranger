using Jobranger.Cli.Config;

namespace Jobranger.Cli.Features.Setup;

internal static class SmtpDefaults {
    private const int implicitTlsPort = 465;
    private const char at = '@';
    private const string gmailServer = "smtp.gmail.com:465";

    private static readonly string[] gmailDomains = ["gmail.com", "googlemail.com"];

    public static string? Url(string server) {
        if (SmtpServer.Parse($"smtp://{server}") is not SmtpServer parsed) {
            return null;
        }
        return parsed.Port == implicitTlsPort ? $"smtps://{server}" : $"smtp://{server}";
    }

    public static string Server(string address) {
        string domain = address[(address.LastIndexOf(at) + 1)..];
        return gmailDomains.Contains(domain, StringComparer.OrdinalIgnoreCase) ? gmailServer : string.Empty;
    }
}
