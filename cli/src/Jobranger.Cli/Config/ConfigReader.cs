using System.Globalization;
using System.Text.Json.Nodes;
using Jobranger.Cli.Catalogue;
using Jobranger.Cli.Text;

namespace Jobranger.Cli.Config;

internal sealed class ConfigReader(string home) {
    public const int Version = 1;

    private const string ollamaApiPath = "/v1";
    private const char slash = '/';
    private const string feedsKey = "employer_feeds";

    private static readonly string[] rootKeys =
        ["version", "profile", "targets", "sources", "schedule", "model", "notify", "language"];
    private static readonly string[] profileKeys = ["path", "cv_name", "cv_read_at"];
    private static readonly string[] targetKeys = ["subfields", "limits", "places", "relocate"];
    private static readonly string[] limitKeys = ["levels", "years", "hide_masters"];
    private static readonly string[] sourceKeys = ["nav", "employers", "finn", "linkedin", feedsKey];
    private static readonly string[] feedKeys = ["kind", "key", "company", "board"];
    private static readonly string[] scheduleKeys = ["times"];
    private static readonly string[] modelKeys = ["provider", "model", "ollama_url", "monthly_limit_usd", "price"];
    private static readonly string[] priceKeys = ["input_per_mtok", "output_per_mtok"];
    private static readonly string[] notifyKeys = ["website", "email"];
    private static readonly string[] emailKeys = ["on", "address", "via", "smtp_url", "smtp_user", "sender"];
    private static readonly string[] versionChoices = [Version.ToString(CultureInfo.InvariantCulture)];

    private readonly List<ConfigError> errors = [];

    public IReadOnlyList<ConfigError> Errors => errors;

    public AppConfig? Read(JsonNode? root, string fileName) {
        if (root is not JsonObject values) {
            errors.Add(new ConfigError(fileName, ConfigProblem.NotSection));
            return null;
        }
        ConfigSection document = new(values, string.Empty, rootKeys, errors);
        document.ChoiceText("version", versionChoices);
        return new AppConfig {
            Profile = ReadProfile(document) ?? new ProfileRef(home),
            Targets = ReadTargets(document),
            Sources = ReadSources(document),
            Schedule = ReadSchedule(document),
            Model = ReadModel(document),
            Notify = ReadNotify(document),
            Language = document.Choice<Language>("language"),
        };
    }

    public static Language? ReadLanguage(JsonNode? root) =>
        root is JsonObject document
            ? new ConfigSection(document, string.Empty, rootKeys, []).Choice<Language>("language")
            : null;

    private ProfileRef? ReadProfile(ConfigSection document) {
        if (document.Child("profile", profileKeys, required: true) is not ConfigSection section) {
            return null;
        }
        string? path = section.RequiredText("path");
        string? cvName = section.Text("cv_name");
        string? cvReadAt = section.Text("cv_read_at");
        return path is null ? null : new ProfileRef(HomePath.Expand(path, home), cvName, cvReadAt);
    }

    private static Targets ReadTargets(ConfigSection document) {
        Targets defaults = new();
        if (document.Child("targets", targetKeys) is not ConfigSection section) {
            return defaults;
        }
        return new Targets {
            Subfields = section.ChoiceList("subfields", Targets.AllRoles) ?? defaults.Subfields,
            Limits = ReadLimits(section),
            Places = Places(section.TextList("places")) ?? defaults.Places,
            Relocate = section.Bool("relocate") ?? defaults.Relocate,
        };
    }

    private static Limits ReadLimits(ConfigSection targets) {
        Limits defaults = new();
        if (targets.Child("limits", limitKeys) is not ConfigSection section) {
            return defaults;
        }
        return new Limits {
            Levels = Sorted(section.ChoiceList("levels", Limits.AllLevels)) ?? defaults.Levels,
            Years = Sorted(section.ChoiceList("years", Limits.AllYears)) ?? defaults.Years,
            HideMasters = section.Bool("hide_masters") ?? defaults.HideMasters,
        };
    }

    private static SourcesConfig ReadSources(ConfigSection document) {
        SourcesConfig defaults = new();
        if (document.Child("sources", sourceKeys) is not ConfigSection section) {
            return defaults;
        }
        return new SourcesConfig {
            Nav = section.Bool("nav") ?? defaults.Nav,
            Employers = section.Bool("employers") ?? defaults.Employers,
            Finn = section.Bool("finn") ?? defaults.Finn,
            LinkedIn = section.Bool("linkedin") ?? defaults.LinkedIn,
            EmployerFeeds = ReadFeeds(section) ?? defaults.EmployerFeeds,
        };
    }

    private static List<EmployerFeed>? ReadFeeds(ConfigSection sources) =>
        sources.Sections(feedsKey, feedKeys)?.Select(ReadFeed).OfType<EmployerFeed>().ToList();

    private static EmployerFeed? ReadFeed(ConfigSection item) {
        string? kind = item.ChoiceText("kind", EmployerFeed.AllKinds);
        string? key = item.RequiredText("key");
        string? company = item.RequiredText("company");
        string? board = item.RequiredText("board");
        return kind is not null && key is not null && company is not null && board is not null
            ? new EmployerFeed(kind, key, company, board)
            : null;
    }

    private static ScheduleConfig ReadSchedule(ConfigSection document) {
        ScheduleConfig defaults = new();
        if (document.Child("schedule", scheduleKeys) is not ConfigSection section) {
            return defaults;
        }
        return new ScheduleConfig { Times = section.TextList("times") ?? defaults.Times };
    }

    private static ModelConfig ReadModel(ConfigSection document) {
        ModelConfig defaults = new();
        if (document.Child("model", modelKeys) is not ConfigSection section) {
            return defaults;
        }
        string? ollamaUrl = section.Text("ollama_url");
        return new ModelConfig {
            Provider = section.Choice<ProviderName>("provider"),
            Model = section.Text("model"),
            OllamaUrl = ollamaUrl is null ? defaults.OllamaUrl : OllamaRoot(ollamaUrl),
            MonthlyLimitUsd = section.Number("monthly_limit_usd"),
            Price = ReadPrice(section),
        };
    }

    private static PriceOverride? ReadPrice(ConfigSection model) {
        if (model.Child("price", priceKeys) is not ConfigSection section) {
            return null;
        }
        decimal? input = section.RequiredNumber("input_per_mtok");
        decimal? output = section.RequiredNumber("output_per_mtok");
        return input is decimal knownInput && output is decimal knownOutput
            ? new PriceOverride(knownInput, knownOutput)
            : null;
    }

    private static NotifyConfig ReadNotify(ConfigSection document) {
        NotifyConfig defaults = new();
        if (document.Child("notify", notifyKeys) is not ConfigSection section) {
            return defaults;
        }
        return new NotifyConfig {
            Website = section.Bool("website") ?? defaults.Website,
            Email = ReadEmail(section),
        };
    }

    private static EmailConfig ReadEmail(ConfigSection notify) {
        EmailConfig defaults = new();
        if (notify.Child("email", emailKeys) is not ConfigSection section) {
            return defaults;
        }
        return new EmailConfig {
            On = section.Bool("on") ?? defaults.On,
            Address = section.Text("address"),
            Via = section.Choice<EmailVia>("via") ?? defaults.Via,
            SmtpUrl = section.Text("smtp_url"),
            SmtpUser = section.Text("smtp_user"),
            Sender = section.Text("sender"),
        };
    }

    private static List<string>? Places(List<string>? places) =>
        places?.Select(place => place.Trim())
            .Where(place => place.Length > 0)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

    private static List<string>? Sorted(List<string>? values) =>
        values?.Distinct(StringComparer.Ordinal).Order(StringComparer.Ordinal).ToList();

    private static string OllamaRoot(string url) {
        if (!Uri.TryCreate(url, UriKind.Absolute, out Uri? parsed)) {
            return url;
        }
        string path = parsed.AbsolutePath.TrimEnd(slash);
        while (path.EndsWith(ollamaApiPath, StringComparison.Ordinal)) {
            path = path[..^ollamaApiPath.Length].TrimEnd(slash);
        }
        return parsed.GetLeftPart(UriPartial.Authority) + path + parsed.Query + parsed.Fragment;
    }
}
