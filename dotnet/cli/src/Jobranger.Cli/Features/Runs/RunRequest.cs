using Jobranger.Cli.Config;

namespace Jobranger.Cli.Features.Runs;

internal sealed record RunRequest(AppConfig Config, IRunEvents Events);
