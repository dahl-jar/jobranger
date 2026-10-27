using System.Globalization;

namespace Jobranger.Cli.Text;

internal sealed class EnglishMessages : IMessages {
    public string RootDescription =>
        "Finds job ads on FINN, NAV, LinkedIn and employer job boards that fit your profile.";

    public string DescriptionHeading => "Description:";

    public string UsageHeading => "Usage:";

    public string OptionsHeading => "Options:";

    public string OptionsPlaceholder => "options";

    public string HelpOptionDescription => "Show help and usage";

    public string VersionOptionDescription => "Show the version";

    public string InvalidArguments => "The arguments are not valid.";

    public string HelpHint(string programName) => $"Run '{programName} --help' for usage.";

    public string UnknownArgument(string token) => $"Unknown command or argument '{token}'.";

    public string InvalidOption(string option) => $"Invalid use of '{option}'.";

    public string ConfigUnknownKey => "is not a known setting";

    public string ConfigRequired => "is required";

    public string ConfigNotBoolean => "is not true or false";

    public string ConfigNotNumber => "is not a number";

    public string ConfigNotText => "is not text";

    public string ConfigNotList => "is not a list";

    public string ConfigNotSection => "is not a section of settings";

    public string ConfigRunTimeFormat => "is not a time in HH:MM form";

    public string ConfigNotOllamaUrl => "is not an http or https URL with a host";

    public string ConfigNotSmtpUrl =>
        "is not smtp://host[:port] or smtps://host[:port] without a user, path or query";

    public string ConfigNotAddress => "is not one email address";

    public string ConfigNotSender => "is not one sender address";

    public string ConfigAddressMissing => "email is on but no email address is set";

    public string ConfigSmtpUrlMissing =>
        "email goes through SMTP but no SMTP server (smtp_url) is set";

    public string ConfigAliasUsed => "uses an alias (*name), which the config file does not allow";

    public string ConfigDuplicateKey => "appears more than once";

    public string ConfigNotUtf8 => "is not UTF-8 text";

    public string ConfigNotServiceUrl =>
        "is not an http or https URL with a host and no user, query or fragment";

    public string ConfigNotChoice(string allowed) => $"is not one of: {allowed}";

    public string ConfigNotYaml(int line) => $"is not valid YAML (line {line})";

    public string ConfigAmountOutOfRange(decimal max) =>
        string.Create(CultureInfo.InvariantCulture, $"is not between 0 and {max}");

    public string ConfigRunTimesTooClose(int hours) => $"are less than {hours} hours apart";
}
