using System.Runtime.Versioning;
using Jobranger.Cli.Storage;
using Jobranger.Cli.Tests.Mocks;
using Microsoft.Data.Sqlite;
using Microsoft.Extensions.Logging.Abstractions;

namespace Jobranger.Cli.Tests.Storage;

[UnsupportedOSPlatform("windows")]
public sealed class StateStoreTests : IDisposable {
    private const string cursorKey = "nav_position";

    private readonly TestDatabase db = new();
    private readonly StateStore state;

    public StateStoreTests() {
        state = new StateStore(db.Connection);
    }

    public void Dispose() => db.Dispose();

    [Fact]
    public void ShouldReadLatestValue_WhenDatabaseIsReopened() {
        state.Set(cursorKey, "https://feed.example.test/1");
        state.Set(cursorKey, "https://feed.example.test/2");

        using SqliteConnection reopened = Database.Open(db.Path, NullLogger.Instance);

        string? read = new StateStore(reopened).Get(cursorKey);
        Assert.Equal("https://feed.example.test/2", read);
    }

    [Fact]
    public void ShouldReturnNull_WhenKeyIsMissing() {
        Assert.Null(state.Get(cursorKey));
    }

    [Fact]
    public void ShouldLeaveOutSchemaVersion_WhenListingCursors() {
        state.Set(cursorKey, "page-1");

        Assert.Equal(new Dictionary<string, string> { [cursorKey] = "page-1" }, state.Cursors());
    }
}
