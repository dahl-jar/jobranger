using Jobranger.Cli.Config;

namespace Jobranger.Cli.Features.Setup;

internal sealed record InitState(AppConfig Config, IReadOnlyList<KeyValuePair<string, string>> Secrets) {
    public static InitState Empty { get; } = new(new AppConfig { Profile = new ProfileRef(string.Empty) }, []);

    public InitState WithSecret(string name, string value) => this with { Secrets = [.. Secrets, new(name, value)] };

    public override string ToString() => nameof(InitState);
}
