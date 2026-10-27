namespace Jobranger.Cli.Text;

internal sealed class NorwegianMessages : IMessages {
    public string RootDescription =>
        "Finner stillingsannonser på FINN, NAV, LinkedIn og arbeidsgiveres jobbsider som passer profilen din.";

    public string DescriptionHeading => "Beskrivelse:";

    public string UsageHeading => "Bruk:";

    public string OptionsHeading => "Valg:";

    public string OptionsPlaceholder => "valg";

    public string HelpOptionDescription => "Vis hjelp og bruk";

    public string VersionOptionDescription => "Vis versjonen";

    public string InvalidArguments => "Argumentene er ikke gyldige.";

    public string HelpHint(string programName) => $"Kjør '{programName} --help' for bruk.";

    public string UnknownArgument(string token) => $"Ukjent kommando eller argument '{token}'.";

    public string InvalidOption(string option) => $"Ugyldig bruk av '{option}'.";
}
