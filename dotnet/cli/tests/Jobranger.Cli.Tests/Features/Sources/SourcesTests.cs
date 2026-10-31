using System.Text.Json.Nodes;
using Jobranger.Cli.Config;
using Jobranger.Cli.Features.Sources;
using Jobranger.Cli.Tests.Mocks;
using Microsoft.Extensions.Logging.Testing;
using static Jobranger.Cli.Tests.Mocks.FakeSiteHandler;
using static Jobranger.Cli.Tests.Mocks.SourceFixtures;
using SourcesEntry = Jobranger.Cli.Features.Sources.Sources;

namespace Jobranger.Cli.Tests.Features.Sources;

public sealed class SourcesTests {
    private const string secondEntryPath = "/api/v1/feedentry/" + SecondNavUuid;

    [Theory]
    [InlineData(true, true, "finn,nav")]
    [InlineData(false, true, "nav")]
    [InlineData(true, false, "finn")]
    public void ShouldListEnabledSources_WhenConfigTurnsThemOn(bool finn, bool nav, string expected) {
        SourcesConfig config = new() { Finn = finn, Nav = nav, Employers = false };

        IReadOnlyList<ISource> sources = SourcesEntry.Enabled(config);

        Assert.Equal(expected, string.Join(',', sources.Select(source => source.Name)));
    }

    [Fact]
    public async Task ShouldReturnPartialBatch_WhenSourceIsCancelled() {
        using CancellationTokenSource cancel = new();
        FakeSiteHandler handler = new(url => url.AbsolutePath switch {
            "/api/publicToken" => Body(NavToken),
            "/api/v1/feedentry/" + NavUuid or secondEntryPath => Body(NavEntry(("title", "Utvikler")).ToJsonString()),
            _ => Body(FeedPage(new JsonArray(FeedItem(NavUuid), FeedItem(SecondNavUuid)))),
        }) {
            OnRequest = url => {
                if (url.AbsolutePath == secondEntryPath) {
                    cancel.Cancel();
                }
            },
        };

        SourceOutcome outcome = await SourcesEntry.FetchAsync(new NavSource(), Context(handler), cancel.Token);

        Assert.Equal(
            (true, NavId),
            (outcome.Cancelled, Ids(outcome.Batch?.Listings)));
    }

    [Fact]
    public async Task ShouldNotReportCancel_WhenSourceReturnsFullBatch() {
        using CancellationTokenSource cancel = new();
        SourceBatch full = new() { Source = "fake" };
        ScriptedSource finishing = new("fake", _ => {
            cancel.Cancel();
            return Task.FromResult(full);
        });

        SourceOutcome outcome = await SourcesEntry.FetchAsync(finishing, Context(new FakeSiteHandler(_ => Body(""))),
            cancel.Token);

        Assert.False(outcome.Cancelled);
    }

    [Fact]
    public async Task ShouldCarryPartialBatch_WhenSourceFails() {
        SourceBatch partial = new() { Source = "fake" };
        ScriptedSource failing = new("fake", _ => throw new SourceException("fake feed failed", partial));

        SourceOutcome outcome = await SourcesEntry.FetchAsync(failing, Context(new FakeSiteHandler(_ => Body(""))),
            CancellationToken.None);

        Assert.Equal(new SourceOutcome("fake", partial, "fake feed failed", false), outcome);
    }

    [Fact]
    public async Task ShouldNameErrorType_WhenSourceCrashes() {
        ScriptedSource crashing = new("fake", _ => throw new InvalidOperationException("boom"));

        SourceOutcome outcome = await SourcesEntry.FetchAsync(crashing, Context(new FakeSiteHandler(_ => Body(""))),
            CancellationToken.None);

        Assert.Equal(new SourceOutcome("fake", null, nameof(InvalidOperationException), false), outcome);
    }

    [Fact]
    public async Task ShouldLogCrash_WhenSourceThrowsUnexpectedError() {
        ScriptedSource crashing = new("fake", _ => throw new InvalidOperationException("boom"));
        FakeLogger logger = new();

        await SourcesEntry.FetchAsync(crashing, Context(new FakeSiteHandler(_ => Body("")), logger: logger),
            CancellationToken.None);

        Assert.Equal("source.crashed", Events(logger));
    }

    [Theory]
    [InlineData(true, null)]
    [InlineData(false, nameof(OperationCanceledException))]
    public async Task ShouldReportCancel_WhenSourceThrowsCancelled(bool cancelled, string? error) {
        using CancellationTokenSource cancel = new();
        if (cancelled) {
            cancel.Cancel();
        }
        ScriptedSource cancelling = new("fake", _ => throw new OperationCanceledException());

        SourceOutcome outcome = await SourcesEntry.FetchAsync(cancelling, Context(new FakeSiteHandler(_ => Body(""))),
            cancel.Token);

        Assert.Equal(new SourceOutcome("fake", null, error, cancelled), outcome);
    }
}
