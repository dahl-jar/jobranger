using System.Runtime.Versioning;
using Jobranger.Cli.Features.Ads;
using Jobranger.Cli.Features.Learning;
using Jobranger.Cli.Storage;
using Jobranger.Cli.Tests.Mocks;
using Microsoft.Data.Sqlite;
using Microsoft.Extensions.Logging.Abstractions;
using static Jobranger.Cli.Tests.Features.Learning.DecisionFixtures;

namespace Jobranger.Cli.Tests.Features.Learning;

[UnsupportedOSPlatform("windows")]
public sealed class DecisionSchemaTests {
    [Fact]
    public void ShouldAddDecisionTables_WhenPythonDatabaseIsOpened() {
        using TempDir temp = new();
        string path = temp.File("python-v2.db");
        File.Copy(TestDatabase.PythonDatabase(), path);

        using SqliteConnection connection = Database.Open(path, NullLogger.Instance);

        Assert.Equal((0L, "0|0"), (new DecisionStore(connection).Count(), Assert.Single(TestDatabase.Rows(connection,
            "SELECT (SELECT COUNT(*) FROM decision_vectors), (SELECT COUNT(*) FROM classification_examples)"))));
    }

    [Fact]
    public void ShouldRejectDecision_WhenActionIsSkipped() {
        using TestDatabase db = new();

        Assert.Throws<SqliteException>(() => db.Execute(
            "INSERT INTO decisions (ad_id, dedup_key, action, decided_at, title, company, summary, requirements, "
            + "embed_text) VALUES ('finn:1', 'k', 'skipped', 'at', 't', 'c', 's', '[]', 'e')"));
    }

    [Fact]
    public void ShouldDropExampleUses_WhenAdIsDeleted() {
        using TestDatabase db = new();
        AdStore ads = new(db.Connection, AdFixtures.Clock());
        ads.Upsert(AdFixtures.Primary);
        DecisionStore store = new(db.Connection);
        store.RecordExamples(AdFixtures.Primary.Id, [store.Record(Primary)]);

        db.Execute("DELETE FROM ads");

        Assert.Empty(store.UseCounts());
    }
}
