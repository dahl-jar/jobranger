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
}
