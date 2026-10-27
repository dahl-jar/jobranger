using System.Text;
using System.Text.Json.Nodes;
using Jobranger.Cli.Features.Sources;
using Jobranger.Cli.Tests.Mocks;
using Microsoft.Extensions.Logging.Testing;
using static Jobranger.Cli.Tests.Mocks.FakeSiteHandler;
using static Jobranger.Cli.Tests.Mocks.SourceFixtures;

namespace Jobranger.Cli.Tests.Features.Sources;

public sealed class AshbySourceTests {
    private const string boardUrl = "https://api.ashbyhq.com/posting-api/job-board/";

    private static readonly AshbySource source = new(AshbyFeed);

    [Fact]
    public async Task ShouldMapListingFields_WhenJobIsInNorway() {
        FakeSiteHandler handler = Board(BoardJson());

        SourceBatch batch = await source.FetchAsync(Context(handler), CancellationToken.None);

        Assert.Equal(new SourceListing {
            Id = "ashby:example-one:ash-1",
            Source = "ashby:example-one",
            SourceId = "ash-1",
            Title = "Software Engineer",
            Company = "Org One AS",
            Location = "Oslo",
            Published = "2026-08-20T10:00:00.000Z",
            Url = "https://jobs.ashbyhq.com/example-one/ash-1",
            Description = "Build Python services.",
        }, batch.Listings[0]);
    }

    [Fact]
    public async Task ShouldListOnlyListedNorwegianJobs_WhenBoardHasForeignAndUnlistedJobs() {
        FakeSiteHandler handler = Board(BoardJson());
        FakeLogger logger = new();

        SourceBatch batch = await source.FetchAsync(Context(handler, logger: logger), CancellationToken.None);

        Assert.Equal(
            ("ashby:example-one:ash-1,ashby:example-one:ash-2", 2, ""),
            (Ids(batch.Listings), batch.Read, Events(logger)));
    }

    [Fact]
    public async Task ShouldSetListedIds_WhenBoardIsRead() {
        FakeSiteHandler handler = Board(BoardJson());

        SourceBatch batch = await source.FetchAsync(Context(handler), CancellationToken.None);

        Assert.Equal(
            new HashSet<string> { "ashby:example-one:ash-1", "ashby:example-one:ash-2" },
            batch.ListedIds?.ToHashSet());
    }

    [Fact]
    public async Task ShouldUseSecondaryLocation_WhenAddressIsAbroad() {
        FakeSiteHandler handler = Board(BoardJson());

        SourceBatch batch = await source.FetchAsync(Context(handler), CancellationToken.None);

        Assert.Equal("Bergen", batch.Listings[1].Location);
    }

    [Fact]
    public async Task ShouldUseLocationText_WhenNorwegianAddressHasNoLocality() {
        JsonObject board = BoardJson();
        board["jobs"]![0]!["address"]!["postalAddress"]!.AsObject().Remove("addressLocality");
        FakeSiteHandler handler = Board(board);

        SourceBatch batch = await source.FetchAsync(Context(handler), CancellationToken.None);

        Assert.Equal("Oslo, Norway", batch.Listings[0].Location);
    }

    [Theory]
    [InlineData("title", "\"  \"")]
    [InlineData("id", null)]
    [InlineData("jobUrl", "7")]
    [InlineData("isListed", "\"yes\"")]
    public async Task ShouldSkipJob_WhenRequiredFieldIsInvalid(string key, string? value) {
        JsonObject board = BoardJson();
        board["jobs"]![0]![key] = value is null ? null : JsonNode.Parse(value);
        FakeSiteHandler handler = Board(board);
        FakeLogger logger = new();

        SourceBatch batch = await source.FetchAsync(Context(handler, logger: logger), CancellationToken.None);

        Assert.Equal(("ashby:example-one:ash-2", "ashby.job.skipped"), (Ids(batch.Listings), Events(logger)));
    }

    [Fact]
    public async Task ShouldSkipJob_WhenTitleIsLoneSurrogate() {
        string body = BoardJson().ToJsonString()
            .Replace("\"Software Engineer\"", "\"\\ud800\"", StringComparison.Ordinal);
        FakeSiteHandler handler = new(_ => Body(body));
        FakeLogger logger = new();

        SourceBatch batch = await source.FetchAsync(Context(handler, logger: logger), CancellationToken.None);

        Assert.Equal(("ashby:example-one:ash-2", "ashby.job.skipped"), (Ids(batch.Listings), Events(logger)));
    }

    [Theory]
    [InlineData("{}")]
    [InlineData("""{"jobs": {}}""")]
    [InlineData("[]")]
    public async Task ShouldFailWithoutPartial_WhenBoardEnvelopeIsBroken(string body) {
        FakeSiteHandler handler = new(_ => Body(body));

        SourceException error = await Assert.ThrowsAsync<SourceException>(
            () => source.FetchAsync(Context(handler), CancellationToken.None));

        Assert.Equal(("ashby:example-one failed", null), (error.Message, error.Partial));
    }

    [Fact]
    public async Task ShouldEscapeBoardName_WhenRequestingBoard() {
        FakeSiteHandler handler = Board(BoardJson());
        AshbySource spaced = new(AshbyFeed with { Board = "org one/x" });

        await spaced.FetchAsync(Context(handler), CancellationToken.None);

        Assert.Equal(boardUrl + "org%20one%2Fx", Assert.Single(handler.Urls).AbsoluteUri);
    }

    private static JsonObject BoardJson() => JsonNode.Parse(SourceTestData("ashby_board.json"))!.AsObject();

    private static FakeSiteHandler Board(JsonObject board) =>
        new(_ => Body(Encoding.UTF8.GetBytes(board.ToJsonString())));
}
