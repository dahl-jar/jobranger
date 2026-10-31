using System.Globalization;

namespace Jobranger.Cli.Text;

internal sealed class EnglishMessages : IMessages {
    private const string longDateFormat = "dddd d MMMM";
    private const string shortDateFormat = "d MMM";

    private static readonly string[] numberWords =
        ["no", "one", "two", "three", "four", "five", "six", "seven", "eight", "nine", "ten"];

    public string RootDescription =>
        "Finds job ads on FINN, NAV, LinkedIn and employer job boards that fit your profile.";

    public string DescriptionHeading => "Description:";

    public string UsageHeading => "Usage:";

    public string OptionsHeading => "Options:";

    public string OptionsPlaceholder => "options";

    public string ArgumentsHeading => "Arguments:";

    public string CommandsHeading => "Commands:";

    public string CommandPlaceholder => "command";

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

    public string ConfigNotDocId =>
        "is not a document id: doc_ and then 1 to 36 lowercase letters, digits or _";

    public string ConfigReservedId => "is reserved for context ids: about_me and doc_ ids";

    public string ConfigNotDate => "is not a date in YYYY-MM-DD form";

    public string ConfigNotChoice(string allowed) => $"is not one of: {allowed}";

    public string ConfigNotYaml(int line) => $"is not valid YAML (line {line})";

    public string ConfigTooLong(int limit) => $"is longer than {limit} characters";

    public string ConfigTooMany(int limit) => $"has more than {limit} entries";

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

    public string DevicesDescription => "Show or remove paired browsers.";

    public string DevicesListDescription => "List paired browsers.";

    public string DevicesRevokeDescription => "Remove one paired browser.";

    public string DevicesRevokeIdDescription => "The browser's id, or the start of it.";

    public string UnpairDescription => "Remove every paired browser.";

    public string DevicesNone => "No paired browsers. Pair one with: jobranger pair";

    public string DeviceLine(string id, string name, string pairedOn, string lastSeenOn) =>
        $"{id}  {name}  paired {pairedOn}  last seen {lastSeenOn}";

    public string DeviceNeverSeen => "never";

    public string DevicesRevoked(string name) => $"\u2713 Revoked {name}.";

    public string DevicesNoMatch(string id) => $"No browser with id {id}.";

    public string DevicesAmbiguous(string id) =>
        $"More than one browser has an id that starts with {id}. Nothing was revoked. Use more of the id.";

    public string SecretStoreFailed => "The secret store did not answer. Unlock it and try again.";

    public string Unpaired(int count) =>
        $"\u2713 Unpaired {count} {(count == 1 ? "browser" : "browsers")}. "
        + "The website can no longer reach this machine.";

    public string UnpairNone => "No paired browsers.";

    public string UnpairKeysFailed => "New link keys could not be made. Run jobranger unpair again.";

    public string UnpairKeysFileFailed(string path, string reason) =>
        $"New link keys could not be made because jobranger cannot use {path} ({reason}). "
        + "Fix or remove that file, then run jobranger unpair again.";

    public string UnpairKeysWriteFailed(string path, string reason) =>
        $"New link keys could not be made because jobranger cannot change {path} ({reason}). "
        + "Make that file and its folder readable and writable, then run jobranger unpair again.";

    public string DatabaseTooNew => "The database was made by a newer jobranger. Update jobranger and try again.";

    public string DatabaseUnreadable(string path) => $"Cannot read the jobranger database at {path}.";

    public string DirNotWritable(string path) =>
        $"Cannot create or use the folder {path}. Make it and the folders above it writable, then try again.";

    public string FileUnreadable(string path) => $"Cannot read {path}.";

    public string ReportDescription => "Show your open matches.";

    public string ReportNoMatches => "No matches yet. Start a run with: jobranger run";

    public string ReportNoDate => "no date";

    public string LogsDescription => "Show the log lines from the last run.";

    public string LogsNoRuns => "No runs yet. Run one with: jobranger run";

    public string LogsMissing(int keepDays) => $"No log lines for the last run. Logs are kept for {keepDays} days.";

    public string ConfigMissing => "No config yet. Set up with: jobranger init";

    public string ProfileMissing(string path) => $"No profile file at {path}. Set up with: jobranger init";

    public string FileAccessFailed => "Cannot read a jobranger file. Check the folder permissions.";

    public Language Language => Language.En;

    public string DigestSubject(int matches, string date) =>
        $"jobranger: {matches} {(matches == 1 ? "match" : "matches")}, {date}";

    public string DigestLongDate(DateOnly day) => day.ToString(longDateFormat, CultureInfo.InvariantCulture);

    public string DigestShortDate(DateOnly day) => day.ToString(shortDateFormat, CultureInfo.InvariantCulture);

    public string DigestNoDate => "No date";

    public string DigestDueNote(int? daysLeft) => daysLeft switch {
        null => "apply early",
        0 => "today",
        1 => "tomorrow",
        _ => $"in {daysLeft} days",
    };

    public string DigestStrongLabel => "Strong fit";

    public string DigestPartialLabel => "Partial fit";

    public string DigestOnRequestLabel => "Shown on request";

    public string DigestApplySoonTitle => "Apply this week";

    public string DigestNewTodayTitle => "New today";

    public string DigestCarriedTitle => "Still waiting from earlier";

    public string DigestStrongFits(int count) => $"{Number(count)} strong {(count == 1 ? "fit" : "fits")}";

    public string DigestPartialFits(int count) => $"{Number(count)} partial {(count == 1 ? "fit" : "fits")}";

    public string DigestShownOnRequest(int count) => $"{Number(count)} {(count == 1 ? "ad" : "ads")} shown on request";

    public string DigestThisMorning => "this morning";

    public string DigestThisAfternoon => "this afternoon";

    public string DigestThisEvening => "this evening";

    public string DigestLead(string fits, string when, int newAds) =>
        $"{Capitalized(fits)} {when}, picked from {newAds} {(newAds == 1 ? "new ad" : "new ads")}.";

    public string DigestNoNewMatches(string when, int newAds) =>
        $"No new matches {when}, from {newAds} {(newAds == 1 ? "new ad" : "new ads")}.";

    public string DigestJoin(string items, string last) => $"{items} and {last}";

    public string DigestMoreSources(int count) => $"{count} more";

    public string DigestCareerPage => "Career page";

    public string DigestCareerPageOf(string key) => $"the {key} career page";

    public string DigestCareerPages(int count) =>
        $"{count} {(count == 1 ? "company career page" : "company career pages")}";

    public string DigestReadNote(string sources, bool overnight) =>
        $"Read {sources} {(overnight ? "overnight" : "today")}.";

    public string DigestClosedNote(int count) =>
        $"{Capitalized(Number(count))} {(count == 1 ? "earlier job" : "earlier jobs")} closed since the last run.";

    public string DigestCouldNotRead(string sources) => $"Could not read {sources}.";

    public string DigestPausedNote(int pending) =>
        $"AI checks are paused until next month: {pending} {AdsWait(pending)}.";

    public string DigestKeyRefusedNote(int pending) =>
        $"AI checks stopped: the provider refused the API key. {pending} {AdsWait(pending)}. "
        + "Set a new key with: jobranger init";

    public string DigestProviderDownNote(int pending) =>
        $"AI checks stopped: the provider did not answer. {pending} {AdsWait(pending)} for the next run.";

    public string DigestMoreOnWebsite => "more on the website";

    public string DigestMoreInReport => "more in jobranger report";

    public string DigestOpenAd => "Open the ad";

    public string DigestMarkApplied => "Mark applied";

    public string DigestNotForMe => "Not for me";

    public string DigestOpenApp => "Open jobranger";

    public string DigestSettings => "Settings";

    public string DigestUnsubscribe => "Unsubscribe";

    public string DigestViewHidden => "view";

    public string DigestStopEmails => "Stop these emails: jobranger config edit";

    public string HiddenLine(int total, string parts) => $"{total} hidden: {parts}";

    public string HiddenSenior(int count) => $"{count} senior";

    public string HiddenMasters(int count) => $"{count} master's";

    public string HiddenRole(int count) => $"{count} {(count == 1 ? "other role" : "other roles")}";

    public string HiddenPlace(int count) => $"{count} {(count == 1 ? "other place" : "other places")}";

    public string HiddenOther(int count) => $"{count} {(count == 1 ? "other reason" : "other reasons")}";

    public string HiddenYearsAbove(int count, int cap) => $"{count} need {cap}+ yrs";

    public string HiddenYearsUnknown(int count) => $"{count} need more yrs";

    private static string Number(int count) => count < numberWords.Length ? numberWords[count] : $"{count}";

    private static string Capitalized(string text) =>
        text.Length == 0 ? text : char.ToUpperInvariant(text[0]) + text[1..];

    private static string AdsWait(int pending) => pending == 1 ? "ad waits" : "ads wait";
}
