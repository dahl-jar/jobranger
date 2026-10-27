using System.Globalization;
using System.Text.RegularExpressions;

namespace Jobranger.Cli.Config;

internal static partial class AppConfigValidator {
    public const decimal MaxAmountUsd = 1_000_000m;

    private const int minutesPerHour = 60;
    private const int minutesPerDay = 24 * minutesPerHour;
    private const int minRunGapHours = 2;
    private const int minRunGapMinutes = minRunGapHours * minutesPerHour;
    private const string hourGroup = "hour";
    private const string minuteGroup = "minute";

    private static readonly string[] ollamaSchemes = [Uri.UriSchemeHttp, Uri.UriSchemeHttps];
    private static readonly string timesLocation = ConfigLocation.Join(ConfigKeys.Schedule, ConfigKeys.Times);
    private static readonly string emailLocation = ConfigLocation.Join(ConfigKeys.Notify, ConfigKeys.Email);
    private static readonly string priceLocation = ConfigLocation.Join(ConfigKeys.Model, ConfigKeys.Price);
    private static readonly string monthlyLimitLocation =
        ConfigLocation.Join(ConfigKeys.Model, ConfigKeys.MonthlyLimitUsd);
    private static readonly string inputPriceLocation = ConfigLocation.Join(priceLocation, ConfigKeys.InputPerMtok);
    private static readonly string outputPriceLocation = ConfigLocation.Join(priceLocation, ConfigKeys.OutputPerMtok);
    private static readonly string ollamaUrlLocation = ConfigLocation.Join(ConfigKeys.Model, ConfigKeys.OllamaUrl);
    private static readonly string addressLocation = ConfigLocation.Join(emailLocation, ConfigKeys.Address);
    private static readonly string senderLocation = ConfigLocation.Join(emailLocation, ConfigKeys.Sender);
    private static readonly string smtpUrlLocation = ConfigLocation.Join(emailLocation, ConfigKeys.SmtpUrl);

    public static IReadOnlyList<ConfigError> Validate(AppConfig config) {
        List<ConfigError> errors = [];
        CheckTimes(config.Schedule.Times, errors);
        CheckAmount(config.Model.MonthlyLimitUsd, monthlyLimitLocation, errors);
        CheckAmount(config.Model.Price?.InputPerMtok, inputPriceLocation, errors);
        CheckAmount(config.Model.Price?.OutputPerMtok, outputPriceLocation, errors);
        if (!IsOllamaUrl(config.Model.OllamaUrl)) {
            errors.Add(new ConfigError(ollamaUrlLocation, ConfigProblem.NotOllamaUrl));
        }
        CheckEmailValues(config.Notify.Email, errors);
        CheckEmailRequired(config.Notify.Email, errors);
        return errors;
    }

    private static bool IsOllamaUrl(string url) =>
        Uri.TryCreate(url, UriKind.Absolute, out Uri? parsed)
        && ollamaSchemes.Contains(parsed.Scheme);

    private static void CheckTimes(IReadOnlyList<string> times, List<ConfigError> errors) {
        List<int> minutes = [];
        for (int index = 0; index < times.Count; index++) {
            Match match = RunTime().Match(times[index]);
            if (!match.Success) {
                errors.Add(new ConfigError(ConfigLocation.Item(timesLocation, index), ConfigProblem.RunTimeFormat));
                continue;
            }
            minutes.Add((int.Parse(match.Groups[hourGroup].Value, CultureInfo.InvariantCulture) * minutesPerHour)
                + int.Parse(match.Groups[minuteGroup].Value, CultureInfo.InvariantCulture));
        }
        minutes.Sort();
        List<int> gaps = [.. minutes.Zip(minutes.Skip(1), (earlier, later) => later - earlier)];
        if (minutes.Count > 1) {
            gaps.Add(minutes[0] + minutesPerDay - minutes[^1]);
        }
        if (gaps.Any(gap => gap < minRunGapMinutes)) {
            errors.Add(new ConfigError(
                timesLocation, ConfigProblem.RunTimesTooClose, minRunGapHours.ToString(CultureInfo.InvariantCulture)));
        }
    }

    private static void CheckAmount(decimal? amount, string location, List<ConfigError> errors) {
        if (amount is decimal value && (value < 0 || value > MaxAmountUsd)) {
            errors.Add(new ConfigError(
                location, ConfigProblem.AmountOutOfRange, MaxAmountUsd.ToString(CultureInfo.InvariantCulture)));
        }
    }

    private static void CheckEmailValues(EmailConfig email, List<ConfigError> errors) {
        if (email.Address is string address && !MailAddresses.IsAddress(address)) {
            errors.Add(new ConfigError(addressLocation, ConfigProblem.NotAddress));
        }
        if (email.Sender is string sender && !MailAddresses.IsMailbox(sender)) {
            errors.Add(new ConfigError(senderLocation, ConfigProblem.NotSender));
        }
        if (email.SmtpUrl is string url && SmtpServer.Parse(url) is null) {
            errors.Add(new ConfigError(smtpUrlLocation, ConfigProblem.NotSmtpUrl));
        }
    }

    private static void CheckEmailRequired(EmailConfig email, List<ConfigError> errors) {
        if (!email.On) {
            return;
        }
        if (email.Address is null) {
            errors.Add(new ConfigError(emailLocation, ConfigProblem.AddressMissing));
        }
        if (email.Via == EmailVia.Smtp && email.SmtpUrl is null) {
            errors.Add(new ConfigError(emailLocation, ConfigProblem.SmtpUrlMissing));
        }
    }

    [GeneratedRegex(@"^(?<hour>[01][0-9]|2[0-3]):(?<minute>[0-5][0-9])\z", RegexOptions.CultureInvariant)]
    private static partial Regex RunTime();
}
