using System.Collections;
using System.Runtime.Versioning;
using Jobranger.Cli.Commands;
using Jobranger.Cli.Config;
using Jobranger.Cli.Features.Providers;
using Jobranger.Cli.Http;
using Jobranger.Cli.Tests.Mocks;
using Microsoft.Extensions.Time.Testing;

namespace Jobranger.Cli.Tests.Commands;

[UnsupportedOSPlatform("windows")]
public sealed class RunCommandModelStopTests : IDisposable {
    private const string keyRefusedEnglish =
        "AI checks stopped: the provider refused the API key. 1 ad waits. Set a new key with: jobranger init";
    private const string providerDownEnglish = "AI checks stopped: the provider call failed. 1 ad waits for the next run.";
    private const string pausedEnglish = "AI checks are paused until next month: 1 ad waits.";
    private const string keyRefusedNorwegian =
        "AI-sjekkene stoppet: leverandøren avviste nøkkelen. 1 annonse venter. Sett en ny nøkkel med: jobranger init";
    private const string providerDownNorwegian = "AI-sjekkene stoppet: kallet til leverandøren feilet. 1 annonse venter på neste kjøring.";

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
    [InlineData(Locales.English, true, RunCommandFixtures.CountsEnglish, keyRefusedEnglish, RunCommandFixtures.SavedEnglish)]
    [InlineData(Locales.English, false, RunCommandFixtures.CountsEnglish, providerDownEnglish, RunCommandFixtures.SavedEnglish)]
    [InlineData(Locales.Norwegian, true, RunCommandFixtures.CountsNorwegian, keyRefusedNorwegian, RunCommandFixtures.SavedNorwegian)]
    [InlineData(Locales.Norwegian, false, RunCommandFixtures.CountsNorwegian, providerDownNorwegian, RunCommandFixtures.SavedNorwegian)]
    public async Task ShouldPrintStopNote_WhenModelStops(string lang, bool keyRefused, string counts, string note, string saved) {
        environment["LANG"] = lang;

        int exit = await InvokeAsync(keyRefused ? ApiErrorKind.Auth : ApiErrorKind.NotFound);

        Assert.Equal((CliExitCodes.Ok, RunCommandFixtures.Lines(counts, note, saved)), (exit, console.Output.TrimEnd()));
    }

    [Fact]
    public async Task ShouldPrintOnlySkipLine_WhenAdsWaitWithNoModel() {
        RunCommandFixtures.Save(environment, RunCommandFixtures.Config(model: new ModelConfig()));

        int exit = await InvokeAsync(RunSeams.Production with { EnabledSources = _ => [] });

        Assert.Equal(
            (CliExitCodes.Ok, RunCommandFixtures.Lines(
                RunCommandFixtures.CountsEnglish, RunCommandFixtures.SkipLine, RunCommandFixtures.SavedEnglish)),
            (exit, console.Output.TrimEnd()));
    }

    [Fact]
    public async Task ShouldPrintPausedNote_WhenMonthlyLimitStopsChecks() {
        AppConfig config = RunCommandFixtures.Config();
        RunCommandFixtures.Save(environment, config with { Model = config.Model with { MonthlyLimitUsd = 0m } });

        int exit = await InvokeAsync(ApiErrorKind.Auth);

        Assert.Equal(
            (CliExitCodes.Ok, RunCommandFixtures.Lines(
                RunCommandFixtures.CountsEnglish, pausedEnglish, RunCommandFixtures.SavedEnglish)),
            (exit, console.Output.TrimEnd()));
    }

    private Task<int> InvokeAsync(ApiErrorKind kind) => InvokeAsync(RunSeams.Production with {
        EnabledSources = _ => [],
        OpenModel = (_, _, _, _, _) => Task.FromResult<IModelClient>(
            new FakeModelClient((_, _) => throw new ApiException(kind, "refused"))),
    });

    private Task<int> InvokeAsync(RunSeams seams) =>
        RunCommandFixtures.InvokeAsync(environment, console, seams, clock, TestContext.Current.CancellationToken);
}
