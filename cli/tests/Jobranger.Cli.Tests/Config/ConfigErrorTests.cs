using Jobranger.Cli.Config;
using Jobranger.Cli.Tests.Mocks;
using Jobranger.Cli.Text;

namespace Jobranger.Cli.Tests.Config;

public sealed class ConfigErrorTests {
    [Theory]
    [InlineData(Locales.English)]
    [InlineData(Locales.Norwegian)]
    public void ShouldDescribeEveryProblem_WhenLanguageIsChosen(string lang) {
        IMessages messages = Messages.For(null, Locales.EnvironmentWithoutConfig(lang));

        string[] blank = [.. Enum.GetValues<ConfigProblem>()
            .Where(problem => new ConfigError("model", problem, "1").Describe(messages).Length <= "model: ".Length)
            .Select(problem => problem.ToString())];

        Assert.Empty(blank);
    }

    [Fact]
    public void ShouldNameLocationAndChoices_WhenValueIsNotAllowed() {
        ConfigError error = new("model.provider", ConfigProblem.NotChoice, "anthropic, openai");

        string text = error.Describe(new EnglishMessages());

        Assert.Equal("model.provider: is not one of: anthropic, openai", text);
    }

    [Theory]
    [InlineData(
        "schedule.times", nameof(ConfigProblem.RunTimesTooClose), "2", "schedule.times: are less than 2 hours apart")]
    [InlineData(
        "model.monthly_limit_usd", nameof(ConfigProblem.AmountOutOfRange), "1000000",
        "model.monthly_limit_usd: is not between 0 and 1000000")]
    [InlineData("docs", nameof(ConfigProblem.TooMany), "10", "docs: has more than 10 entries")]
    public void ShouldNameLimit_WhenValueBreaksLimit(string location, string problem, string limit, string expected) {
        ConfigError error = new(location, Enum.Parse<ConfigProblem>(problem), limit);

        string text = error.Describe(new EnglishMessages());

        Assert.Equal(expected, text);
    }
}
