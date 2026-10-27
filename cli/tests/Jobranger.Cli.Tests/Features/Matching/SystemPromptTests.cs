using System.Text.Json;
using Jobranger.Cli.Config;
using Jobranger.Cli.Features.Profiles;
using Jobranger.Cli.Tests.Mocks;
using MatchingEntry = Jobranger.Cli.Features.Matching.Matching;
using ProfilesEntry = Jobranger.Cli.Features.Profiles.Profiles;

namespace Jobranger.Cli.Tests.Features.Matching;

public sealed class SystemPromptTests {
    private readonly string profileText =
        ProfilesEntry.Text(ProfilesEntry.Load(MatchingFixtures.TestData("profile_user.yaml")));

    [Theory]
    [InlineData("narrow")]
    [InlineData("wide")]
    [InlineData("lead")]
    [InlineData("none")]
    public void ShouldEqualGolden_WhenGuidanceIsEmpty(string variant) {
        string limits = MatchingEntry.LimitRules(MatchingFixtures.LimitVariants[variant]);

        string system = MatchingEntry.BuildSystem(string.Empty, limits, profileText);

        Assert.Equal(MatchingFixtures.GoldenText("Prompts", $"system-empty-{variant}.txt"), system);
    }

    [Theory]
    [InlineData("plain", "narrow")]
    [InlineData("plain", "wide")]
    [InlineData("accents", "narrow")]
    [InlineData("accents", "wide")]
    public void ShouldEqualGolden_WhenGuidanceIsEdited(string guidance, string variant) {
        string limits = MatchingEntry.LimitRules(MatchingFixtures.LimitVariants[variant]);

        string system = MatchingEntry.BuildSystem(MatchingFixtures.Guidances[guidance], limits, profileText);

        Assert.Equal(MatchingFixtures.GoldenText("Prompts", $"system-{guidance}-{variant}.txt"), system);
    }

    [Theory]
    [InlineData("system-empty-narrow")]
    [InlineData("system-accents-wide")]
    public void ShouldEqualGoldenVersion_WhenHashingSystem(string name) {
        using JsonDocument versions = JsonDocument.Parse(MatchingFixtures.GoldenText("Prompts", "versions.json"));

        string version = MatchingEntry.AssessmentVersion(MatchingFixtures.GoldenText("Prompts", name + ".txt"));

        Assert.Equal(versions.RootElement.GetProperty(name).GetString(), version);
    }
}
