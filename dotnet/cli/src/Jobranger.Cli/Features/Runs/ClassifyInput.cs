using Jobranger.Cli.Config;

namespace Jobranger.Cli.Features.Runs;

internal sealed record ClassifyInput(RunModel? Model, PromptSetup Prompt, AppConfig Config, DateOnly Today);
