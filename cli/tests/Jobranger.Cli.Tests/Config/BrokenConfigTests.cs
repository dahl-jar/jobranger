using Jobranger.Cli.Config;
using Jobranger.Cli.Tests.Mocks;

namespace Jobranger.Cli.Tests.Config;

public sealed class BrokenConfigTests {
    private const string broken = "Broken";

    [Theory]
    [InlineData("01-unterminated-quote.yaml", "01-unterminated-quote.yaml", nameof(ConfigProblem.NotYaml))]
    [InlineData("02-unterminated-flow.yaml", "02-unterminated-flow.yaml", nameof(ConfigProblem.NotYaml))]
    [InlineData("03-unterminated-flowmap.yaml", "03-unterminated-flowmap.yaml", nameof(ConfigProblem.NotYaml))]
    [InlineData("04-duplicate.yaml", "model.model", nameof(ConfigProblem.DuplicateKey))]
    [InlineData("05-alias.yaml", "notify.email.smtp_user", nameof(ConfigProblem.AliasUsed))]
    [InlineData("06-merge-key.yaml", "notify.<<", nameof(ConfigProblem.AliasUsed))]
    [InlineData("07-latin1.yaml", "07-latin1.yaml", nameof(ConfigProblem.NotUtf8))]
    [InlineData("08-bool-wrong.yaml", "sources.nav", nameof(ConfigProblem.NotBoolean))]
    [InlineData("09-number-wrong.yaml", "model.monthly_limit_usd", nameof(ConfigProblem.NotNumber))]
    [InlineData("10-provider-choice.yaml", "model.provider", nameof(ConfigProblem.NotChoice))]
    [InlineData("11-role-choice.yaml", "targets.subfields.1", nameof(ConfigProblem.NotChoice))]
    [InlineData("12-via-choice.yaml", "notify.email.via", nameof(ConfigProblem.NotChoice))]
    [InlineData("13-smtp-userinfo.yaml", "notify.email.smtp_url", nameof(ConfigProblem.NotSmtpUrl))]
    [InlineData("14-address.yaml", "notify.email.address", nameof(ConfigProblem.NotAddress))]
    [InlineData("15-ollama.yaml", "model.ollama_url", nameof(ConfigProblem.NotOllamaUrl))]
    [InlineData("16-time.yaml", "schedule.times.0", nameof(ConfigProblem.RunTimeFormat))]
    [InlineData("17-text-is-list.yaml", "model.model", nameof(ConfigProblem.NotText))]
    [InlineData("18-section-is-text.yaml", "targets", nameof(ConfigProblem.NotSection))]
    [InlineData("19-root-scalar.yaml", "19-root-scalar.yaml", nameof(ConfigProblem.NotSection))]
    [InlineData("20-root-list.yaml", "20-root-list.yaml", nameof(ConfigProblem.NotSection))]
    [InlineData("21-empty.yaml", "21-empty.yaml", nameof(ConfigProblem.NotSection))]
    [InlineData("22-unknown-key.yaml", "unknown_key", nameof(ConfigProblem.UnknownKey))]
    [InlineData("23-multi-doc.yaml", "23-multi-doc.yaml", nameof(ConfigProblem.NotYaml))]
    [InlineData("24-version.yaml", "version", nameof(ConfigProblem.NotChoice))]
    [InlineData("25-tab-indent.yaml", "25-tab-indent.yaml", nameof(ConfigProblem.NotYaml))]
    [InlineData("26-no-profile.yaml", "profile", nameof(ConfigProblem.Required))]
    [InlineData("28-price-missing.yaml", "model.price.output_per_mtok", nameof(ConfigProblem.Required))]
    [InlineData("29-language.yaml", "language", nameof(ConfigProblem.NotChoice))]
    [InlineData("30-amount-neg.yaml", "model.monthly_limit_usd", nameof(ConfigProblem.AmountOutOfRange))]
    [InlineData("31-complex-key.yaml", "31-complex-key.yaml", nameof(ConfigProblem.NotYaml))]
    [InlineData("32-feed-kind.yaml", "sources.employer_feeds.0.kind", nameof(ConfigProblem.NotChoice))]
    [InlineData("35-directive-late.yaml", "35-directive-late.yaml", nameof(ConfigProblem.NotYaml))]
    [InlineData("36-sender-two.yaml", "notify.email.sender", nameof(ConfigProblem.NotSender))]
    [InlineData("37-nul-byte.yaml", "37-nul-byte.yaml", nameof(ConfigProblem.NotYaml))]
    public void ShouldReportLocation_WhenConfigFileIsBroken(string file, string location, string problem) {
        string path = ConfigFixtures.ConfigTestData(broken, file);

        ConfigInvalidException invalid = Assert.Throws<ConfigInvalidException>(
            () => ConfigFile.Load(path, ConfigFixtures.Home));

        ConfigError first = invalid.Errors[0];
        Assert.Equal((location, Enum.Parse<ConfigProblem>(problem)), (first.Location, first.Problem));
    }

    [Theory]
    [InlineData("01-unterminated-quote.yaml")]
    [InlineData("02-unterminated-flow.yaml")]
    [InlineData("03-unterminated-flowmap.yaml")]
    [InlineData("04-duplicate.yaml")]
    [InlineData("05-alias.yaml")]
    [InlineData("08-bool-wrong.yaml")]
    [InlineData("09-number-wrong.yaml")]
    [InlineData("10-provider-choice.yaml")]
    [InlineData("11-role-choice.yaml")]
    [InlineData("12-via-choice.yaml")]
    [InlineData("13-smtp-userinfo.yaml")]
    [InlineData("14-address.yaml")]
    [InlineData("15-ollama.yaml")]
    [InlineData("16-time.yaml")]
    [InlineData("17-text-is-list.yaml")]
    [InlineData("18-section-is-text.yaml")]
    [InlineData("19-root-scalar.yaml")]
    [InlineData("20-root-list.yaml")]
    [InlineData("22-unknown-key.yaml")]
    [InlineData("23-multi-doc.yaml")]
    [InlineData("25-tab-indent.yaml")]
    [InlineData("29-language.yaml")]
    [InlineData("31-complex-key.yaml")]
    [InlineData("32-feed-kind.yaml")]
    [InlineData("35-directive-late.yaml")]
    [InlineData("36-sender-two.yaml")]
    public void ShouldLeaveValueOutOfError_WhenConfigFileIsBroken(string file) {
        string path = ConfigFixtures.ConfigTestData(broken, file);

        ConfigInvalidException invalid = Assert.Throws<ConfigInvalidException>(
            () => ConfigFile.Load(path, ConfigFixtures.Home));

        Assert.DoesNotContain(ConfigFixtures.Secret, invalid.ToString(), StringComparison.Ordinal);
    }
}
