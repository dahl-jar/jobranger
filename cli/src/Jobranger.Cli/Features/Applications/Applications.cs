using Jobranger.Cli.Storage;
using Microsoft.Data.Sqlite;

namespace Jobranger.Cli.Features.Applications;

internal sealed class Applications(SqliteConnection connection) {
    private readonly ApplicationStore store = new(connection);

    public long Create(NewApplication application) => store.Create(application);

    public void Update(long id, ApplicationChange change, DateTimeOffset at) => store.Update(id, change, at);

    public void Remove(long id, DateTimeOffset at) => store.Remove(id, at);

    public void Restore(long id) => store.Restore(id);

    public void AddEvent(long id, DateTimeOffset at, string text) => store.AddEvent(id, at, text);

    public IReadOnlyList<Application> List(bool includeRemoved = false) => store.List(includeRemoved);
}
