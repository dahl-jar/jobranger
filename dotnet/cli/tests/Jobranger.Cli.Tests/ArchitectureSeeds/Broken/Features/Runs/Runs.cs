namespace Jobranger.Cli.Tests.ArchitectureSeeds.Broken.Features.Runs;

internal static class Runs {
    public static int Start() => RunPass.Run();

    public static int Deliver() => Delivery.Delivery.Send();

    public static int Configure() => Setup.Setup.Begin();
}
