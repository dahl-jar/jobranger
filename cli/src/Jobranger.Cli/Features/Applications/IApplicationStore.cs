using Jobranger.Cli.Storage;

namespace Jobranger.Cli.Features.Applications;

internal interface IApplicationStore {
    long Create(NewApplication application);

    void Update(long id, ApplicationChange change, DateTimeOffset at);

    void Remove(long id, DateTimeOffset at);

    void Restore(long id);

    void AddEvent(long id, DateTimeOffset at, string text);

    IReadOnlyList<Application> List(bool includeRemoved = false);
}
