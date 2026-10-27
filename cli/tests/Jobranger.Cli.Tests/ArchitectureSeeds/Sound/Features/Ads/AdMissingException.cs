namespace Jobranger.Cli.Tests.ArchitectureSeeds.Sound.Features.Ads;

public sealed class AdMissingException : Exception {
    public AdMissingException() {
    }

    public AdMissingException(string message) : base(message) {
    }

    public AdMissingException(string message, Exception innerException) : base(message, innerException) {
    }
}
