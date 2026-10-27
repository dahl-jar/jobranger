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
}
