using System.Globalization;
using System.Runtime.Versioning;
using Jobranger.Cli.Catalogue;
using Jobranger.Cli.Config;
using Jobranger.Cli.Tests.Mocks;
using Jobranger.Cli.Text;
using Microsoft.Extensions.Logging.Abstractions;

namespace Jobranger.Cli.Tests.Config;

[UnsupportedOSPlatform("windows")]
public sealed class ConfigFileTests : IDisposable {
    private const string home = "/home/user";
    private const string secret = "pass-1234";
    private const int deepNesting = 100;

    private static readonly string sample = TestData("config.yaml");

    private readonly TempDir temp = new();
    private readonly string path;

    public ConfigFileTests() {
        path = temp.File("config.yaml");
    }

    public void Dispose() => temp.Dispose();

    [Fact]
    public void ShouldKeepBlockComments_WhenSavingConfig() {
        File.WriteAllText(path, sample.Replace("# what to look for", "\n#what to look for", StringComparison.Ordinal));
        AppConfig config = ConfigFile.Load(path, home);

        ConfigFile.Save(
            path, config with { Targets = config.Targets with { Relocate = false } }, home, NullLogger.Instance);

        string[] comments = [.. File.ReadAllLines(path).Where(line => line.Contains('#', StringComparison.Ordinal))];
        Assert.Equal(
            [
                "# jobranger settings",
                "# what to look for",
                "  limits: # filters",
                "  places: [Bergen, Ålesund] # where",
                "  relocate: false # chosen during setup",
                "# end of settings",
            ],
            comments);
    }

    [Fact]
    public void ShouldWriteSameText_WhenSavingUnchangedConfig() {
        File.WriteAllText(path, sample);

        ConfigFile.Save(path, ConfigFile.Load(path, home), home, NullLogger.Instance);

        Assert.Equal(sample, File.ReadAllText(path));
    }

    [Fact]
    public void ShouldWriteDefaults_WhenSettingsAreOmitted() {
        File.WriteAllText(
            path,
            "profile:\n  path: /home/user/profile.yaml\ntargets:\n  limits: {}\nsources: {}\nschedule: {}\n"
            + "model: {}\nnotify:\n  email: {}\n");

        ConfigFile.Save(path, ConfigFile.Load(path, home), home, NullLogger.Instance);

        Assert.Equal(TestData("default.yaml"), File.ReadAllText(path));
    }

    [Fact]
    public void ShouldAcceptEveryChoice_WhenConfigListsAllValues() {
        File.WriteAllText(path, sample
            .Replace("[backend, fullstack]", "[" + string.Join(", ", Targets.AllRoles) + "]", StringComparison.Ordinal)
            .Replace("[junior, mid, none]", "[" + string.Join(", ", Limits.AllLevels) + "]", StringComparison.Ordinal)
            .Replace("[0-1, 1-3]", "[" + string.Join(", ", Limits.AllYears) + "]", StringComparison.Ordinal));

        AppConfig config = ConfigFile.Load(path, home);

        Assert.Equal(
            (9, 5, 4),
            (config.Targets.Subfields.Count, config.Targets.Limits.Levels.Count, config.Targets.Limits.Years.Count));
    }

    [Fact]
    public void ShouldKeepHashLineAsText_WhenItIsInsideBlockScalar() {
        File.WriteAllText(path, sample.Replace(
            "  model: claude-haiku-4-5\n",
            "  model: |\n    # not a comment\n    claude\n# about the url\n",
            StringComparison.Ordinal));

        ConfigFile.Save(path, ConfigFile.Load(path, home), home, NullLogger.Instance);

        Assert.Equal(
            [
                "# jobranger settings",
                "# what to look for",
                "  limits: # filters",
                "  places: [Bergen, Ålesund] # where",
                "  relocate: true # chosen during setup",
                "  model: '# not a comment",
                "  # about the url",
                "# end of settings",
            ],
            File.ReadAllLines(path).Where(line => line.Contains('#', StringComparison.Ordinal)));
    }

    [Fact]
    public void ShouldKeepInlineComment_WhenLastLineHasNoNewline() {
        File.WriteAllText(
            path, sample.Replace("language: nb\n# end of settings\n", "language: nb # last", StringComparison.Ordinal));

        ConfigFile.Save(path, ConfigFile.Load(path, home), home, NullLogger.Instance);

        Assert.Contains("language: nb # last", File.ReadAllLines(path));
    }

    [Fact]
    public void ShouldReportNotUtf8_WhenBytesAreInvalid() {
        File.WriteAllBytes(path, [.. "version: 1\nprofile:\n  path: "u8, 0xC3, 0x28]);

        ConfigInvalidException invalid = Assert.Throws<ConfigInvalidException>(() => ConfigFile.Load(path, home));

        Assert.Equal([new ConfigError("config.yaml", ConfigProblem.NotUtf8)], invalid.Errors);
    }

    [Fact]
    public void ShouldRefuseToSave_WhenConfigIsInvalid() {
        File.WriteAllText(path, sample);
        AppConfig config = Full() with { Schedule = new ScheduleConfig { Times = ["25:00"] } };

        Assert.Throws<ConfigInvalidException>(() => ConfigFile.Save(path, config, home, NullLogger.Instance));

        Assert.Equal(sample, File.ReadAllText(path));
    }

    [Fact]
    public void ShouldListEveryError_WhenSeveralKeysAreUnknown() {
        File.WriteAllText(path, sample + "unknown_key: 1\nother_key: 2\n");

        ConfigInvalidException invalid = Assert.Throws<ConfigInvalidException>(() => ConfigFile.Load(path, home));

        Assert.Equal(
            $"{path} is invalid: unknown_key: is not a known setting | other_key: is not a known setting",
            invalid.Message);
    }

    [Fact]
    public void ShouldWriteLfText_WhenFileUsesCrlf() {
        File.WriteAllText(path, sample.ReplaceLineEndings("\r\n"));

        ConfigFile.Save(path, ConfigFile.Load(path, home), home, NullLogger.Instance);

        Assert.Equal(sample, File.ReadAllText(path));
    }

    [Fact]
    public void ShouldReadEveryField_WhenConfigIsSaved() {
        AppConfig config = Full();

        ConfigFile.Save(path, config, home, NullLogger.Instance);
        AppConfig loaded = ConfigFile.Load(path, home);

        Assert.Equal(Describe(config), Describe(loaded));
    }

    [Theory]
    [InlineData("/home/user/.config/jobranger/profile.yaml", "~/.config/jobranger/profile.yaml")]
    [InlineData("/home/username/profile.yaml", "/home/username/profile.yaml")]
    [InlineData("/home/user", "/home/user")]
    public void ShouldWriteTildeOnlyUnderHome_WhenSavingProfilePath(string profilePath, string written) {
        ConfigFile.Save(path, Full() with { Profile = new ProfileRef(profilePath) }, home, NullLogger.Instance);

        Assert.Contains("  path: " + written, File.ReadAllLines(path));
    }

    [Fact]
    public void ShouldKeepRelativeProfilePath_WhenSaving() {
        AppConfig config = Full() with { Profile = new ProfileRef("profile.yaml") };

        ConfigFile.Save(path, config, Directory.GetCurrentDirectory(), NullLogger.Instance);

        Assert.Contains("  path: profile.yaml", File.ReadAllLines(path));
    }

    [Theory]
    [InlineData("~/.config/jobranger/profile.yaml", "/home/user/.config/jobranger/profile.yaml")]
    [InlineData("'~'", "/home/user")]
    public void ShouldExpandTilde_WhenProfilePathStartsWithIt(string written, string expanded) {
        File.WriteAllText(path, sample.Replace("~/.config/jobranger/profile.yaml", written, StringComparison.Ordinal));

        AppConfig config = ConfigFile.Load(path, home);

        Assert.Equal(expanded, config.Profile.Path);
    }

    [Fact]
    public void ShouldReadPlacesAsText_WhenTheyLookLikeOtherTypes() {
        AppConfig config = Full() with { Targets = new Targets { Places = ["null", "true", "123", "~", "1e3"] } };

        ConfigFile.Save(path, config, home, NullLogger.Instance);

        Assert.Equal(["null", "true", "123", "~", "1e3"], ConfigFile.Load(path, home).Targets.Places);
    }

    [Fact]
    public void ShouldWriteFileWithMode0600_WhenSavingConfig() {
        ConfigFile.Save(path, Full(), home, NullLogger.Instance);

        Assert.Equal(FileModes.OwnerReadWrite, File.GetUnixFileMode(path));
    }

    [Theory]
    [InlineData("targets: [unclosed\n")]
    [InlineData("version: 1\n...\n---\n[unclosed\n")]
    public void ShouldKeepBrokenFile_WhenSavingOverInvalidYaml(string broken) {
        File.WriteAllText(path, broken);

        Assert.Throws<ConfigInvalidException>(() => ConfigFile.Save(path, Full(), home, NullLogger.Instance));

        Assert.Equal(broken, File.ReadAllText(path));
    }

    [Fact]
    public void ShouldKeepSectionOrder_WhenSavingConfig() {
        string notify = sample[sample.IndexOf("notify:\n", StringComparison.Ordinal)..sample.IndexOf(
            "language: nb\n", StringComparison.Ordinal)];
        string reordered = sample.Replace(notify, string.Empty, StringComparison.Ordinal)
            .Replace("version: 1\n", string.Empty, StringComparison.Ordinal)
            .Replace("# what to look for\n", notify + "version: 1\n# what to look for\n", StringComparison.Ordinal);
        File.WriteAllText(path, reordered);

        ConfigFile.Save(path, ConfigFile.Load(path, home), home, NullLogger.Instance);

        Assert.Equal(reordered, File.ReadAllText(path));
    }

    [Fact]
    public void ShouldKeepHeaderOnTop_WhenSavingPartialConfig() {
        File.WriteAllText(path, "# header\nnotify:\n  email:\n    on: false\nprofile:\n  path: ~/p\n");

        ConfigFile.Save(path, ConfigFile.Load(path, home), home, NullLogger.Instance);

        Assert.Equal("# header", File.ReadLines(path).First());
    }

    [Fact]
    public void ShouldThrowMissing_WhenFileIsAbsent() {
        ConfigMissingException missing = Assert.Throws<ConfigMissingException>(() => ConfigFile.Load(path, home));

        Assert.Equal((path, $"file not found: {path}"), (missing.Path, missing.Message));
    }

    [Fact]
    public void ShouldStripApiPath_WhenOllamaUrlEndsWithIt() {
        File.WriteAllText(
            path, sample.Replace("http://localhost:11434", "http://box:11434//v1/v1/", StringComparison.Ordinal));

        AppConfig config = ConfigFile.Load(path, home);

        Assert.Equal("http://box:11434", config.Model.OllamaUrl);
    }

    [Fact]
    public void ShouldDropBlankAndRepeatedPlaces_WhenLoading() {
        File.WriteAllText(
            path, sample.Replace("[Bergen, Ålesund]", "[' Bergen ', bergen, '', Ålesund]", StringComparison.Ordinal));

        AppConfig config = ConfigFile.Load(path, home);

        Assert.Equal(["Bergen", "Ålesund"], config.Targets.Places);
    }

    [Theory]
    [InlineData("version: 1", "version: 2", "version", nameof(ConfigProblem.NotChoice))]
    [InlineData("language: nb", "language: de", "language", nameof(ConfigProblem.NotChoice))]
    [InlineData("provider: anthropic", "provider: gemini", "model.provider", nameof(ConfigProblem.NotChoice))]
    [InlineData("via: smtp", "via: post", "notify.email.via", nameof(ConfigProblem.NotChoice))]
    [InlineData(
        "subfields: [backend, fullstack]", "subfields: [blockchain]", "targets.subfields.0",
        nameof(ConfigProblem.NotChoice))]
    [InlineData(
        "levels: [junior, mid, none]", "levels: [intern]", "targets.limits.levels.0", nameof(ConfigProblem.NotChoice))]
    [InlineData(
        "kind: teamtailor", "kind: workday", "sources.employer_feeds.0.kind", nameof(ConfigProblem.NotChoice))]
    [InlineData(
        "hide_masters: true", "hide_masters: 7", "targets.limits.hide_masters", nameof(ConfigProblem.NotBoolean))]
    [InlineData(
        "monthly_limit_usd: 5", "monthly_limit_usd: .nan", "model.monthly_limit_usd", nameof(ConfigProblem.NotNumber))]
    [InlineData("places: [Bergen, Ålesund]", "places: Bergen", "targets.places", nameof(ConfigProblem.NotList))]
    [InlineData(
        "schedule:\n  times: ['03:00', '15:00']\n", "schedule: daily\n", "schedule", nameof(ConfigProblem.NotSection))]
    [InlineData("model: claude-haiku-4-5", "model: [a, b]", "model.model", nameof(ConfigProblem.NotText))]
    [InlineData("  path: ~/.config/jobranger/profile.yaml\n", "", "profile.path", nameof(ConfigProblem.Required))]
    [InlineData(
        "path: ~/.config/jobranger/profile.yaml", "path: ''", "profile.path", nameof(ConfigProblem.Required))]
    [InlineData("profile:\n", "old_profile:\n", "profile", nameof(ConfigProblem.Required))]
    [InlineData(
        "price: null", "price:\n    output_per_mtok: 1", "model.price.input_per_mtok", nameof(ConfigProblem.Required))]
    [InlineData("places: [Bergen, Ålesund]", "places: [[Bergen]]", "targets.places.0", nameof(ConfigProblem.NotText))]
    [InlineData("version: 1", "version: 1\nunknown_key: 1", "unknown_key", nameof(ConfigProblem.UnknownKey))]
    [InlineData(
        "  relocate: true", "  relocate: true\n  relocate: false", "targets.relocate",
        nameof(ConfigProblem.DuplicateKey))]
    [InlineData(
        "hide_masters: true", "hide_masters: &flag true\n    other: *flag", "targets.limits.other",
        nameof(ConfigProblem.AliasUsed))]
    public void ShouldReportLocation_WhenValueIsNotAllowed(
        string original, string replacement, string location, string problem) {
        File.WriteAllText(path, sample.Replace(original, replacement, StringComparison.Ordinal));

        ConfigInvalidException invalid = Assert.Throws<ConfigInvalidException>(() => ConfigFile.Load(path, home));

        Assert.Contains(
            (location, Enum.Parse<ConfigProblem>(problem)), invalid.Errors.Select(error => (error.Location, error.Problem)));
    }

    [Theory]
    [InlineData("~")]
    [InlineData("Null")]
    [InlineData("NULL")]
    [InlineData("")]
    public void ShouldReadNull_WhenPlainValueIsNullLiteral(string literal) {
        File.WriteAllText(path, sample.Replace("cv_name: cv.pdf", "cv_name: " + literal, StringComparison.Ordinal));

        AppConfig config = ConfigFile.Load(path, home);

        Assert.Null(config.Profile.CvName);
    }

    [Theory]
    [InlineData("- version\n- profile\n")]
    [InlineData("")]
    public void ShouldReportNotSection_WhenFileIsNotMapping(string text) {
        File.WriteAllText(path, text);

        ConfigInvalidException invalid = Assert.Throws<ConfigInvalidException>(() => ConfigFile.Load(path, home));

        Assert.Equal([new ConfigError("config.yaml", ConfigProblem.NotSection)], invalid.Errors);
    }

    [Theory]
    [InlineData("version: 1\nname: a: b\n", 2)]
    [InlineData("version: 1\nprofile:\n  path: ~/p\n---\nimportant: data\n", 4)]
    [InlineData("... x\n", 1)]
    [InlineData("version: 1\nprofile:\n  path: ~/p\nsources:\n  nav: true\ntargets:\n\tplaces: [x]\n", 7)]
    [InlineData(
        "version: 1\nprofile:\n  path: ~/p\nmodel:\n  model: |\n    line\n    \tinner tab\ntargets:\n\tplaces: [x]\n", 9)]
    [InlineData("]\nversion: 1\nprofile:\n  path: ~/p\nmodel:\n  model: |\n    \tx\n", 1)]
    public void ShouldNameErrorLine_WhenYamlIsBroken(string text, int line) {
        File.WriteAllText(path, text);

        ConfigInvalidException invalid = Assert.Throws<ConfigInvalidException>(() => ConfigFile.Load(path, home));

        Assert.Equal(
            [new ConfigError("config.yaml", ConfigProblem.NotYaml, line.ToString(CultureInfo.InvariantCulture))],
            invalid.Errors);
    }

    [Fact]
    public void ShouldReportNotYaml_WhenNestingIsTooDeep() {
        File.WriteAllText(
            path,
            "version: 1\nprofile:\n  path: ~/p\ntargets:\n  places: " + new string('[', deepNesting)
            + new string(']', deepNesting) + "\n");

        ConfigInvalidException invalid = Assert.Throws<ConfigInvalidException>(() => ConfigFile.Load(path, home));

        Assert.Equal([new ConfigError("config.yaml", ConfigProblem.NotYaml, "5")], invalid.Errors);
    }

    [Theory]
    [InlineData("smtp_url: smtp://smtp.example.test:587", "smtp_url: \"smtp://user:" + secret + "@smtp.example.test\"")]
    [InlineData("ollama_url: http://localhost:11434", "ollama_url: http://user:" + secret + "@[::1")]
    [InlineData("address: user@example.com", "address: " + secret + "@")]
    [InlineData("hide_masters: true", "hide_masters: " + secret)]
    [InlineData("monthly_limit_usd: 5", "monthly_limit_usd: " + secret)]
    [InlineData("version: 1", "version: 1\ntargets: [" + secret)]
    public void ShouldLeaveValueOutOfError_WhenConfigIsInvalid(string original, string replacement) {
        File.WriteAllText(path, sample.Replace(original, replacement, StringComparison.Ordinal));

        ConfigInvalidException invalid = Assert.Throws<ConfigInvalidException>(() => ConfigFile.Load(path, home));

        Assert.DoesNotContain(secret, invalid.ToString(), StringComparison.Ordinal);
    }

    private static string TestData(string name) =>
        File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "TestData", "Config", name));

    private static AppConfig Full() => new() {
        Profile = new ProfileRef("/home/user/.config/jobranger/profile.yaml", "cv.pdf", "2026-10-01T08:00:00+00:00"),
        Targets = new Targets {
            Subfields = ["backend", "ml_ai"],
            Limits = new Limits { Levels = ["junior", "senior"], Years = ["6+"], HideMasters = false },
            Places = ["Bergen", "Ålesund"],
            Relocate = false,
        },
        Sources = new SourcesConfig {
            Nav = false,
            Employers = false,
            Finn = true,
            LinkedIn = true,
            EmployerFeeds =
                [new EmployerFeed("ashby", "employer", "Employer: Oslo", "https://jobs.example.test/employer")],
        },
        Schedule = new ScheduleConfig { Times = ["06:30", "18:45"] },
        Model = new ModelConfig {
            Provider = ProviderName.OpenRouter,
            Model = "provider/model-1",
            OllamaUrl = "https://box:11434",
            MonthlyLimitUsd = 12.5m,
            Price = new PriceOverride(0.8m, 4m),
        },
        Notify = new NotifyConfig {
            Website = false,
            Email = new EmailConfig {
                On = true,
                Address = "user@bø.example",
                Via = EmailVia.Resend,
                SmtpUrl = "smtps://smtp.example.test",
                SmtpUser = "user",
                Sender = "jobranger <user@example.test>",
            },
        },
        Language = Language.Nb,
    };

    private static string Describe(AppConfig config) => string.Join(
        " | ",
        config with {
            Targets = null!,
            Sources = null!,
            Schedule = null!,
        },
        string.Join(",", config.Targets.Subfields),
        string.Join(",", config.Targets.Limits.Levels),
        string.Join(",", config.Targets.Limits.Years),
        config.Targets.Limits.HideMasters,
        string.Join(",", config.Targets.Places),
        config.Targets.Relocate,
        config.Sources with { EmployerFeeds = [] },
        string.Join(",", config.Sources.EmployerFeeds),
        string.Join(",", config.Schedule.Times));
}
