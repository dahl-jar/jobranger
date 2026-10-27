using Jobranger.Cli.Tests.ArchitectureSeeds.Sound.Http;

namespace Jobranger.Cli.Tests.ArchitectureSeeds.Sound.Features.Ads;

internal static class Ads {
    public static Ad First() {
        AdStore store = new();
        return store.Count() < 0 ? throw new AdMissingException() : new Ad(SourceRequests.Send(), AdKind.Job);
    }

    public static AdPosition Position() => new(0);
}
