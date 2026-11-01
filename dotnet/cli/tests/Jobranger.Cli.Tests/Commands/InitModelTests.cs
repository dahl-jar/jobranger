using System.Globalization;
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
    [InlineData(Locales.English, 401, "✕ Anthropic rejected the key.")]
    [InlineData(Locales.Norwegian, 401, "✕ Anthropic avviste nøkkelen.")]
    [InlineData(Locales.English, 400, "✕ Anthropic rejected the key.")]
    [InlineData(Locales.English, 404, "✕ Anthropic rejected the key.")]
    public async Task ShouldAskAgain_WhenProviderRejectsKey(string language, int status, string line) {
        init.SetLanguage(language);
        IMessages messages = Messages.For(null, init.Environment);
        object[] answers = [InitFixtures.Anthropic, InitFixtures.ApiKey, InitFixtures.OtherKey, .. init.WebsiteFlow[2..]];
        ScriptedPrompter prompter = init.Prompter(answers);

        int exit = await init.InvokeAsync(
            init.Seams(prompter, FakeApiHandler.Json(status, InitFixtures.AuthRefused), InitHarness.Listing()));

        Assert.Equal(CliExitCodes.Ok, exit);
        Assert.Contains(line, init.Output, StringComparison.Ordinal);
        Assert.Equal(2, prompter.Asked.Count(message => message == messages.InitApiKey));
        Assert.Equal(InitFixtures.OtherKey, init.Store.Values[anthropicSecret]);
    }

    [Fact]
    public async Task ShouldAskAgain_WhenOpenRouterRejectsKey() {
        await AssertKeyAskedAgainAsync(
            InitFixtures.ApiKey,
            InitHarness.RouterListing(),
            FakeApiHandler.Json(401, InitFixtures.AuthRefused),
            InitHarness.RouterListing(),
            InitHarness.RouterKeyWorks());
    }

    [Fact]
    public async Task ShouldAskAgain_WhenOpenRouterKeyIsEmpty() {
        await AssertKeyAskedAgainAsync(
            string.Empty, InitHarness.RouterListing(), InitHarness.RouterListing(), InitHarness.RouterKeyWorks());
    }

    [Theory]
    [InlineData(Locales.English, "✕ http://ollama.example.test did not answer as Ollama.")]
    [InlineData(Locales.Norwegian, "✕ http://ollama.example.test svarte ikke som Ollama.")]
    public async Task ShouldNameUrl_WhenOllamaRefuses(string language, string line) {
        init.SetLanguage(language);
        ScriptedPrompter prompter = init.Prompter(
            [InitFixtures.Ollama, "http://ollama.example.test", .. init.OllamaFlow[1..]]);

        int exit = await init.InvokeAsync(init.Seams(
            prompter, FakeApiHandler.Json(404, InitFixtures.EmptyOllamaList), InitHarness.OllamaListing()));

        Assert.Equal(CliExitCodes.Ok, exit);
        Assert.Contains(InitHarness.Lines(line), init.Output, StringComparison.Ordinal);
    }

    [Fact]
    public async Task ShouldHidePassword_WhenOllamaRefusesUrlWithPassword() {
        ScriptedPrompter prompter = init.Prompter(
            [InitFixtures.Ollama, "http://user:Secret42@ollama.example.test", .. init.OllamaFlow[1..]]);

        int exit = await init.InvokeAsync(init.Seams(
            prompter, FakeApiHandler.Json(401, InitFixtures.EmptyOllamaList), InitHarness.OllamaListing()));

        Assert.Equal(CliExitCodes.Ok, exit);
        Assert.Contains(
            InitHarness.Lines("✕ http://ollama.example.test did not answer as Ollama."), init.Output, StringComparison.Ordinal);
        Assert.DoesNotContain("Secret42", init.Output, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData(Locales.English, "localhost:11434", "✕ localhost:11434 is not an http:// or https:// address.")]
    [InlineData(Locales.Norwegian, "localhost:11434", "✕ localhost:11434 er ikke en http://- eller https://-adresse.")]
    [InlineData(Locales.English, "ftp://user:Secret42@ollama.example.test", "✕ ftp://ollama.example.test is not an http:// or https:// address.")]
    public async Task ShouldAskUrlAgain_WhenUrlIsNotHttp(string language, string url, string line) {
        init.SetLanguage(language);
        IMessages messages = Messages.For(null, init.Environment);
        ScriptedPrompter prompter = init.Prompter([InitFixtures.Ollama, url, .. init.OllamaFlow[1..]]);

        int exit = await init.InvokeAsync(init.Seams(prompter, InitHarness.OllamaListing()));

        Assert.Equal(CliExitCodes.Ok, exit);
        Assert.Contains(InitHarness.Lines(line), init.Output, StringComparison.Ordinal);
        Assert.Equal(2, prompter.Asked.Count(message => message == messages.InitOllamaUrl));
        Assert.DoesNotContain(messages.InitTryAgain, prompter.Asked);
        Assert.DoesNotContain("Secret42", init.Output, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData(Locales.English, "Checking Anthropic...")]
    [InlineData(Locales.Norwegian, "Sjekker Anthropic...")]
    public async Task ShouldSayChecking_BeforeFirstRequest(string language, string line) {
        init.SetLanguage(language);
        string? shownAtRequest = null;
        FakeApiHandler listing = new((_, _) => {
            shownAtRequest = init.Output;
            return Task.FromResult(FakeApiHandler.Response(200, InitFixtures.AnthropicList()));
        });

        int exit = await init.InvokeAsync(init.Seams(init.Prompter(init.WebsiteFlow), listing));

        Assert.Equal(CliExitCodes.Ok, exit);
        Assert.Contains(InitHarness.Lines(line), shownAtRequest, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("7e22")]
    [InlineData("2")]
    public async Task ShouldAskPrices_WhenListedPriceIsOutOfRange(string perToken) {
        string list = InitFixtures.RouterList
            .Replace("0.00001", perToken, StringComparison.Ordinal)
            .Replace("0.00002", perToken, StringComparison.Ordinal);
        ScriptedPrompter prompter = init.Prompter(
            [InitFixtures.OpenRouter, InitFixtures.ApiKey, InitFixtures.RouterModel, "1", "3", string.Empty, .. init.AfterModel]);

        int exit = await init.InvokeAsync(init.Seams(prompter, FakeApiHandler.Json(200, list), InitHarness.RouterKeyWorks()));

        Assert.Equal((CliExitCodes.Ok, new PriceOverride(1m, 3m)), (exit, init.Saved.Model.Price));
        Assert.Equal("Model One   no price", prompter.Choices[1][0].Label);
    }

    [Fact]
    public async Task ShouldOfferRetry_WhenProviderIsUnreachable() {
        ScriptedPrompter prompter = init.Prompter(
            [InitFixtures.OpenRouter, InitFixtures.ApiKey, true, .. RouterFlow[1..]]);

        int exit = await init.InvokeAfterRefusalAsync(prompter);

        Assert.Equal(CliExitCodes.Ok, exit);
        Assert.Contains(InitHarness.Lines("✕ Could not reach OpenRouter."), init.Output, StringComparison.Ordinal);
        Assert.Contains(english.InitTryAgain, prompter.Asked);
    }

    [Fact]
    public async Task ShouldAskAgain_WhenProviderHasNoModels() {
        ScriptedPrompter prompter = init.Prompter(
            [InitFixtures.Ollama, string.Empty, true, .. init.OllamaFlow[1..]]);

        int exit = await init.InvokeAsync(init.Seams(
            prompter, FakeApiHandler.Json(200, InitFixtures.EmptyOllamaList), InitHarness.OllamaListing()));

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

    [Fact]
    public async Task ShouldWarn_WhenUnpricedModelHasLimit() {
        int exit = await init.InvokeAsync(init.Seams(init.Prompter(UnpricedFlow("5")), InitHarness.UnpricedListing()));

        Assert.Equal(CliExitCodes.Ok, exit);
        Assert.Contains(english.UnpricedLimitWarning, init.Output, StringComparison.Ordinal);
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
    public async Task ShouldAskAgain_WhenAmountIsInvalid(string amount) {
        object[] answers = [
            InitFixtures.Anthropic, InitFixtures.ApiKey, InitFixtures.TableModel, amount, string.Empty, .. init.AfterModel,
        ];

        int exit = await init.InvokeAsync(init.Seams(init.Prompter(answers), InitHarness.Listing()));

        Assert.Equal(CliExitCodes.Ok, exit);
        Assert.Contains(english.InitNotAmount, init.Output, StringComparison.Ordinal);
        Assert.Null(init.Saved.Model.MonthlyLimitUsd);
    }

    [Fact]
    public async Task ShouldSaveLimit_WhenAmountIsAtMaximum() {
        string limit = AppConfigValidator.MaxAmountUsd.ToString(CultureInfo.InvariantCulture);
        object[] answers = [InitFixtures.Anthropic, InitFixtures.ApiKey, InitFixtures.TableModel, limit, .. init.AfterModel];

        int exit = await init.InvokeAsync(init.Seams(init.Prompter(answers), InitHarness.Listing()));

        Assert.Equal((CliExitCodes.Ok, AppConfigValidator.MaxAmountUsd), (exit, init.Saved.Model.MonthlyLimitUsd));
    }

    [Fact]
    public async Task ShouldSayOllamaAnswered_WhenListingWorks() {
        await init.InvokeAsync(init.Seams(init.Prompter(init.OllamaFlow), InitHarness.OllamaListing()));

        Assert.Contains(english.InitOllamaAnswered, init.Output, StringComparison.Ordinal);
    }

    [Fact]
    public async Task ShouldNotAskLimit_WhenProviderIsOllama() {
        ScriptedPrompter prompter = init.Prompter(init.OllamaFlow);

        int exit = await init.InvokeAsync(init.Seams(prompter, InitHarness.OllamaListing()));

        Assert.Equal(CliExitCodes.Ok, exit);
        Assert.DoesNotContain(english.InitMonthlyLimit, prompter.Asked);
    }

    [Fact]
    public async Task ShouldSaveModel_WhenPicked() {
        object[] answers = [InitFixtures.Ollama, "http://ollama.example.test:11434", .. init.OllamaFlow[2..]];

        int exit = await init.InvokeAsync(init.Seams(init.Prompter(answers), InitHarness.OllamaListing()));

        ModelConfig saved = init.Saved.Model;
        Assert.Equal(
            (CliExitCodes.Ok, ProviderName.Ollama, InitFixtures.OllamaModel, "http://ollama.example.test:11434"),
            (exit, saved.Provider, saved.Model, saved.OllamaUrl));
    }

    [Fact]
    public async Task ShouldDropControlCharacters_WhenModelNameHasThem() {
        const string spoofedList = """{"models":[{"name":"llama3:latest\u001b]0;spoof\u0007\u001b[2J"}]}""";
        ScriptedPrompter prompter = init.Prompter(InitFixtures.Ollama, string.Empty, ScriptedPrompter.Cancel);

        await init.InvokeAsync(init.Seams(prompter, FakeApiHandler.Json(200, spoofedList)));

        string label = Assert.Single(prompter.Choices[^1]).Label;
        Assert.DoesNotContain(label, char.IsControl);
        Assert.StartsWith("llama3:latest]0;spoof[2J", label, StringComparison.Ordinal);
    }

    private async Task AssertKeyAskedAgainAsync(string firstKey, params FakeApiHandler[] handlers) {
        ScriptedPrompter prompter = init.Prompter([InitFixtures.OpenRouter, firstKey, .. RouterFlow[1..]]);

        int exit = await init.InvokeAsync(init.Seams(prompter, handlers));

        Assert.Equal(CliExitCodes.Ok, exit);
        Assert.Contains(english.InitKeyRejected("OpenRouter"), init.Output, StringComparison.Ordinal);
        Assert.Equal(2, prompter.Asked.Count(message => message == english.InitApiKey));
    }

    private object[] UnpricedFlow(string limit) =>
        [InitFixtures.Anthropic, InitFixtures.ApiKey, InitFixtures.UnpricedModel, string.Empty, limit, .. init.AfterModel];
}
