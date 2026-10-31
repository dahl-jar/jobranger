using System.Text.Json.Nodes;
using Jobranger.Cli.Features.Budget;
using Jobranger.Cli.Features.Providers;

namespace Jobranger.Cli.Features.Runs;

internal sealed record RunModel(IModelClient Client, JsonObject Schema, ModelPrices? Prices);
