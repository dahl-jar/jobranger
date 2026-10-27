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
}
