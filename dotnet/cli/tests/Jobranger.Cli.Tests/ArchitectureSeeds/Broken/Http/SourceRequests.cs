using Jobranger.Cli.Tests.ArchitectureSeeds.Broken.Features.Ads;

namespace Jobranger.Cli.Tests.ArchitectureSeeds.Broken.Http;

internal static class SourceRequests {
    public static string Send(Ad ad) => ad.Title;
}
