using System.Globalization;
using System.Text.Json.Nodes;
using Jobranger.Cli.Catalogue;
using Jobranger.Cli.Http;
using Jobranger.Cli.Text;

namespace Jobranger.Cli.Config;

internal sealed class ConfigReader(string home) {
    public const int Version = 1;

    private static readonly string[] rootKeys = [
        ConfigKeys.Version,
        ConfigKeys.Profile,
        ConfigKeys.Targets,
        ConfigKeys.Sources,
        ConfigKeys.Schedule,
        ConfigKeys.Model,
        ConfigKeys.Notify,
        ConfigKeys.Language,
    ];
    private static readonly string[] profileKeys = [ConfigKeys.Path, ConfigKeys.CvName, ConfigKeys.CvReadAt];
    private static readonly string[] targetKeys =
        [ConfigKeys.Subfields, ConfigKeys.Limits, ConfigKeys.Places, ConfigKeys.Relocate];
    private static readonly string[] limitKeys = [ConfigKeys.Levels, ConfigKeys.Years, ConfigKeys.HideMasters];
    private static readonly string[] sourceKeys =
        [ConfigKeys.Nav, ConfigKeys.Employers, ConfigKeys.Finn, ConfigKeys.LinkedIn, ConfigKeys.EmployerFeeds];
    private static readonly string[] feedKeys = [ConfigKeys.Kind, ConfigKeys.Key, ConfigKeys.Company, ConfigKeys.Board];
    private static readonly string[] scheduleKeys = [ConfigKeys.Times];
    private static readonly string[] modelKeys = [
        ConfigKeys.Provider, ConfigKeys.Model, ConfigKeys.OllamaUrl, ConfigKeys.MonthlyLimitUsd, ConfigKeys.Price,
    ];
    private static readonly string[] priceKeys = [ConfigKeys.InputPerMtok, ConfigKeys.OutputPerMtok];
    private static readonly string[] notifyKeys = [ConfigKeys.Website, ConfigKeys.Email];
    private static readonly string[] emailKeys = [
        ConfigKeys.On, ConfigKeys.Address, ConfigKeys.Via, ConfigKeys.SmtpUrl, ConfigKeys.SmtpUser, ConfigKeys.Sender,
    ];
    private static readonly string[] versionChoices = [Version.ToString(CultureInfo.InvariantCulture)];

    private readonly List<ConfigError> errors = [];

    public IReadOnlyList<ConfigError> Errors => errors;

    public AppConfig? Read(JsonNode? root, string fileName) {
        if (root is not JsonObject values) {
            errors.Add(new ConfigError(fileName, ConfigProblem.NotSection));
            return null;
        }
        ConfigSection document = new(values, string.Empty, rootKeys, errors);
        document.ChoiceText(ConfigKeys.Version, versionChoices);
        return new AppConfig {
            Profile = ReadProfile(document) ?? new ProfileRef(home),
            Targets = ReadTargets(document),
            Sources = ReadSources(document),
            Schedule = ReadSchedule(document),
            Model = ReadModel(document),
            Notify = ReadNotify(document),
            Language = document.Choice<Language>(ConfigKeys.Language),
        };
    }

    public static Language? ReadLanguage(JsonNode? root) =>
        root is JsonObject document
            ? new ConfigSection(document, string.Empty, rootKeys, []).Choice<Language>(ConfigKeys.Language)
            : null;

    private ProfileRef? ReadProfile(ConfigSection document) {
        if (document.Child(ConfigKeys.Profile, profileKeys, required: true) is not ConfigSection section) {
            return null;
        }
        string? path = section.RequiredText(ConfigKeys.Path);
        string? cvName = section.Text(ConfigKeys.CvName);
        string? cvReadAt = section.Text(ConfigKeys.CvReadAt);
        return path is null ? null : new ProfileRef(HomePath.Expand(path, home), cvName, cvReadAt);
    }

    private static Targets ReadTargets(ConfigSection document) {
        Targets defaults = new();
        if (document.Child(ConfigKeys.Targets, targetKeys) is not ConfigSection section) {
            return defaults;
        }
        return new Targets {
            Subfields = section.ChoiceList(ConfigKeys.Subfields, Targets.AllRoles) ?? defaults.Subfields,
            Limits = ReadLimits(section),
            Places = Places(section.TextList(ConfigKeys.Places)) ?? defaults.Places,
            Relocate = section.Bool(ConfigKeys.Relocate) ?? defaults.Relocate,
        };
    }

    private static Limits ReadLimits(ConfigSection targets) {
        Limits defaults = new();
        if (targets.Child(ConfigKeys.Limits, limitKeys) is not ConfigSection section) {
            return defaults;
        }
        return new Limits {
            Levels = Sorted(section.ChoiceList(ConfigKeys.Levels, Limits.AllLevels)) ?? defaults.Levels,
            Years = Sorted(section.ChoiceList(ConfigKeys.Years, Limits.AllYears)) ?? defaults.Years,
            HideMasters = section.Bool(ConfigKeys.HideMasters) ?? defaults.HideMasters,
        };
    }

    private static SourcesConfig ReadSources(ConfigSection document) {
        SourcesConfig defaults = new();
        if (document.Child(ConfigKeys.Sources, sourceKeys) is not ConfigSection section) {
            return defaults;
        }
        return new SourcesConfig {
            Nav = section.Bool(ConfigKeys.Nav) ?? defaults.Nav,
            Employers = section.Bool(ConfigKeys.Employers) ?? defaults.Employers,
            Finn = section.Bool(ConfigKeys.Finn) ?? defaults.Finn,
            LinkedIn = section.Bool(ConfigKeys.LinkedIn) ?? defaults.LinkedIn,
            EmployerFeeds = ReadFeeds(section) ?? defaults.EmployerFeeds,
        };
    }

    private static List<EmployerFeed>? ReadFeeds(ConfigSection sources) =>
        sources.Sections(ConfigKeys.EmployerFeeds, feedKeys)?.Select(ReadFeed).OfType<EmployerFeed>().ToList();

    private static EmployerFeed? ReadFeed(ConfigSection item) {
        string? kind = item.ChoiceText(ConfigKeys.Kind, EmployerFeed.AllKinds);
        string? key = item.RequiredText(ConfigKeys.Key);
        string? company = item.RequiredText(ConfigKeys.Company);
        string? board = item.RequiredText(ConfigKeys.Board);
        return kind is not null && key is not null && company is not null && board is not null
            ? new EmployerFeed(kind, key, company, board)
            : null;
    }

    private static ScheduleConfig ReadSchedule(ConfigSection document) {
        ScheduleConfig defaults = new();
        if (document.Child(ConfigKeys.Schedule, scheduleKeys) is not ConfigSection section) {
            return defaults;
        }
        return new ScheduleConfig { Times = section.TextList(ConfigKeys.Times) ?? defaults.Times };
    }

    private static ModelConfig ReadModel(ConfigSection document) {
        ModelConfig defaults = new();
        if (document.Child(ConfigKeys.Model, modelKeys) is not ConfigSection section) {
            return defaults;
        }
        string? ollamaUrl = section.Text(ConfigKeys.OllamaUrl);
        return new ModelConfig {
            Provider = section.Choice<ProviderName>(ConfigKeys.Provider),
            Model = section.Text(ConfigKeys.Model),
            OllamaUrl = ollamaUrl is null ? defaults.OllamaUrl : OllamaUrls.Root(ollamaUrl),
            MonthlyLimitUsd = section.Number(ConfigKeys.MonthlyLimitUsd),
            Price = ReadPrice(section),
        };
    }

    private static PriceOverride? ReadPrice(ConfigSection model) {
        if (model.Child(ConfigKeys.Price, priceKeys) is not ConfigSection section) {
            return null;
        }
        decimal? input = section.RequiredNumber(ConfigKeys.InputPerMtok);
        decimal? output = section.RequiredNumber(ConfigKeys.OutputPerMtok);
        return input is decimal knownInput && output is decimal knownOutput
            ? new PriceOverride(knownInput, knownOutput)
            : null;
    }

    private static NotifyConfig ReadNotify(ConfigSection document) {
        NotifyConfig defaults = new();
        if (document.Child(ConfigKeys.Notify, notifyKeys) is not ConfigSection section) {
            return defaults;
        }
        return new NotifyConfig {
            Website = section.Bool(ConfigKeys.Website) ?? defaults.Website,
            Email = ReadEmail(section),
        };
    }

    private static EmailConfig ReadEmail(ConfigSection notify) {
        EmailConfig defaults = new();
        if (notify.Child(ConfigKeys.Email, emailKeys) is not ConfigSection section) {
            return defaults;
        }
        return new EmailConfig {
            On = section.Bool(ConfigKeys.On) ?? defaults.On,
            Address = section.Text(ConfigKeys.Address),
            Via = section.Choice<EmailVia>(ConfigKeys.Via) ?? defaults.Via,
            SmtpUrl = section.Text(ConfigKeys.SmtpUrl),
            SmtpUser = section.Text(ConfigKeys.SmtpUser),
            Sender = section.Text(ConfigKeys.Sender),
        };
    }

    private static List<string>? Places(List<string>? places) =>
        places?.Select(place => place.Trim())
            .Where(place => place.Length > 0)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

    private static List<string>? Sorted(List<string>? values) =>
        values?.Distinct(StringComparer.Ordinal).Order(StringComparer.Ordinal).ToList();
}
