namespace Jobranger.Relay.Telemetry;

internal static class RequestLogFloor {
    private const string aspNetCoreCategory = "Microsoft.AspNetCore";
    private const LogLevel floor = LogLevel.Warning;

    public static ILoggingBuilder AddRequestLogFloor(this ILoggingBuilder logging) {
        logging.Services.PostConfigure<LoggerFilterOptions>(Raise);
        return logging;
    }

    private static void Raise(LoggerFilterOptions filters) {
        List<LoggerFilterRule> rules = [.. filters.Rules.Select(Raised)];
        IEnumerable<string?> providers = rules.Select(rule => rule.ProviderName).Append(null).Distinct();
        filters.Rules.Clear();
        foreach (LoggerFilterRule rule in rules) {
            filters.Rules.Add(rule);
        }

        foreach (string? provider in providers) {
            filters.Rules.Add(new LoggerFilterRule(provider, aspNetCoreCategory, floor, null));
        }
    }

    private static LoggerFilterRule Raised(LoggerFilterRule rule) =>
        IsAspNetCore(rule) && rule.LogLevel is not >= floor
            ? new LoggerFilterRule(rule.ProviderName, rule.CategoryName, floor, rule.Filter)
            : rule;

    private static bool IsAspNetCore(LoggerFilterRule rule) =>
        rule.CategoryName?.StartsWith(aspNetCoreCategory, StringComparison.Ordinal) == true;
}
