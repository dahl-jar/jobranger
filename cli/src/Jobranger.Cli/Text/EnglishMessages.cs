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

    public string ConfigNotEvidenceId =>
        "is not an id of 2 to 40 lowercase letters, digits or _ that starts with a letter";

    public string ConfigNotMonth => "is not a month in YYYY-MM form";

    public string ConfigBelowZero => "is below 0";

    public string ConfigUnreadable => "could not be read";

    public string ConfigNotChoice(string allowed) => $"is not one of: {allowed}";

    public string ConfigNotYaml(int line) => $"is not valid YAML (line {line})";

    public string ConfigTooLong(int limit) => $"is longer than {limit} characters";

    public string ConfigAmountOutOfRange(decimal max) =>
        string.Create(CultureInfo.InvariantCulture, $"is not between 0 and {max}");

    public string ConfigRunTimesTooClose(int hours) => $"are less than {hours} hours apart";

    public string UpdateAvailable(string latest, string current, string command) =>
        $"jobranger {latest} is out (you have {current}). Upgrade: {command}";

    public string PairDescription => "Connect the website to this machine.";

    public string PairAlreadyRunning => "Pairing is already running in another terminal.";

    public string PairRelayUnreachable => "Cannot reach the jobranger relay. Check your connection and try again.";

    public string PairRelayRefused => "The relay refused the pairing request. Try again in a minute.";

    public string PairKeysBroken => "The link keys cannot be used. Run jobranger unpair to make new ones.";

    public string PairOpenPage => "Open this page in your browser:";

    public string PairEnterCode => "and enter this code:";

    public string PairBrowserFallback => "a browser";

    public string PairWebsiteWords => "The website shows these words:";

    public string PairWordsMatch => "Do they match? [y/N] ";

    public IReadOnlyList<string> PairYesAnswers { get; } = ["y", "yes"];

    public string PairNotPaired => "Not paired.";

    public string PairFailed => "Pairing failed. Run jobranger pair again.";

    public string PairCodeExpired => "The code expired. Run jobranger pair again.";

    public string PairCodeExpiresIn(int minutes) =>
        $"The code expires in {minutes} {(minutes == 1 ? "minute" : "minutes")}. Waiting for the website...";

    public string PairWantsToPair(string agent) => $"{agent} wants to pair.";

    public string PairBrowserWantsToPair => "A browser wants to pair.";

    public string PairPaired(string agent) => $"\u2713 Paired with {agent}.";

    public string DatabaseTooNew => "The database was made by a newer jobranger. Update jobranger and try again.";

    public string DatabaseUnreadable(string path) => $"Cannot read the jobranger database at {path}.";
}
