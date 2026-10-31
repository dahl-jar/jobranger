using Jobranger.Cli.Config;
using Jobranger.Cli.Features.Matching;
using Jobranger.Cli.Features.Profiles;
using Jobranger.Cli.Files;
using MatchingEntry = Jobranger.Cli.Features.Matching.Matching;
using ProfilesEntry = Jobranger.Cli.Features.Profiles.Profiles;

namespace Jobranger.Cli.Features.Runs;

internal sealed record PromptSetup(
    string System, string Version, IReadOnlySet<string> Evidence, IReadOnlySet<string> Education) {
    public static PromptSetup Load(AppConfig config, Paths paths) {
        Profile profile = ProfilesEntry.Load(config.Profile.Path);
        UserContext context = ProfilesEntry.LoadContext(paths.ContextPath());
        IReadOnlySet<string> evidence = ProfilesEntry.CandidateEvidence(profile, context, paths.ContextPath());
        GuidanceText guidance = MatchingEntry.LoadGuidance(paths.GuidancePath());
        string system = MatchingEntry.BuildSystem(
            guidance.Text,
            MatchingEntry.LimitRules(config.Targets.Limits),
            ProfilesEntry.Text(profile),
            ProfilesEntry.ContextText(context));
        return new PromptSetup(
            system, MatchingEntry.AssessmentVersion(system), evidence, ProfilesEntry.EducationIds(profile));
    }
}
