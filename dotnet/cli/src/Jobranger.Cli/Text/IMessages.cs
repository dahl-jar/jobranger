namespace Jobranger.Cli.Text;

internal interface IMessages {
    string RootDescription { get; }

    string DescriptionHeading { get; }

    string UsageHeading { get; }

    string OptionsHeading { get; }

    string OptionsPlaceholder { get; }

    string HelpOptionDescription { get; }

    string VersionOptionDescription { get; }

    string InvalidArguments { get; }

    string HelpHint(string programName);

    string UnknownArgument(string token);

    string InvalidOption(string option);

    string ConfigUnknownKey { get; }

    string ConfigRequired { get; }

    string ConfigNotBoolean { get; }

    string ConfigNotNumber { get; }

    string ConfigNotText { get; }

    string ConfigNotList { get; }

    string ConfigNotSection { get; }

    string ConfigRunTimeFormat { get; }

    string ConfigNotOllamaUrl { get; }

    string ConfigNotSmtpUrl { get; }

    string ConfigNotAddress { get; }

    string ConfigNotSender { get; }

    string ConfigAddressMissing { get; }

    string ConfigSmtpUrlMissing { get; }

    string ConfigAliasUsed { get; }

    string ConfigDuplicateKey { get; }

    string ConfigNotUtf8 { get; }

    string ConfigNotServiceUrl { get; }

    string ConfigNotEvidenceId { get; }

    string ConfigNotMonth { get; }

    string ConfigBelowZero { get; }

    string ConfigUnreadable { get; }

    string ConfigNotDocId { get; }

    string ConfigReservedId { get; }

    string ConfigNotDate { get; }

    string ConfigNotChoice(string allowed);

    string ConfigAmountOutOfRange(decimal max);

    string ConfigRunTimesTooClose(int hours);

    string ConfigNotYaml(int line);

    string ConfigTooLong(int limit);

    string ConfigTooMany(int limit);

    string UpdateAvailable(string latest, string current, string command);

    string PairDescription { get; }

    string PairAlreadyRunning { get; }

    string PairRelayUnreachable { get; }

    string PairRelayRefused { get; }

    string PairKeysBroken { get; }

    string PairOpenPage { get; }

    string PairEnterCode { get; }

    string PairBrowserFallback { get; }

    string PairWebsiteWords { get; }

    string PairWordsMatch { get; }

    IReadOnlyList<string> PairYesAnswers { get; }

    string PairNotPaired { get; }

    string PairFailed { get; }

    string PairCodeExpired { get; }

    string PairCodeExpiresIn(int minutes);

    string PairWantsToPair(string agent);

    string PairBrowserWantsToPair { get; }

    string PairPaired(string agent);

    string DatabaseTooNew { get; }

    string DatabaseUnreadable(string path);

    string ReportDescription { get; }

    string ReportNoMatches { get; }

    string ReportNoDate { get; }

    string LogsDescription { get; }

    string LogsNoRuns { get; }

    string LogsMissing(int keepDays);

    string ConfigMissing { get; }

    string FileUnreadable(string path);

    Language Language { get; }

    string DigestSubject(int matches, string date);

    string DigestLongDate(DateOnly day);

    string DigestShortDate(DateOnly day);

    string DigestNoDate { get; }

    string DigestDueNote(int? daysLeft);

    string DigestStrongLabel { get; }

    string DigestPartialLabel { get; }

    string DigestOnRequestLabel { get; }

    string DigestApplySoonTitle { get; }

    string DigestNewTodayTitle { get; }

    string DigestCarriedTitle { get; }

    string DigestStrongFits(int count);

    string DigestPartialFits(int count);

    string DigestShownOnRequest(int count);

    string DigestThisMorning { get; }

    string DigestThisAfternoon { get; }

    string DigestThisEvening { get; }

    string DigestLead(string fits, string when, int newAds);

    string DigestNoNewMatches(string when, int newAds);

    string DigestJoin(string items, string last);

    string DigestMoreSources(int count);

    string DigestCareerPage { get; }

    string DigestCareerPageOf(string key);

    string DigestCareerPages(int count);

    string DigestReadNote(string sources, bool overnight);

    string DigestClosedNote(int count);

    string DigestCouldNotRead(string sources);

    string DigestPausedNote(int pending);

    string DigestKeyRefusedNote(int pending);

    string DigestProviderDownNote(int pending);

    string DigestMoreOnWebsite { get; }

    string DigestMoreInReport { get; }

    string DigestOpenAd { get; }

    string DigestMarkApplied { get; }

    string DigestNotForMe { get; }

    string DigestOpenApp { get; }

    string DigestSettings { get; }

    string DigestUnsubscribe { get; }

    string DigestViewHidden { get; }

    string DigestStopEmails { get; }

    string HiddenLine(int total, string parts);

    string HiddenSenior(int count);

    string HiddenMasters(int count);

    string HiddenRole(int count);

    string HiddenPlace(int count);

    string HiddenOther(int count);

    string HiddenYearsAbove(int count, int cap);

    string HiddenYearsUnknown(int count);
}
