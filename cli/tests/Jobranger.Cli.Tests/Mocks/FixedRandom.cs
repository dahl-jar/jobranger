namespace Jobranger.Cli.Tests.Mocks;

internal sealed class FixedRandom(double value) : Random {
    public override double NextDouble() => value;
}
