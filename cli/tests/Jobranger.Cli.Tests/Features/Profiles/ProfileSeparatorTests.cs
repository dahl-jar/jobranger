using Jobranger.Cli.Features.Profiles;
using Jobranger.Cli.Tests.Mocks;
using ProfilesEntry = Jobranger.Cli.Features.Profiles.Profiles;

namespace Jobranger.Cli.Tests.Features.Profiles;

public sealed class ProfileSeparatorTests : IDisposable {
    internal const string FactsProfile = "summary: s\nprofessional_years: 1\nprojects:\n  - id: abc\n    name: n\n    facts: ";

    private readonly TempDir temp = new();

    public void Dispose() => temp.Dispose();

    [Theory]
    [InlineData("Built X.\u2028Led Y.", "Built X.\u2028Led Y.")]
    [InlineData("\"Built X.\u2028Led Y.\"", "Built X.\u2028Led Y.")]
    [InlineData("Built X.\u2029Led Y.", "Built X.\u2029Led Y.")]
    [InlineData("Built X.\u0085Led Y.", "Built X. Led Y.")]
    public void ShouldReadFactsLikePython_WhenTextHasUnicodeSeparator(string facts, string expected) {
        string path = temp.File("profile.yaml");
        File.WriteAllText(path, FactsProfile + facts + "\n");

        Profile loaded = ProfilesEntry.Load(path);

        Assert.Equal(expected, loaded.Projects[0].Facts);
    }
}
