using System.Globalization;
using System.Runtime.Versioning;
using System.Text;
using Jobranger.Cli.Config;
using Jobranger.Cli.Features.Budget;
using Jobranger.Cli.Features.Models;
using Jobranger.Cli.Features.Providers;
using Jobranger.Cli.Http;
using Jobranger.Cli.Platform;
using Jobranger.Cli.Text;
using BudgetEntry = Jobranger.Cli.Features.Budget.Budget;
using ModelsEntry = Jobranger.Cli.Features.Models.Models;
using ProvidersEntry = Jobranger.Cli.Features.Providers.Providers;
using RunsEntry = Jobranger.Cli.Features.Runs.Runs;

namespace Jobranger.Cli.Features.Setup;

[UnsupportedOSPlatform("windows")]
internal sealed class ModelStep(SetupContext context, SetupSecrets secrets) : ISetupStep {
    private const string keyCheckModel = "key-check";
    private const char decimalComma = ',';
    private const char decimalPoint = '.';

    private readonly IMessages messages = context.Messages;

    public string Title => messages.InitModelTitle;

    public async Task<InitState> RunAsync(InitState state, CancellationToken cancellationToken) {
        ProviderName provider = await AskProviderAsync(cancellationToken).ConfigureAwait(false);
        Listing listing = await ListModelsAsync(provider, cancellationToken).ConfigureAwait(false);
        await context.Output.WriteLineAsync(messages.InitCostBasis(BudgetEntry.DefaultAdsPerDay)).ConfigureAwait(false);
        string modelId = await context.Prompter.SelectAsync(
            messages.InitModel,
            [.. listing.Models.Select(model => Row(provider, model))],
            cancellationToken).ConfigureAwait(false);
        PriceOverride? price = await PriceAsync(provider, listing.Models.First(model => model.Id == modelId), cancellationToken)
            .ConfigureAwait(false);
        ModelConfig model = new() {
            Provider = provider,
            Model = modelId,
            OllamaUrl = listing.OllamaUrl,
            Price = price,
            MonthlyLimitUsd = provider == ProviderName.Ollama
                ? null
                : await AskAmountAsync(messages.InitMonthlyLimit, false, cancellationToken).ConfigureAwait(false),
        };
        AppConfig config = state.Config with { Model = model };
        if (BudgetEntry.UnpricedLimit(config, PricesOf(provider, modelId, price))) {
            await context.Output.WriteLineAsync(messages.UnpricedLimitWarning).ConfigureAwait(false);
        }
        InitState next = state with { Config = config };
        return ProvidersEntry.SecretName(provider) is string name && listing.Key is string key ? next.WithSecret(name, key) : next;
    }

    private static PriceOverride? Listed(ProviderName provider, ModelInfo model) =>
        ModelsEntry.SpecFor(provider, model.Id, null) is null
        && model.PriceIn is decimal input
        && model.PriceOut is decimal output
            ? new PriceOverride(input, output)
            : null;

    private static ModelPrices? PricesOf(ProviderName provider, string modelId, PriceOverride? price) =>
        ModelsEntry.SpecFor(provider, modelId, price) is ModelSpec spec ? RunsEntry.ToPrices(spec) : null;

    private static decimal? Amount(string text) =>
        decimal.TryParse(
            text.Replace(decimalComma, decimalPoint),
            NumberStyles.AllowDecimalPoint,
            CultureInfo.InvariantCulture,
            out decimal amount) && amount <= AppConfigValidator.MaxAmountUsd
            ? amount
            : null;

    private static string Printable(string name) {
        StringBuilder kept = new(name.Length);
        foreach (int code in CodePoints.Of(name).Where(PythonUnicode.IsPrintable)) {
            CodePoints.Append(kept, code);
        }
        return kept.ToString();
    }

    private async Task<ProviderName> AskProviderAsync(CancellationToken cancellationToken) {
        PromptChoice[] choices = [
            .. Enum.GetValues<ProviderName>().Select(provider => new PromptChoice(WireNames.Of(provider), provider.ToString())),
        ];
        string answer = await context.Prompter.SelectAsync(messages.InitProvider, choices, cancellationToken)
            .ConfigureAwait(false);
        return WireNames.Parse<ProviderName>(answer);
    }

    private async Task<Listing> ListModelsAsync(ProviderName provider, CancellationToken cancellationToken) {
        while (true) {
            (string? key, string ollamaUrl) = await AskCredentialAsync(provider, cancellationToken).ConfigureAwait(false);
            IReadOnlyList<ModelInfo>? models = await TryListAsync(provider, key, ollamaUrl, cancellationToken)
                .ConfigureAwait(false);
            if (models is not null) {
                return new Listing(key, ollamaUrl, models);
            }
        }
    }

    private async Task<(string? Key, string OllamaUrl)> AskCredentialAsync(
        ProviderName provider, CancellationToken cancellationToken) {
        string defaultUrl = new ModelConfig().OllamaUrl;
        if (ProvidersEntry.SecretName(provider) is string name) {
            return (await secrets.AskAsync(name, messages.InitApiKey, cancellationToken).ConfigureAwait(false), defaultUrl);
        }
        return (null, await context.Prompter.TextAsync(messages.InitOllamaUrl, defaultUrl, cancellationToken)
            .ConfigureAwait(false));
    }

    private async Task<IReadOnlyList<ModelInfo>?> TryListAsync(
        ProviderName provider, string? key, string ollamaUrl, CancellationToken cancellationToken) {
        string label = provider.ToString();
        try {
            IReadOnlyList<ModelInfo> models = await new ModelsEntry(() => context.Seams.CreateModelHandler(provider), context.Time)
                .ListAsync(provider, key, ollamaUrl, cancellationToken).ConfigureAwait(false);
            if (provider == ProviderName.OpenRouter
                && !await KeyWorksAsync(provider, key, cancellationToken).ConfigureAwait(false)) {
                await WriteRejectionAsync(provider, ollamaUrl).ConfigureAwait(false);
                return null;
            }
            if (models.Count == 0) {
                await context.Output.WriteLineAsync(messages.InitNoModels(label)).ConfigureAwait(false);
                await ConfirmRetryAsync(cancellationToken).ConfigureAwait(false);
                return null;
            }
            await context.Output.WriteLineAsync(key is null ? messages.InitOllamaAnswered : messages.InitKeyWorks)
                .ConfigureAwait(false);
            return models;
        } catch (Exception error) when (error is ModelSetupException
            or ApiException { Kind: ApiErrorKind.Auth or ApiErrorKind.BadRequest or ApiErrorKind.NotFound }) {
            await WriteRejectionAsync(provider, ollamaUrl).ConfigureAwait(false);
            return null;
        } catch (ApiException) {
            await context.Output.WriteLineAsync(messages.InitCouldNotReach(label)).ConfigureAwait(false);
            await ConfirmRetryAsync(cancellationToken).ConfigureAwait(false);
            return null;
        }
    }

    private Task WriteRejectionAsync(ProviderName provider, string ollamaUrl) =>
        context.Output.WriteLineAsync(
            provider == ProviderName.Ollama ? messages.InitOllamaRefused(ollamaUrl) : messages.InitKeyRejected(provider.ToString()));

    private async Task<bool> KeyWorksAsync(ProviderName provider, string? key, CancellationToken cancellationToken) {
        using IModelClient client = await new ProvidersEntry(() => context.Seams.CreateModelHandler(provider), context.Time)
            .CreateAsync(
                new ModelConfig { Provider = provider, Model = keyCheckModel },
                (_, _) => Task.FromResult(key),
                false,
                cancellationToken).ConfigureAwait(false);
        KeyCheck check = await client.CheckKeyAsync(cancellationToken).ConfigureAwait(false);
        return check.Ok;
    }

    private async Task ConfirmRetryAsync(CancellationToken cancellationToken) {
        if (!await context.Prompter.ConfirmAsync(messages.InitTryAgain, true, cancellationToken).ConfigureAwait(false)) {
            throw new SetupCancelledException();
        }
    }

    private PromptChoice Row(ProviderName provider, ModelInfo model) {
        ModelPrices? prices = PricesOf(provider, model.Id, Listed(provider, model));
        decimal? estimate = BudgetEntry.EstimateDailyCost(prices, BudgetEntry.DefaultAdsPerDay, model.Batch);
        string name = Printable(model.Name);
        string label = estimate is decimal usd
            ? messages.InitModelRow(name, usd)
            : messages.InitModelRowNoPrice(name);
        return new PromptChoice(model.Id, label);
    }

    private async Task<PriceOverride?> PriceAsync(ProviderName provider, ModelInfo model, CancellationToken cancellationToken) {
        if (Listed(provider, model) is PriceOverride listed) {
            return listed;
        }
        if (ModelsEntry.SpecFor(provider, model.Id, null) is not null
            || await AskAmountAsync(messages.InitPriceInput, false, cancellationToken).ConfigureAwait(false) is not decimal input) {
            return null;
        }
        decimal? output = await AskAmountAsync(messages.InitPriceOutput, true, cancellationToken).ConfigureAwait(false);
        return new PriceOverride(input, output.GetValueOrDefault());
    }

    private async Task<decimal?> AskAmountAsync(string message, bool required, CancellationToken cancellationToken) {
        while (true) {
            string answer = await context.Prompter.TextAsync(message, null, cancellationToken).ConfigureAwait(false);
            if (answer.Length == 0 && !required) {
                return null;
            }
            if (Amount(answer) is decimal amount) {
                return amount;
            }
            await context.Output.WriteLineAsync(messages.InitNotAmount).ConfigureAwait(false);
        }
    }

    private sealed record Listing(string? Key, string OllamaUrl, IReadOnlyList<ModelInfo> Models);
}
