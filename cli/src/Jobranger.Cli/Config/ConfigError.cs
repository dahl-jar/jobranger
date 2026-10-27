using System.Globalization;
using Jobranger.Cli.Text;

namespace Jobranger.Cli.Config;

internal sealed record ConfigError(string Location, ConfigProblem Problem, string Detail = "") {
    private const string separator = ": ";

    public string Describe(IMessages messages) => Location + separator + Text(messages);

    private string Text(IMessages messages) => Problem switch {
        ConfigProblem.UnknownKey => messages.ConfigUnknownKey,
        ConfigProblem.Required => messages.ConfigRequired,
        ConfigProblem.NotBoolean => messages.ConfigNotBoolean,
        ConfigProblem.NotNumber => messages.ConfigNotNumber,
        ConfigProblem.NotText => messages.ConfigNotText,
        ConfigProblem.NotList => messages.ConfigNotList,
        ConfigProblem.NotSection => messages.ConfigNotSection,
        ConfigProblem.NotChoice => messages.ConfigNotChoice(Detail),
        ConfigProblem.AmountOutOfRange =>
            messages.ConfigAmountOutOfRange(decimal.Parse(Detail, CultureInfo.InvariantCulture)),
        ConfigProblem.RunTimeFormat => messages.ConfigRunTimeFormat,
        ConfigProblem.RunTimesTooClose =>
            messages.ConfigRunTimesTooClose(int.Parse(Detail, CultureInfo.InvariantCulture)),
        ConfigProblem.NotOllamaUrl => messages.ConfigNotOllamaUrl,
        ConfigProblem.NotSmtpUrl => messages.ConfigNotSmtpUrl,
        ConfigProblem.NotAddress => messages.ConfigNotAddress,
        ConfigProblem.NotSender => messages.ConfigNotSender,
        ConfigProblem.AddressMissing => messages.ConfigAddressMissing,
        ConfigProblem.SmtpUrlMissing => messages.ConfigSmtpUrlMissing,
        ConfigProblem.AliasUsed => messages.ConfigAliasUsed,
        ConfigProblem.DuplicateKey => messages.ConfigDuplicateKey,
        ConfigProblem.NotYaml => messages.ConfigNotYaml(int.Parse(Detail, CultureInfo.InvariantCulture)),
        ConfigProblem.NotUtf8 => messages.ConfigNotUtf8,
        ConfigProblem.NotServiceUrl => messages.ConfigNotServiceUrl,
        ConfigProblem.TooLong => messages.ConfigTooLong(int.Parse(Detail, CultureInfo.InvariantCulture)),
        ConfigProblem.NotEvidenceId => messages.ConfigNotEvidenceId,
        ConfigProblem.NotMonth => messages.ConfigNotMonth,
        ConfigProblem.BelowZero => messages.ConfigBelowZero,
        ConfigProblem.Unreadable => messages.ConfigUnreadable,
        _ => throw new InvalidOperationException($"config problem has no text problem={Problem}"),
    };
}
