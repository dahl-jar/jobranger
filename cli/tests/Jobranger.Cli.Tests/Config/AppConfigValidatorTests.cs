using Jobranger.Cli.Config;

namespace Jobranger.Cli.Tests.Config;

public sealed class AppConfigValidatorTests {
    private static readonly AppConfig valid = new() { Profile = new ProfileRef("/home/user/profile.yaml") };

    [Theory]
    [InlineData("23:30", "01:00")]
    [InlineData("10:00", "11:59")]
    public void ShouldRejectRunTimes_WhenUnderTwoHoursApart(string first, string second) {
        AppConfig config = valid with { Schedule = new ScheduleConfig { Times = [first, second] } };

        IReadOnlyList<ConfigError> errors = AppConfigValidator.Validate(config);

        Assert.Equal([new ConfigError("schedule.times", ConfigProblem.RunTimesTooClose, "2")], errors);
    }

    [Theory]
    [InlineData("10:00", "12:00")]
    [InlineData("23:00", "01:00")]
    public void ShouldAcceptRunTimes_WhenTwoHoursApart(string first, string second) {
        AppConfig config = valid with { Schedule = new ScheduleConfig { Times = [first, second] } };

        IReadOnlyList<ConfigError> errors = AppConfigValidator.Validate(config);

        Assert.Empty(errors);
    }

    [Fact]
    public void ShouldAcceptSchedule_WhenNoRunTimeIsSet() {
        AppConfig config = valid with { Schedule = new ScheduleConfig { Times = [] } };

        IReadOnlyList<ConfigError> errors = AppConfigValidator.Validate(config);

        Assert.Empty(errors);
    }

    [Theory]
    [InlineData("25:00")]
    [InlineData("9:00")]
    [InlineData("12:60")]
    [InlineData("12:00 ")]
    [InlineData("12:00\n")]
    public void ShouldRejectRunTime_WhenNotHhMm(string time) {
        AppConfig config = valid with { Schedule = new ScheduleConfig { Times = ["03:00", time] } };

        IReadOnlyList<ConfigError> errors = AppConfigValidator.Validate(config);

        Assert.Equal([new ConfigError("schedule.times.1", ConfigProblem.RunTimeFormat)], errors);
    }

    [Theory]
    [InlineData(-0.01, 1, 1, "model.monthly_limit_usd")]
    [InlineData(1, -1, 1, "model.price.input_per_mtok")]
    [InlineData(1, 1, 1_000_000.01, "model.price.output_per_mtok")]
    public void ShouldRejectAmount_WhenOutOfRange(double limit, double input, double output, string location) {
        AppConfig config = valid with {
            Model = new ModelConfig {
                MonthlyLimitUsd = (decimal)limit,
                Price = new PriceOverride((decimal)input, (decimal)output),
            },
        };

        IReadOnlyList<ConfigError> errors = AppConfigValidator.Validate(config);

        Assert.Equal([new ConfigError(location, ConfigProblem.AmountOutOfRange, "1000000")], errors);
    }

    [Fact]
    public void ShouldAcceptAmounts_WhenOnRangeEdges() {
        AppConfig config = valid with {
            Model = new ModelConfig { MonthlyLimitUsd = 0, Price = new PriceOverride(0, 1_000_000) },
        };

        IReadOnlyList<ConfigError> errors = AppConfigValidator.Validate(config);

        Assert.Empty(errors);
    }

    [Theory]
    [InlineData("ftp://box:11434")]
    [InlineData("http://")]
    public void ShouldRejectOllamaUrl_WhenNotHttpWithHost(string url) {
        AppConfig config = valid with { Model = new ModelConfig { OllamaUrl = url } };

        IReadOnlyList<ConfigError> errors = AppConfigValidator.Validate(config);

        Assert.Equal([new ConfigError("model.ollama_url", ConfigProblem.NotOllamaUrl)], errors);
    }

    [Fact]
    public void ShouldAcceptOllamaUrl_WhenItIsHttps() {
        AppConfig config = valid with { Model = new ModelConfig { OllamaUrl = "https://box:11434" } };

        IReadOnlyList<ConfigError> errors = AppConfigValidator.Validate(config);

        Assert.Empty(errors);
    }

    [Theory]
    [InlineData(null, "resend", null, nameof(ConfigProblem.AddressMissing))]
    [InlineData("user@example.com", "smtp", null, nameof(ConfigProblem.SmtpUrlMissing))]
    public void ShouldReportMissingField_WhenEmailIsOn(string? address, string via, string? smtpUrl, string problem) {
        AppConfig config = valid with { Notify = new NotifyConfig { Email = Email(address, via, smtpUrl) } };

        IReadOnlyList<ConfigError> errors = AppConfigValidator.Validate(config);

        Assert.Equal([new ConfigError("notify.email", Enum.Parse<ConfigProblem>(problem))], errors);
    }

    [Fact]
    public void ShouldAcceptResendEmail_WhenSmtpUrlIsMissing() {
        EmailConfig email = Email("user@example.com", "resend", null);
        AppConfig config = valid with { Notify = new NotifyConfig { Email = email } };

        IReadOnlyList<ConfigError> errors = AppConfigValidator.Validate(config);

        Assert.Empty(errors);
    }

    [Theory]
    [InlineData("user@", null, null, "notify.email.address", nameof(ConfigProblem.NotAddress))]
    [InlineData(null, "a@b.test, c@d.test", null, "notify.email.sender", nameof(ConfigProblem.NotSender))]
    [InlineData(null, null, "smtp://user@smtp.example.test", "notify.email.smtp_url", nameof(ConfigProblem.NotSmtpUrl))]
    public void ShouldRejectEmailField_WhenValueIsInvalid(
        string? address, string? sender, string? smtpUrl, string location, string problem) {
        EmailConfig email = new() { Address = address, Sender = sender, SmtpUrl = smtpUrl };
        AppConfig config = valid with { Notify = new NotifyConfig { Email = email } };

        IReadOnlyList<ConfigError> errors = AppConfigValidator.Validate(config);

        Assert.Equal([new ConfigError(location, Enum.Parse<ConfigProblem>(problem))], errors);
    }

    private static EmailConfig Email(string? address, string via, string? smtpUrl) => new() {
        On = true,
        Address = address,
        Via = via == "smtp" ? EmailVia.Smtp : EmailVia.Resend,
        SmtpUrl = smtpUrl,
    };
}
