namespace Jobranger.Cli.Tests.ArchitectureSeeds.Sound.Commands;

internal static class RunCommand {
    public static string Invoke() => Features.Delivery.Delivery.Send() + Features.Ads.Ads.First().Title;
}
