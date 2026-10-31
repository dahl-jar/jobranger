using System.Collections;
using System.Runtime.Versioning;
using Jobranger.Cli.Commands;
using Jobranger.Cli.Config;
using Jobranger.Cli.Features.Providers;
using Jobranger.Cli.Features.Runs;
using Jobranger.Cli.Http;
using Jobranger.Cli.Tests.Mocks;
using Jobranger.Cli.Text;
using Microsoft.Extensions.Time.Testing;
using DeliveryEntry = Jobranger.Cli.Features.Delivery.Delivery;

namespace Jobranger.Cli.Tests.Commands;

[UnsupportedOSPlatform("windows")]
public sealed class RunCommandModelStopTests : IDisposable {
    private const string countsEnglish = "0 ads read, 0 new, 0 matches.";
    private const string countsNorwegian = "0 annonser lest, 0 nye, 0 treff.";
    private const string savedEnglish = "✓ Saved for the website";
    private const string savedNorwegian = "✓ Lagret for nettsiden";
    private const string keyRefusedEnglish =
        "AI checks stopped: the provider refused the API key. 1 ad waits. Set a new key with: jobranger init";
    private const string providerDownEnglish = "AI checks stopped: the provider did not answer. 1 ad waits for the next run.";
    private const string keyRefusedNorwegian =
        "AI-sjekkene stoppet: leverandøren avviste nøkkelen. 1 annonse venter. Sett en ny nøkkel med: jobranger init";
    private const string providerDownNorwegian = "AI-sjekkene stoppet: leverandøren svarte ikke. 1 annonse venter på neste kjøring.";
    private const string skipLine = "AI checks skipped: no model or key. Set one with: jobranger init";

    private static readonly RunResult completed = new() { Status = RunStatus.Completed };

    private readonly TempDir temp = new();
    private readonly RecordingConsole console = new();
    private readonly FakeTimeProvider clock = AdFixtures.Clock();
    private readonly Hashtable environment;

    public RunCommandModelStopTests() {
        environment = RunCommandFixtures.Environment(temp, Locales.English);
        RunCommandFixtures.Save(environment, RunCommandFixtures.Config());
        RunCommandFixtures.SeedPendingAd(environment, clock);
    }

    public void Dispose() {
        console.Dispose();
        temp.Dispose();
    }

    [Theory]
    [InlineData(Locales.English, true, countsEnglish, keyRefusedEnglish, savedEnglish)]
    [InlineData(Locales.English, false, countsEnglish, providerDownEnglish, savedEnglish)]
    [InlineData(Locales.Norwegian, true, countsNorwegian, keyRefusedNorwegian, savedNorwegian)]
    [InlineData(Locales.Norwegian, false, countsNorwegian, providerDownNorwegian, savedNorwegian)]
    public async Task ShouldPrintStopNote_WhenModelStops(string lang, bool keyRefused, string counts, string note, string saved) {
        environment["LANG"] = lang;

        int exit = await InvokeAsync(keyRefused ? ApiErrorKind.Auth : ApiErrorKind.NotFound);

        Assert.Equal((CliExitCodes.Ok, Lines(counts, note, saved)), (exit, console.Output.TrimEnd()));
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void ShouldPrintNoStopNote_WhenNoAdsWait(bool keyRefused) {
        RunResult result = completed with { ModelStopped = keyRefused ? StopReason.Auth : StopReason.ProviderDown, Pending = 0 };

        IReadOnlyList<string> lines = DeliveryEntry.SummaryLines(result, new EnglishMessages());

        Assert.Empty(lines);
    }

    [Fact]
    public void ShouldPrintNoStopNote_WhenBudgetPausedChecks() {
        RunResult result = completed with { ModelStopped = StopReason.Budget, Pending = 1 };

        IReadOnlyList<string> lines = DeliveryEntry.SummaryLines(result, new EnglishMessages());

        Assert.Empty(lines);
    }

    [Fact]
    public async Task ShouldPrintOnlySkipLine_WhenAdsWaitWithNoModel() {
        RunCommandFixtures.Save(environment, RunCommandFixtures.Config(model: new ModelConfig()));

        int exit = await InvokeAsync(RunSeams.Production with { EnabledSources = _ => [] });

        Assert.Equal((CliExitCodes.Ok, Lines(countsEnglish, skipLine, savedEnglish)), (exit, console.Output.TrimEnd()));
    }

    private static string Lines(params string[] lines) => string.Join(Environment.NewLine, lines);

    private Task<int> InvokeAsync(ApiErrorKind kind) => InvokeAsync(RunSeams.Production with {
        EnabledSources = _ => [],
        OpenModel = (_, _, _, _, _) => Task.FromResult<IModelClient>(
            new FakeModelClient((_, _) => throw new ApiException(kind, "refused"))),
    });

    private Task<int> InvokeAsync(RunSeams seams) {
        CliServices services = CliServices.Create(clock, console.Console, console.ErrorConsole, environment);
        System.CommandLine.RootCommand root = [new RunCommand(services, environment, clock, inputRedirected: true, seams)];
        return root.Parse([RunCommand.CommandName]).InvokeAsync(null, TestContext.Current.CancellationToken)
            .WaitAsync(TestWaits.Step, TestContext.Current.CancellationToken);
    }
}
