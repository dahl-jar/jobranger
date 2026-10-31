using System.Globalization;

namespace Jobranger.Cli.Text;

internal sealed class NorwegianMessages : IMessages {
    private static readonly string[] weekdays = ["søndag", "mandag", "tirsdag", "onsdag", "torsdag", "fredag", "lørdag"];

    private static readonly string[] months = [
        "januar", "februar", "mars", "april", "mai", "juni", "juli", "august", "september", "oktober", "november",
        "desember",
    ];

    private static readonly string[] shortMonths =
        ["jan", "feb", "mar", "apr", "mai", "jun", "jul", "aug", "sep", "okt", "nov", "des"];

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

    public string ConfigNotEvidenceId =>
        "er ikke en id på 2 til 40 små bokstaver, sifre eller _ som starter med en bokstav";

    public string ConfigNotMonth => "er ikke en måned på formen ÅÅÅÅ-MM";

    public string ConfigBelowZero => "er under 0";

    public string ConfigUnreadable => "kunne ikke leses";

    public string ConfigNotDocId =>
        "er ikke en dokument-id: doc_ og så 1 til 36 små bokstaver, sifre eller _";

    public string ConfigReservedId => "er reservert for kontekst-id-er: about_me og doc_-id-er";

    public string ConfigNotDate => "er ikke en dato på formen ÅÅÅÅ-MM-DD";

    public string ConfigNotChoice(string allowed) => $"er ikke en av: {allowed}";

    public string ConfigNotYaml(int line) => $"er ikke gyldig YAML (linje {line})";

    public string ConfigTooLong(int limit) => $"er lengre enn {limit} tegn";

    public string ConfigTooMany(int limit) => $"har mer enn {limit} oppføringer";

    public string ConfigAmountOutOfRange(decimal max) =>
        string.Create(CultureInfo.InvariantCulture, $"er ikke mellom 0 og {max}");

    public string ConfigRunTimesTooClose(int hours) => $"ligger mindre enn {hours} timer fra hverandre";

    public string UpdateAvailable(string latest, string current, string command) =>
        $"jobranger {latest} er ute (du har {current}). Oppgrader: {command}";

    public string PairDescription => "Koble nettsiden til denne maskinen.";

    public string PairAlreadyRunning => "Tilkobling kjører allerede i en annen terminal.";

    public string PairRelayUnreachable => "Får ikke kontakt med mellomtjeneren. Sjekk tilkoblingen og prøv igjen.";

    public string PairRelayRefused => "Mellomtjeneren avviste forespørselen. Prøv igjen om et minutt.";

    public string PairKeysBroken => "Koblingsnøklene kan ikke brukes. Kjør jobranger unpair for å lage nye.";

    public string PairOpenPage => "Åpne denne siden i nettleseren:";

    public string PairEnterCode => "og skriv inn denne koden:";

    public string PairBrowserFallback => "en nettleser";

    public string PairWebsiteWords => "Nettsiden viser disse ordene:";

    public string PairWordsMatch => "Stemmer de? [j/N] ";

    public IReadOnlyList<string> PairYesAnswers { get; } = ["j", "ja", "y", "yes"];

    public string PairNotPaired => "Ikke koblet til.";

    public string PairFailed => "Tilkoblingen feilet. Kjør jobranger pair på nytt.";

    public string PairCodeExpired => "Koden har utløpt. Kjør jobranger pair på nytt.";

    public string PairCodeExpiresIn(int minutes) =>
        $"Koden utløper om {minutes} {(minutes == 1 ? "minutt" : "minutter")}. Venter på nettsiden ...";

    public string PairWantsToPair(string agent) => $"{agent} vil koble seg til.";

    public string PairBrowserWantsToPair => "En nettleser vil koble seg til.";

    public string PairPaired(string agent) => $"\u2713 Koblet til {agent}.";

    public string DatabaseTooNew => "Databasen er laget av en nyere jobranger. Oppdater jobranger og prøv igjen.";

    public string DatabaseUnreadable(string path) => $"Kan ikke lese jobranger-databasen i {path}.";

    public string ReportDescription => "Vis de åpne treffene dine.";

    public string ReportNoMatches => "Ingen treff ennå. Start en kjøring med: jobranger run";

    public string ReportNoDate => "ingen dato";

    public string LogsDescription => "Vis logglinjene fra siste kjøring.";

    public string LogsNoRuns => "Ingen kjøringer ennå. Start en med: jobranger run";

    public string LogsMissing(int keepDays) => $"Ingen logglinjer for siste kjøring. Logger lagres i {keepDays} dager.";

    public string ConfigMissing => "Ingen oppsettsfil ennå. Sett opp med: jobranger init";

    public string ProfileMissing(string path) => $"Ingen profilfil på {path}. Sett opp med: jobranger init";

    public string FileUnreadable(string path) => $"Kan ikke lese {path}.";

    public string FileAccessFailed => "Kan ikke lese en jobranger-fil. Sjekk tilgangene til mappen.";

    public Language Language => Language.Nb;

    public string DigestSubject(int matches, string date) => $"jobranger: {matches} treff, {date}";

    public string DigestLongDate(DateOnly day) =>
        $"{weekdays[(int)day.DayOfWeek]} {day.Day}. {months[day.Month - 1]}";

    public string DigestShortDate(DateOnly day) => $"{day.Day}.\u00A0{shortMonths[day.Month - 1]}";

    public string DigestNoDate => "Ingen dato";

    public string DigestDueNote(int? daysLeft) => daysLeft switch {
        null => "søk tidlig",
        0 => "i dag",
        1 => "i morgen",
        _ => $"om {daysLeft} dager",
    };

    public string DigestStrongLabel => "Passer godt";

    public string DigestPartialLabel => "Passer delvis";

    public string DigestOnRequestLabel => "Vist likevel";

    public string DigestApplySoonTitle => "Søk denne uken";

    public string DigestNewTodayTitle => "Nye i dag";

    public string DigestCarriedTitle => "Venter fortsatt fra før";

    public string DigestStrongFits(int count) => $"{count} {(count == 1 ? "godt treff" : "gode treff")}";

    public string DigestPartialFits(int count) => $"{count} delvis treff";

    public string DigestShownOnRequest(int count) =>
        $"{count} {(count == 1 ? "annonse" : "annonser")} vist likevel";

    public string DigestThisMorning => "i morges";

    public string DigestThisAfternoon => "i ettermiddag";

    public string DigestThisEvening => "i kveld";

    public string DigestLead(string fits, string when, int newAds) =>
        $"{fits} {when}, valgt ut blant {newAds} {(newAds == 1 ? "ny annonse" : "nye annonser")}.";

    public string DigestNoNewMatches(string when, int newAds) =>
        $"Ingen nye treff {when}, blant {newAds} {(newAds == 1 ? "ny annonse" : "nye annonser")}.";

    public string DigestJoin(string items, string last) => $"{items} og {last}";

    public string DigestMoreSources(int count) => $"{count} til";

    public string DigestCareerPage => "Karriereside";

    public string DigestCareerPageOf(string key) => $"karrieresiden til {key}";

    public string DigestCareerPages(int count) => $"{count} {(count == 1 ? "karriereside" : "karrieresider")}";

    public string DigestReadNote(string sources, bool overnight) =>
        $"Leste {sources} {(overnight ? "i natt" : "i dag")}.";

    public string DigestClosedNote(int count) =>
        $"{count} {(count == 1 ? "tidligere stilling" : "tidligere stillinger")} er avsluttet siden forrige kjøring.";

    public string DigestCouldNotRead(string sources) => $"Kunne ikke lese {sources}.";

    public string DigestPausedNote(int pending) =>
        $"AI-sjekkene er satt på pause til neste måned: {pending} {AdsWait(pending)}.";

    public string DigestKeyRefusedNote(int pending) =>
        $"AI-sjekkene stoppet: leverandøren avviste nøkkelen. {pending} {AdsWait(pending)}. "
        + "Sett en ny nøkkel med: jobranger init";

    public string DigestProviderDownNote(int pending) =>
        $"AI-sjekkene stoppet: leverandøren svarte ikke. {pending} {AdsWait(pending)} på neste kjøring.";

    public string DigestMoreOnWebsite => "til på nettsiden";

    public string DigestMoreInReport => "til i jobranger report";

    public string DigestOpenAd => "Åpne annonsen";

    public string DigestMarkApplied => "Marker som søkt";

    public string DigestNotForMe => "Ikke for meg";

    public string DigestOpenApp => "Åpne jobranger";

    public string DigestSettings => "Innstillinger";

    public string DigestUnsubscribe => "Meld av";

    public string DigestViewHidden => "vis";

    public string DigestStopEmails => "Stopp disse e-postene: jobranger config edit";

    public string HiddenLine(int total, string parts) => $"{total} {(total == 1 ? "skjult" : "skjulte")}: {parts}";

    public string HiddenSenior(int count) => $"{count} senior";

    public string HiddenMasters(int count) => $"{count} master";

    public string HiddenRole(int count) => $"{count} {(count == 1 ? "annen rolle" : "andre roller")}";

    public string HiddenPlace(int count) => $"{count} {(count == 1 ? "annet sted" : "andre steder")}";

    public string HiddenOther(int count) => $"{count} {(count == 1 ? "annen grunn" : "andre grunner")}";

    public string HiddenYearsAbove(int count, int cap) => $"{count} krever {cap}+ år";

    public string HiddenYearsUnknown(int count) => $"{count} krever flere år";

    private static string AdsWait(int pending) => pending == 1 ? "annonse venter" : "annonser venter";
}
