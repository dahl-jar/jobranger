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

    private static readonly Dictionary<string, string> initRoleLabels = new() {
        ["mobile"] = "Mobil",
        ["devops_platform"] = "DevOps og plattform",
        ["ml_ai"] = "ML og AI",
        ["embedded"] = "Innebygd",
        ["game"] = "Spill",
    };

    private static readonly Dictionary<string, string> initLevelLabels = new() {
        ["junior"] = "Junior",
        ["mid"] = "Middels",
        ["senior"] = "Senior",
        ["lead"] = "Tech lead",
        ["none"] = "Nivå ikke oppgitt",
    };

    private static readonly Dictionary<string, string> initYearsLabels = new() {
        ["0-1"] = "0–1 år",
        ["1-3"] = "1–3 år",
        ["3-6"] = "3–6 år",
        ["6+"] = "6+ år",
    };

    public string RootDescription =>
        "Finner stillingsannonser på FINN, NAV, LinkedIn og arbeidsgiveres jobbsider som passer profilen din.";

    public string DescriptionHeading => "Beskrivelse:";

    public string UsageHeading => "Bruk:";

    public string OptionsHeading => "Valg:";

    public string OptionsPlaceholder => "valg";

    public string ArgumentsHeading => "Argumenter:";

    public string CommandsHeading => "Kommandoer:";

    public string CommandPlaceholder => "kommando";

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

    public string RunDescription => "Les jobbkildene, sjekk nye annonser og send resultatene.";

    public string RunReading(string sources) => $"Leser {sources}...";

    public string RunCounts(int adsRead, int newAds, int matches) =>
        $"{adsRead} {(adsRead == 1 ? "annonse" : "annonser")} lest, {newAds} {(newAds == 1 ? "ny" : "nye")}, {matches} treff.";

    public string RunSentBoth(string email) => $"\u2713 Sendt til nettsiden og til {email}";

    public string RunSentEmail(string email) => $"\u2713 Sendt til {email}";

    public string RunSavedForWebsite => "\u2713 Lagret for nettsiden";

    public string RunEmailFailed(string reason) => $"\u2715 E-post ikke sendt: {reason}";

    public string RunLocked => "En annen kjøring pågår allerede.";

    public string AiChecksSkipped => "AI-sjekkene ble hoppet over: ingen modell eller nøkkel. Sett en med: jobranger init";

    public string UnpricedLimitWarning =>
        "Modellen din har ingen pris, så månedsgrensen kan ikke sjekkes. "
        + "AI-sjekkene står på pause til du setter en pris på modellen eller fjerner grensen.";

    public string UnknownEvidenceDropped(int count) =>
        $"{count} {(count == 1 ? "annonse" : "annonser")} ble forkastet fordi AI-en viste til noe som ikke står i profilen din. "
        + "Sjekk veiledningen din.";

    public string PairDescription => "Koble nettsiden til denne maskinen.";

    public string PairAlreadyRunning => "Tilkobling kjører allerede i en annen terminal.";

    public string PairRelayUnreachable => "Får ikke kontakt med mellomtjeneren. Sjekk tilkoblingen og prøv igjen.";

    public string PairRelayRefused => "Mellomtjeneren avviste forespørselen. Prøv igjen om et minutt.";

    public string PairKeysBroken => "Koblingsnøklene kan ikke brukes. Kjør jobranger unpair for å lage nye.";

    public string PairKeysBlocked(string path, string reason) =>
        $"Tilkoblingen kan ikke starte fordi jobranger ikke kan åpne eller endre {path} ({reason}). "
        + "Gjør filen og mappen lesbare og skrivbare, og kjør jobranger pair på nytt.";

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

    public string DevicesDescription => "Vis eller fjern tilkoblede nettlesere.";

    public string DevicesListDescription => "Vis tilkoblede nettlesere.";

    public string DevicesRevokeDescription => "Fjern én tilkoblet nettleser.";

    public string DevicesRevokeIdDescription => "Nettleserens id, eller starten av den.";

    public string UnpairDescription => "Fjern alle tilkoblede nettlesere.";

    public string DevicesNone => "Ingen tilkoblede nettlesere. Koble til en med: jobranger pair";

    public string DeviceLine(string id, string name, string pairedOn, string lastSeenOn) =>
        $"{id}  {name}  koblet til {pairedOn}  sist sett {lastSeenOn}";

    public string DeviceNeverSeen => "aldri";

    public string DevicesRevoked(string name) => $"\u2713 Fjernet {name}.";

    public string DevicesNoMatch(string id) => $"Ingen nettleser med id {id}.";

    public string DevicesAmbiguous(string id) =>
        $"Flere nettlesere har en id som starter med {id}. Ingenting ble fjernet. Bruk mer av id-en.";

    public string SecretStoreFailed => "Hemmelighetslageret svarte ikke. Lås det opp og prøv igjen.";

    public string Unpaired(int count) =>
        $"\u2713 Koblet fra {count} {(count == 1 ? "nettleser" : "nettlesere")}. "
        + "Nettsiden kan ikke lenger nå denne maskinen.";

    public string UnpairNone => "Ingen tilkoblede nettlesere.";

    public string UnpairKeysFailed => "Nye koblingsnøkler kunne ikke lages. Kjør jobranger unpair på nytt.";

    public string UnpairKeysFileFailed(string path, string reason) =>
        $"Nye koblingsnøkler kunne ikke lages fordi jobranger ikke kan bruke {path} ({reason}). "
        + "Rett eller fjern filen, og kjør jobranger unpair på nytt.";

    public string UnpairKeysWriteFailed(string path, string reason) =>
        $"Nye koblingsnøkler kunne ikke lages fordi jobranger ikke kan endre {path} ({reason}). "
        + "Gjør filen og mappen lesbare og skrivbare, og kjør jobranger unpair på nytt.";

    public string DatabaseTooNew => "Databasen er laget av en nyere jobranger. Oppdater jobranger og prøv igjen.";

    public string DatabaseUnreadable(string path) => $"Kan ikke lese jobranger-databasen i {path}.";

    public string DirNotWritable(string path) =>
        $"Kan ikke lage eller bruke mappen {path}. Gjør den og mappene over den skrivbare, og prøv igjen.";

    public string FileUnreadable(string path) => $"Kan ikke lese {path}.";

    public string ReportDescription => "Vis de åpne treffene dine.";

    public string ReportNoMatches => "Ingen treff ennå. Start en kjøring med: jobranger run";

    public string ReportNoDate => "ingen dato";

    public string LogsDescription => "Vis logglinjene fra siste kjøring.";

    public string LogsNoRuns => "Ingen kjøringer ennå. Start en med: jobranger run";

    public string LogsMissing(int keepDays) => $"Ingen logglinjer for siste kjøring. Logger lagres i {keepDays} dager.";

    public string ConfigMissing => "Ingen oppsettsfil ennå. Sett opp med: jobranger init";

    public string ProfileMissing(string path) => $"Ingen profilfil på {path}. Sett opp med: jobranger init";

    public string FileAccessFailed => "Kan ikke lese en jobranger-fil. Sjekk tilgangene til mappen.";

    public string ConfigDescription => "Vis eller endre oppsettsfilen.";

    public string ConfigShowDescription => "Skriv ut oppsettsfilen.";

    public string ConfigEditDescription => "Åpne oppsettsfilen i redigeringsprogrammet.";

    public string GuidanceDescription => "Vis, endre eller tilbakestill instruksene for vurderingen.";

    public string GuidanceShowDescription => "Skriv ut instruksene for vurderingen.";

    public string GuidanceEditDescription => "Åpne instruksene for vurderingen i redigeringsprogrammet.";

    public string GuidanceResetDescription => "Gå tilbake til standardinstruksene.";

    public string AboutDescription => "Endre teksten om deg.";

    public string AboutEditDescription => "Åpne teksten om deg i redigeringsprogrammet.";

    public string ContextDescription => "Vis kontekstdokumentene dine.";

    public string ContextListDescription => "List opp kontekstdokumentene dine.";

    public string Saved(string path) => $"✓ Lagret {path}";

    public string SaveFailed(string path) => $"✕ Kunne ikke skrive {path}.";

    public string EditAgain => "Endre igjen? (J/n) ";

    public IReadOnlyList<string> EditNoAnswers { get; } = ["n", "nei", "no"];

    public IReadOnlyList<string> EditYesAnswers => PairYesAnswers;

    public string NoChanges => "Ingen endringer.";

    public string SaveAfterFailure => "Redigeringsprogrammet avsluttet med en feil. Lagre endringene? (j/N) ";

    public string EditorFailedExit => "✕ Redigeringsprogrammet avsluttet med en feil. Ingenting ble lagret.";

    public string EditorNotStarted(string program) =>
        $"✕ Kunne ikke starte redigeringsprogrammet {program}. Sett $VISUAL eller $EDITOR.";

    public string EditorTempFile => "✕ Kunne ikke lese eller skrive den midlertidige filen. Ingenting ble lagret.";

    public string RecheckAds(int ads) => $"  Sjekker {ads} {(ads == 1 ? "annonse" : "annonser")} på nytt ved neste kjøring.";

    public string RecheckAdsCost(int ads, decimal costUsd) => string.Create(
        CultureInfo.InvariantCulture,
        $"  Sjekker {ads} {(ads == 1 ? "annonse" : "annonser")} på nytt ved neste kjøring, opptil omtrent ${costUsd:F2}.");

    public string ConfigKept => "Beholdt det forrige oppsettet.";

    public string GuidanceShowDefault => "# Instrukser for vurderingen: standard";

    public string GuidanceShowEdited(string path) => $"# Instrukser for vurderingen: endret ({path})";

    public string GuidanceBackToDefault => "✓ Tilbake til standardinstruksene.";

    public string GuidanceKept => "Beholdt de forrige instruksene.";

    public string AboutKept => "Beholdt den forrige teksten.";

    public string ContextListLine(string id, string name, int chars, string addedAt) =>
        $"{id}  {name}  {chars} tegn  lagt til {addedAt}";

    public string ContextListEmpty => "Ingen kontekstdokumenter. Legg til et med: jobranger context add <fil>";

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
        $"AI-sjekkene stoppet: kallet til leverandøren feilet. {pending} {AdsWait(pending)} på neste kjøring.";

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

    public string EmailMissingAddress => "Ingen e-postadresse er satt. Legg til en med: jobranger init";

    public string EmailMissingSmtpUrl => "Ingen SMTP-server er satt. Legg til en med: jobranger init";

    public string EmailMissingSmtpPassword => "Ikke noe SMTP-passord er lagret. Legg til et med: jobranger init";

    public string EmailMissingResendKey => "Ingen Resend API-nøkkel er lagret. Legg til en med: jobranger init";

    public string EmailUnusableAddress => "E-postadressen eller avsenderen i konfigurasjonen kan ikke brukes.";

    public string EmailInvalidSmtpUrl => "SMTP-serveren må se ut som smtps://host:465 eller smtp://host:587.";

    public string EmailNonAsciiLogin => "SMTP-brukernavnet og -passordet må være vanlig ASCII.";

    public string EmailNoStarttls(string host, int port) => $"SMTP-serveren på {host}:{port} tilbyr ikke STARTTLS.";

    public string EmailNotSmtp(string host, int port) => $"Serveren på {host}:{port} er ikke en SMTP-server.";

    public string EmailSmtpLoginFailed(string host, int port) => $"SMTP-innlogging på {host}:{port} mislyktes.";

    public string EmailSmtpRecipientRefused(string to) => $"Serveren avviste {to}.";

    public string EmailSmtpSenderRefused(string sender) => $"Serveren avviste avsenderen {sender}.";

    public string EmailSmtpStatus(int status) => $"SMTP-serveren svarte {status}.";

    public string EmailSmtpUnsupported(string host, int port) => $"SMTP-serveren på {host}:{port} støtter ikke innlogging eller denne adressen.";

    public string EmailTlsCertificate(string host) => $"Kunne ikke bekrefte TLS-sertifikatet til {host}.";

    public string EmailTlsFailed(string host, int port) => $"TLS med {host}:{port} mislyktes.";

    public string EmailSmtpDisconnected(string host, int port) => $"SMTP-serveren på {host}:{port} lukket tilkoblingen.";

    public string EmailSmtpReplyTooLong(string host, int port) => $"SMTP-serveren på {host}:{port} sendte et svar som er for langt.";

    public string EmailInvalidHost(string host) => $"SMTP-servernavnet {host} er ikke et gyldig vertsnavn.";

    public string EmailSmtpUnreachable(string host, int port) => $"Kunne ikke nå {host}:{port}.";

    public string EmailSmtpFailed(string host, int port) => $"E-posten kunne ikke sendes gjennom {host}:{port}.";

    public string EmailStalled(string host, int port) => $"SMTP-serveren på {host}:{port} sluttet å svare.";

    public string EmailSmtpTooSlow(string host, int port, int seconds) => $"SMTP-serveren på {host}:{port} brukte mer enn {seconds} s, så sendingen ble stoppet.";

    public string EmailResendKeyRefused => "Resend avviste nøkkelen.";

    public string EmailUnusableResendKey => "Den lagrede Resend API-nøkkelen har tegn en API-nøkkel ikke kan ha. Legg til en med: jobranger init";

    public string EmailResendRedirect(int status) => $"Resend svarte med en omdirigering ({status}), så e-posten ble ikke sendt.";

    public string EmailResendStatus(int status) => $"Resend svarte {status}.";

    public string EmailResendTimeout => "Resend svarte ikke i tide.";

    public string EmailResendUnreachable => "Kunne ikke nå Resend.";

    public string EmailResendTooSlow(int seconds) => $"Resend brukte mer enn {seconds} s, så sendingen ble stoppet.";

    public string EmailSecretStore => "E-postpassordet eller -nøkkelen kunne ikke leses fra hemmelighetslageret.";

    public string InitDescription => "Sett opp jobranger: modell, profil, grenser og levering.";

    public string InitNeedsTerminal => "jobranger init trenger en terminal. Kjør kommandoen i et terminalvindu.";

    public string InitStartOver(string path) => $"Det finnes allerede et oppsett på {path}. Vil du starte på nytt? (j/N)";

    public string InitCancelled => "Oppsettet ble avbrutt. Ingenting ble lagret.";

    public string InitModelTitle => "modell";

    public string InitYouTitle => "om deg";

    public string InitLimitsTitle => "grenser og kilder";

    public string InitDeliveryTitle => "levering";

    public string PromptYesOrNo => "Svar j eller n.";

    public string PromptMoreChoices => "(Bruk opp og ned for å se flere)";

    public string PromptCheckboxHelp => "(mellomrom for å velge, enter for å gå videre)";

    public string InitProvider => "Hvilken AI-leverandør skal lese annonsene?";

    public string InitApiKey => "API-nøkkel";

    public string InitOllamaUrl => "Ollama-adresse";

    public string InitEnvVarWins(string variable) => $"{variable} er satt, så jobranger bruker den verdien når den kjører og ser bort fra det du skriver her.";

    public string InitKeyRejected(string provider) => $"✕ {provider} avviste nøkkelen.";

    public string InitChecking(string provider) => $"Sjekker {provider}...";

    public string InitNotHttpUrl(string url) => $"✕ {url} er ikke en http://- eller https://-adresse.";

    public string InitEncodeAtSign => "✕ Skriv @ i et passord som %40.";

    public string InitCouldNotReach(string provider) => $"✕ Kunne ikke nå {provider}.";

    public string InitOllamaRefused(string url) => $"✕ {url} svarte ikke som Ollama.";

    public string InitTryAgain => "Prøve igjen? (J/n)";

    public string InitKeyWorks => "✓ Nøkkelen virker.";

    public string InitOllamaAnswered => "✓ Ollama svarte.";

    public string InitNoModels(string provider) => $"✕ {provider} har ingen modeller å velge mellom.";

    public string InitModel => "Hvilken modell?";

    public string InitModelRowNoPrice(string name) => $"{name}   ingen pris";

    public string InitPriceInput => "Inndatapris, USD per 1M tokens (tomt = ingen pris)";

    public string InitPriceOutput => "Utdatapris, USD per 1M tokens";

    public string InitNotAmount => "✕ Skriv et tall, for eksempel 1.50.";

    public string InitMonthlyLimit => "Månedsgrense i USD (tomt = ingen grense)";

    public string InitSecretStoreFailed => "✕ Hemmelighetslageret svarte ikke. Lås det opp, eller ha hemmeligheter i en fil med: export JOBRANGER_SECRET_STORE=file";

    public string InitProfilePath => "Sti til profilen din";

    public string InitProfileMissing(string path) => $"✕ Ingen profilfil på {path}. En profilfil må minst ha summary og professional_years.";

    public string InitRoles => "Hvilke roller?";

    public string InitPickOne => "✕ Velg minst én.";

    public string InitLevels => "Nivå";

    public string InitYears => "Erfaring annonsen ber om";

    public string InitHideMasters => "Skjul annonser som krever master? (J/n)";

    public string InitPlaces => "Steder (tomt = hvor som helst)";

    public string InitRelocate => "Kan du flytte? (J/n)";

    public string InitAnywhere => "  Hvor som helst i Norge, kan flytte";

    public string InitPlacesMoving(string places) => $"  {places}, kan flytte";

    public string InitSourcesHeading => "Kilder";

    public string InitSourceNav => "  ✓ NAV              åpen stillingsfeed";

    public string InitSourceEmployers => "  ✓ Karrieresider    Teamtailor og Ashby";

    public string InitSourceFinn => "  – FINN             av";

    public string InitSourceLinkedIn => "  – LinkedIn         av";

    public string InitSourceTerms => "    Vilkårene til FINN og LinkedIn tillater ikke automatisk innsamling.";

    public string InitSourcePersonal => "    Slå dem bare på til egen, personlig bruk.";

    public string InitOptIn => "Slå på FINN eller LinkedIn til personlig bruk? (j/N)";

    public string InitOptInWhich => "Hvilke?";

    public string InitChannels => "Hvordan vil du få treffene?";

    public string InitChannelWebsite => "Nettside   se og endre alt i nettleseren";

    public string InitChannelEmail => "E-post     en kort liste etter hver kjøring";

    public string InitAddress => "E-postadresse";

    public string InitNotAddress => "✕ Det er ikke en e-postadresse.";

    public string InitSendThrough => "Send via";

    public string InitViaSmtp => "e-postkontoen din (SMTP)";

    public string InitViaResend => "Resend";

    public string InitSmtpServer => "SMTP-server";

    public string InitNotSmtpServer => "✕ Skriv serveren som vert eller vert:port.";

    public string InitSmtpUser => "SMTP-brukernavn";

    public string InitSmtpPassword => "SMTP-passord (Gmail: app-passord)";

    public string InitResendKey => "Resend API-nøkkel";

    public string InitSendFrom => "Send fra";

    public string InitNotSender => "✕ Skriv avsenderen som navn <adresse> eller adresse.";

    public string InitEnterApiKey => "✕ Skriv inn API-nøkkelen.";

    public string InitEnterSmtpPassword => "✕ Skriv inn SMTP-passordet.";

    public string InitEnterResendKey => "✕ Skriv inn Resend API-nøkkelen.";

    public string InitTestSent(string address) => $"✓ Test-e-post sendt til {address}";

    public string InitTestFailed(string reason) => $"✕ {reason}";

    public string InitTestSubject => "jobranger test-e-post";

    public string InitTestBody(string stamp) => $"Dette er en test-e-post fra jobranger init. Sendt {stamp}.";

    public string InitRunTimes => "Kjøretider (24 t, skilt med komma)";

    public string InitNotRunTime(string time) => $"✕ {time} er ikke et klokkeslett på formen TT:MM";

    public string InitRunTimesTooClose(int hours) => $"✕ Kjøretidene må ligge minst {hours} timer fra hverandre";

    public string InitSaved(string path) => $"✓ Lagret {path}";

    public string InitSecretsStored(string place) => $"✓ Hemmeligheter lagret i {place}, bare på denne maskinen.";

    public string InitStoreKeychain => "macOS-nøkkelringen";

    public string InitStoreKeyring => "systemets nøkkelring";

    public string InitNext => "Neste: jobranger run";

    public string InitSaveFailed(string path) => $"✕ Kunne ikke skrive {path}. Sjekk tillatelsene på mappen.";

    public string InitConfigDirIsFile(string path) => $"✕ {path} er en fil. jobranger trenger en mappe på den stien. Flytt filen, og kjør jobranger init på nytt.";

    public string InitConfigIsFolder(string path) => $"✕ {path} er en mappe. Fjern den, og kjør jobranger init på nytt.";

    public string InitSecretsUnreadable(string path) => $"Kan ikke lese {path}. Rett eller fjern filen, og kjør jobranger init på nytt.";

    public string InitSecretsBusy(string path) => $"En annen jobranger-prosess bruker {path}. Prøv igjen når den er ferdig.";

    public string InitSecretsKept => "Oppsettet ble ikke lagret. Hemmeligheter som alt er skrevet, blir i lageret til neste jobranger init.";

    public char PromptYesKey => 'j';

    public char PromptNoKey => 'n';

    public string InitStepHeader(int number, int count, string title) => $"jobranger-oppsett · {number} av {count} · {title}";

    public string InitCostBasis(int ads) => $"Kostnadsanslagene regner med {ads} annonser om dagen.";

    public string InitModelRow(string name, decimal usd) =>
        string.Create(CultureInfo.InvariantCulture, $"{name}   omtrent ${usd:F2} om dagen");

    public string InitRoleLabel(string id, string label) => initRoleLabels.GetValueOrDefault(id, label);

    public string InitLevelLabel(string level) => initLevelLabels[level];

    public string InitYearsLabel(string band) => initYearsLabels[band];

    private static string AdsWait(int pending) => pending == 1 ? "annonse venter" : "annonser venter";
}
