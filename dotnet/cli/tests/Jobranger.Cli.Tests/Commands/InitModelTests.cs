using System.Runtime.Versioning;
using Jobranger.Cli.Config;
using Jobranger.Cli.Tests.Mocks;
using Jobranger.Cli.Text;

namespace Jobranger.Cli.Tests.Commands;

[UnsupportedOSPlatform("windows")]
public sealed class InitModelTests : IDisposable {
    private const string anthropicSecret = "anthropic_api_key";

    private static readonly EnglishMessages english = new();

    private readonly InitHarness init = new();

    private object[] RouterFlow =>
        [InitFixtures.OpenRouter, InitFixtures.ApiKey, InitFixtures.RouterModel, string.Empty, .. init.AfterModel];

    public void Dispose() => init.Dispose();

    [Fact]
    public async Task ShouldExit130_WhenUserDeclinesRetry() {
        ScriptedPrompter prompter = init.Prompter(InitFixtures.OpenRouter, InitFixtures.ApiKey, false);

        int exit = await init.InvokeAsync(init.Seams(prompter, FakeApiHandler.Refusing()));

        Assert.Equal((RunCommandFixtures.Interrupted, english.InitCancelled), (exit, init.LastLine));
    }

    [Theory]
    [InlineData(Locales.English, "✕ Anthropic rejected the key.")]
    [InlineData(Locales.Norwegian, "✕ Anthropic avviste nøkkelen.")]
    public async Task ShouldAskAgain_WhenProviderRejectsKey(string language, string line) {
        init.SetLanguage(language);
        IMessages messages = Messages.For(null, init.Environment);
        object[] answers = [InitFixtures.Anthropic, InitFixtures.ApiKey, InitFixtures.OtherKey, .. init.WebsiteFlow[2..]];
        ScriptedPrompter prompter = init.Prompter(answers);

        int exit = await init.InvokeAsync(
            init.Seams(prompter, FakeApiHandler.Json(401, InitFixtures.AuthRefused), InitHarness.Listing()));

        Assert.Equal(CliExitCodes.Ok, exit);
        Assert.Contains(line, init.Output, StringComparison.Ordinal);
        Assert.Equal(2, prompter.Asked.Count(message => message == messages.InitApiKey));
        Assert.Equal(InitFixtures.OtherKey, init.Store.Values[anthropicSecret]);
    }

    [Fact]
    public async Task ShouldAskAgain_WhenOpenRouterRejectsKey() {
        ScriptedPrompter prompter = init.Prompter(
            [InitFixtures.OpenRouter, InitFixtures.ApiKey, .. RouterFlow[1..]]);

        int exit = await init.InvokeAsync(init.Seams(
            prompter,
            InitHarness.RouterListing(),
            FakeApiHandler.Json(401, InitFixtures.AuthRefused),
            InitHarness.RouterListing(),
            InitHarness.RouterKeyWorks()));

        Assert.Equal(CliExitCodes.Ok, exit);
        Assert.Contains(english.InitKeyRejected("OpenRouter"), init.Output, StringComparison.Ordinal);
        Assert.Equal(2, prompter.Asked.Count(message => message == english.InitApiKey));
    }

    [Fact]
    public async Task ShouldOfferRetry_WhenProviderIsUnreachable() {
        ScriptedPrompter prompter = init.Prompter(
            [InitFixtures.OpenRouter, InitFixtures.ApiKey, true, .. RouterFlow[1..]]);

        int exit = await init.InvokeAsync(init.Seams(
            prompter, FakeApiHandler.Refusing(), InitHarness.RouterListing(), InitHarness.RouterKeyWorks()));

        Assert.Equal(CliExitCodes.Ok, exit);
        Assert.Contains("✕ Could not reach OpenRouter: Connection", init.Output, StringComparison.Ordinal);
        Assert.Contains(english.InitTryAgain, prompter.Asked);
    }

    [Fact]
    public async Task ShouldAskAgain_WhenProviderHasNoModels() {
        ScriptedPrompter prompter = init.Prompter(
            [InitFixtures.Ollama, string.Empty, true, string.Empty, InitFixtures.OllamaModel, .. init.AfterModel]);

        int exit = await init.InvokeAsync(init.Seams(
            prompter, FakeApiHandler.Json(200, InitFixtures.EmptyOllamaList), FakeApiHandler.Json(200, InitFixtures.OllamaList)));

        Assert.Equal(CliExitCodes.Ok, exit);
        Assert.Contains(english.InitNoModels("Ollama"), init.Output, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("ANTHROPIC_API_KEY", InitFixtures.Smtp, "API key")]
    [InlineData("SMTP_PASSWORD", InitFixtures.Smtp, "SMTP password (Gmail: app password)")]
    [InlineData("RESEND_API_KEY", InitFixtures.Resend, "Resend API key")]
    public async Task ShouldSayVariableWins_WhenKeyVariableIsSet(string variable, string via, string prompt) {
        init.Environment[variable] = "value";
        await using FakeSmtpServer server = InitHarness.AcceptingServer();
        object[] answers = via == InitFixtures.Smtp ? init.SmtpFlow(server) : init.ResendFlow();

        int exit = await init.InvokeAsync(init.Seams(init.Prompter(answers), InitHarness.Listing()));

        int line = init.Output.IndexOf(english.InitEnvVarWins(variable), StringComparison.Ordinal);
        Assert.Equal(CliExitCodes.Ok, exit);
        Assert.InRange(line, 0, init.Output.IndexOf($"? {prompt}", StringComparison.Ordinal));
    }

    [Fact]
    public async Task ShouldStaySilent_WhenKeyVariableIsUnset() {
        int exit = await init.InvokeAsync(init.Seams(init.Prompter(init.WebsiteFlow), InitHarness.Listing()));

        Assert.Equal(CliExitCodes.Ok, exit);
        Assert.DoesNotContain(english.InitEnvVarWins("ANTHROPIC_API_KEY"), init.Output, StringComparison.Ordinal);
    }

    [Fact]
    public async Task ShouldShowDailyCost_WhenModelHasPrice() {
        ScriptedPrompter prompter = init.Prompter(RouterFlow);

        await init.InvokeAsync(init.Seams(prompter, InitHarness.RouterListing(), InitHarness.RouterKeyWorks()));

        Assert.Equal("Model One   about $0.34 a day", prompter.Choices[1][0].Label);
    }

    [Fact]
    public async Task ShouldShowNoPrice_WhenModelHasNoPrice() {
        ScriptedPrompter prompter = init.Prompter(UnpricedFlow(string.Empty));

        await init.InvokeAsync(init.Seams(prompter, InitHarness.UnpricedListing()));

        Assert.Equal("Claude Unlisted   no price", prompter.Choices[1][0].Label);
    }

    [Fact]
    public async Task ShouldSaveListedPrice_WhenTableHasNoPrice() {
        ScriptedPrompter prompter = init.Prompter(RouterFlow);

        int exit = await init.InvokeAsync(init.Seams(prompter, InitHarness.RouterListing(), InitHarness.RouterKeyWorks()));

        Assert.Equal((CliExitCodes.Ok, new PriceOverride(10m, 20m)), (exit, init.Saved.Model.Price));
        Assert.DoesNotContain(english.InitPriceInput, prompter.Asked);
    }

    [Fact]
    public async Task ShouldAskTwoPrices_WhenModelHasNoPrice() {
        ScriptedPrompter prompter = init.Prompter(
            [InitFixtures.Anthropic, InitFixtures.ApiKey, InitFixtures.UnpricedModel, "1,50", "3", string.Empty, .. init.AfterModel]);

        int exit = await init.InvokeAsync(init.Seams(prompter, InitHarness.UnpricedListing()));

        Assert.Equal((CliExitCodes.Ok, new PriceOverride(1.50m, 3m)), (exit, init.Saved.Model.Price));
    }

    [Fact]
    public async Task ShouldAskOutputAgain_WhenOutputPriceIsEmpty() {
        ScriptedPrompter prompter = init.Prompter([
            InitFixtures.Anthropic, InitFixtures.ApiKey, InitFixtures.UnpricedModel, "1", string.Empty, "3", string.Empty,
            .. init.AfterModel,
        ]);

        int exit = await init.InvokeAsync(init.Seams(prompter, InitHarness.UnpricedListing()));

        Assert.Equal((CliExitCodes.Ok, new PriceOverride(1m, 3m)), (exit, init.Saved.Model.Price));
    }

    [Theory]
    [InlineData(Locales.English)]
    [InlineData(Locales.Norwegian)]
    public async Task ShouldWarn_WhenUnpricedModelHasLimit(string language) {
        init.SetLanguage(language);
        IMessages messages = Messages.For(null, init.Environment);

        int exit = await init.InvokeAsync(init.Seams(init.Prompter(UnpricedFlow("5")), InitHarness.UnpricedListing()));

        Assert.Equal(CliExitCodes.Ok, exit);
        Assert.Contains(messages.UnpricedLimitWarning, init.Output, StringComparison.Ordinal);
    }

    [Fact]
    public async Task ShouldNotWarn_WhenPricedModelHasLimit() {
        object[] answers = [InitFixtures.Anthropic, InitFixtures.ApiKey, InitFixtures.TableModel, "5", .. init.AfterModel];

        int exit = await init.InvokeAsync(init.Seams(init.Prompter(answers), InitHarness.Listing()));

        Assert.Equal((CliExitCodes.Ok, 5m), (exit, init.Saved.Model.MonthlyLimitUsd));
        Assert.DoesNotContain(english.UnpricedLimitWarning, init.Output, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("abc")]
    [InlineData("1000001")]
    public async Task ShouldAskAgain_WhenAmountIsNotNumber(string amount) {
        object[] answers = [
            InitFixtures.Anthropic, InitFixtures.ApiKey, InitFixtures.TableModel, amount, string.Empty, .. init.AfterModel,
        ];

        int exit = await init.InvokeAsync(init.Seams(init.Prompter(answers), InitHarness.Listing()));

        Assert.Equal(CliExitCodes.Ok, exit);
        Assert.Contains(english.InitNotAmount, init.Output, StringComparison.Ordinal);
        Assert.Null(init.Saved.Model.MonthlyLimitUsd);
    }

    [Fact]
    public async Task ShouldNotAskLimit_WhenProviderIsOllama() {
        ScriptedPrompter prompter = init.Prompter([InitFixtures.Ollama, string.Empty, InitFixtures.OllamaModel, .. init.AfterModel]);

        int exit = await init.InvokeAsync(init.Seams(prompter, FakeApiHandler.Json(200, InitFixtures.OllamaList)));

        Assert.Equal(CliExitCodes.Ok, exit);
        Assert.DoesNotContain(english.InitMonthlyLimit, prompter.Asked);
    }

    [Fact]
    public async Task ShouldSaveModel_WhenPicked() {
        object[] answers = [InitFixtures.Ollama, "http://ollama.example.test:11434", InitFixtures.OllamaModel, .. init.AfterModel];

        int exit = await init.InvokeAsync(init.Seams(init.Prompter(answers), FakeApiHandler.Json(200, InitFixtures.OllamaList)));

        ModelConfig saved = init.Saved.Model;
        Assert.Equal(
            (CliExitCodes.Ok, ProviderName.Ollama, InitFixtures.OllamaModel, "http://ollama.example.test:11434"),
            (exit, saved.Provider, saved.Model, saved.OllamaUrl));
    }

    private object[] UnpricedFlow(string limit) =>
        [InitFixtures.Anthropic, InitFixtures.ApiKey, InitFixtures.UnpricedModel, string.Empty, limit, .. init.AfterModel];
}
