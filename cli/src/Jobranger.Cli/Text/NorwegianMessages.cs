using System.Globalization;

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

    public string ConfigUnknownKey => "er ikke en kjent innstilling";

    public string ConfigRequired => "må fylles ut";

    public string ConfigNotBoolean => "er ikke true eller false";

    public string ConfigNotNumber => "er ikke et tall";

    public string ConfigNotText => "er ikke tekst";

    public string ConfigNotList => "er ikke en liste";

    public string ConfigNotSection => "er ikke en gruppe med innstillinger";

    public string ConfigRunTimeFormat => "er ikke et klokkeslett på formen TT:MM";

    public string ConfigNotOllamaUrl => "er ikke en http- eller https-adresse med vertsnavn";

    public string ConfigNotSmtpUrl =>
        "er ikke smtp://vert[:port] eller smtps://vert[:port] uten bruker, sti eller spørring";

    public string ConfigNotAddress => "er ikke én e-postadresse";

    public string ConfigNotSender => "er ikke én avsenderadresse";

    public string ConfigAddressMissing => "e-post er slått på, men ingen e-postadresse er satt";

    public string ConfigSmtpUrlMissing =>
        "e-post går via SMTP, men ingen SMTP-server (smtp_url) er satt";

    public string ConfigAliasUsed => "bruker et alias (*navn), som ikke er tillatt i oppsettsfilen";

    public string ConfigDuplicateKey => "står mer enn én gang";

    public string ConfigNotUtf8 => "er ikke UTF-8-tekst";

    public string ConfigNotServiceUrl =>
        "er ikke en http- eller https-adresse med vertsnavn og uten bruker, spørring eller fragment";

    public string ConfigNotChoice(string allowed) => $"er ikke en av: {allowed}";

    public string ConfigNotYaml(int line) => $"er ikke gyldig YAML (linje {line})";

    public string ConfigAmountOutOfRange(decimal max) =>
        string.Create(CultureInfo.InvariantCulture, $"er ikke mellom 0 og {max}");

    public string ConfigRunTimesTooClose(int hours) => $"ligger mindre enn {hours} timer fra hverandre";

    public string UpdateAvailable(string latest, string current, string command) =>
        $"jobranger {latest} er ute (du har {current}). Oppgrader: {command}";
}
