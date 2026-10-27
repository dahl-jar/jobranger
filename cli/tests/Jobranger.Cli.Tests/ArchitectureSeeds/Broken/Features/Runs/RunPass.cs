using Jobranger.Cli.Tests.ArchitectureSeeds.Broken.Features.Ads;

namespace Jobranger.Cli.Tests.ArchitectureSeeds.Broken.Features.Runs;

internal static class RunPass {
    public static int Run() => new AdStore().Count();
}
