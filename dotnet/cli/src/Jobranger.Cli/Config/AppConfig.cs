using Jobranger.Cli.Text;

namespace Jobranger.Cli.Config;

internal sealed record AppConfig {
    public required ProfileRef Profile { get; init; }

    public Targets Targets { get; init; } = new();

    public SourcesConfig Sources { get; init; } = new();

    public ScheduleConfig Schedule { get; init; } = new();

    public ModelConfig Model { get; init; } = new();

    public NotifyConfig Notify { get; init; } = new();

    public Language? Language { get; init; }
}
