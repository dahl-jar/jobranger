namespace Jobranger.Cli.Features.Models;

internal sealed record ModelInfo(string Id, string Name, decimal? PriceIn, decimal? PriceOut, bool Batch);
