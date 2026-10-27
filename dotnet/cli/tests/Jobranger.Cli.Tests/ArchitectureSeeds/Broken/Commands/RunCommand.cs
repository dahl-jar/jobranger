using Jobranger.Cli.Tests.ArchitectureSeeds.Broken.Features.Ads;

namespace Jobranger.Cli.Tests.ArchitectureSeeds.Broken.Commands;

internal static class RunCommand {
    public static int Invoke() => new AdStore().Count();
}
