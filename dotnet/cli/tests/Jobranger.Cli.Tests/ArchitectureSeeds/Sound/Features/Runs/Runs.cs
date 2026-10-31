using Jobranger.Cli.Tests.ArchitectureSeeds.Sound.Features.Ads;

namespace Jobranger.Cli.Tests.ArchitectureSeeds.Sound.Features.Runs;

internal static class Runs {
    public static string Start() {
        Ad ad = Ads.Ads.First();
        AdPosition position = Ads.Ads.Position();
        return ad.Title + position.Index + RunClock.Tick();
    }

    public static string Label(AdKind kind) => kind.ToString();

    public static string Explain(AdMissingException missing) => missing.Message;

    public static int Count(IAdSource source) => source.Count();
}
