namespace Jobranger.Cli.Text;

internal sealed record SplitUrl(string Scheme, string Netloc, string Path, string Query, string Fragment) {
    private const char userSeparator = '@';
    private const char bracketOpen = '[';
    private const char bracketClose = ']';
    private const char portSeparator = ':';

    public string? Hostname {
        get {
            string hostInfo = Netloc[(Netloc.LastIndexOf(userSeparator) + 1)..];
            int open = hostInfo.IndexOf(bracketOpen);
            string host;
            if (open >= 0) {
                string bracketed = hostInfo[(open + 1)..];
                int close = bracketed.IndexOf(bracketClose);
                host = close < 0 ? bracketed : bracketed[..close];
            } else {
                int port = hostInfo.IndexOf(portSeparator);
                host = port < 0 ? hostInfo : hostInfo[..port];
            }
            return host.Length == 0 ? null : host;
        }
    }
}
