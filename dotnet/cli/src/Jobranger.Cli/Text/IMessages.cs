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

    string RunDescription { get; }

    string RunReading(string sources);

    string RunCounts(int adsRead, int newAds, int matches);

    string RunSentBoth(string email);

    string RunSentEmail(string email);

    string RunSavedForWebsite { get; }

    string RunEmailFailed(string reason);

    string RunLocked { get; }

    string AiChecksSkipped { get; }

    string UnpricedLimitWarning { get; }

    string UnknownEvidenceDropped(int count);

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

    string DevicesDescription { get; }

    string DevicesListDescription { get; }

    string DevicesRevokeDescription { get; }

    string UnpairDescription { get; }

    string DevicesNone { get; }

    string DeviceLine(string id, string name, string pairedOn, string lastSeenOn);

    string DeviceNeverSeen { get; }

    string DevicesRevoked(string name);

    string DevicesNoMatch(string id);

    string DevicesAmbiguous(string id);

    string SecretStoreFailed { get; }

    string Unpaired(int count);

    string UnpairNone { get; }

    string UnpairKeysFailed { get; }

    string UnpairKeysFileFailed(string path, string reason);

    string UnpairKeysWriteFailed(string path, string reason);

    string DatabaseTooNew { get; }

    string DatabaseUnreadable(string path);

    string DirNotWritable(string path);

    string FileUnreadable(string path);

    string ReportDescription { get; }

    string ReportNoMatches { get; }

    string ReportNoDate { get; }

    string LogsDescription { get; }

    string LogsNoRuns { get; }

    string LogsMissing(int keepDays);

    string ConfigMissing { get; }

    string ProfileMissing(string path);

    string FileAccessFailed { get; }

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

    string EmailMissingAddress { get; }

    string EmailMissingSmtpUrl { get; }

    string EmailMissingSmtpPassword { get; }

    string EmailMissingResendKey { get; }

    string EmailUnusableAddress { get; }

    string EmailInvalidSmtpUrl { get; }

    string EmailNonAsciiLogin { get; }

    string EmailNoStarttls(string host, int port);

    string EmailNotSmtp(string host, int port);

    string EmailSmtpLoginFailed(string host, int port);

    string EmailSmtpRecipientRefused(string to);

    string EmailSmtpSenderRefused(string sender);

    string EmailSmtpStatus(int status);

    string EmailSmtpUnsupported(string host, int port);

    string EmailTlsCertificate(string host);

    string EmailTlsFailed(string host, int port);

    string EmailSmtpDisconnected(string host, int port);

    string EmailSmtpReplyTooLong(string host, int port);

    string EmailInvalidHost(string host);

    string EmailSmtpUnreachable(string host, int port);

    string EmailSmtpFailed(string host, int port);

    string EmailStalled(string host, int port);

    string EmailSmtpTooSlow(string host, int port, int seconds);

    string EmailResendKeyRefused { get; }

    string EmailUnusableResendKey { get; }

    string EmailResendRedirect(int status);

    string EmailResendStatus(int status);

    string EmailResendTimeout { get; }

    string EmailResendUnreachable { get; }

    string EmailResendTooSlow(int seconds);

    string EmailSecretStore { get; }
}
