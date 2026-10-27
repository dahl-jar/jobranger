using Jobranger.Cli.Config;
using Jobranger.Cli.Tests.Mocks;
using ProfilesEntry = Jobranger.Cli.Features.Profiles.Profiles;

namespace Jobranger.Cli.Tests.Features.Profiles;

public sealed class ProfileParityTests : IDisposable {
    private const string profileName = "profile.yaml";
    private const string evidence = "summary: s\nprofessional_years: 1\nprojects:\n  - id: abc\n    name: n\n    facts: ";

    private readonly TempDir temp = new();

    public void Dispose() => temp.Dispose();

    [Theory]
    [InlineData(evidence + "Built X.\n      \u0085Led Y.\n", "7")]
    [InlineData(evidence + "'Built X.\n      \u0085Led Y.'\n", "7")]
    [InlineData(evidence + "'Built X.\u2028\t\n      Led Y.'\n", "6")]
    public void ShouldNameSeparatorLine_WhenValueWithSeparatorWraps(string yaml, string line) {
        AssertNotYaml(yaml, line);
    }

    [Theory]
    [InlineData("summary: Line one\n  Built X\u2028#1 team\n  line three\nprofessional_years: 1\n", "2")]
    [InlineData("summary: a\u2028\n\tb: 1\nprofessional_years: 1\n", "2")]
    [InlineData("professional_years: 1\nsummary: a\u2028b\n- x\n", "3")]
    public void ShouldNameLine_WhenFileWithSeparatorFailsToParse(string yaml, string line) {
        AssertNotYaml(yaml, line);
    }

    private void AssertNotYaml(string yaml, string line) {
        string path = temp.File(profileName);
        File.WriteAllText(path, yaml);

        ConfigInvalidException error = Assert.Throws<ConfigInvalidException>(() => ProfilesEntry.Load(path));

        Assert.Equal(
            [(profileName, ConfigProblem.NotYaml, line)],
            error.Errors.Select(item => (item.Location, item.Problem, item.Detail)));
    }
}
