using Jobranger.Cli.Tests.ArchitectureSeeds.Broken.Features.Ads;

namespace Jobranger.Cli.Tests.ArchitectureSeeds.Broken.Features.Matching;

internal static class Matching {
    public static string BuildPrompt(Ad ad) => ad.Title;

    public static int Configure(BrokenServices services) => services.Count;
}
