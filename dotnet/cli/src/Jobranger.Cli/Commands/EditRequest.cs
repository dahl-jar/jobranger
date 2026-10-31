using Jobranger.Cli.Features.Rechecks;

namespace Jobranger.Cli.Commands;

internal sealed record EditRequest(
    Func<string> Load,
    string Suffix,
    string TargetPath,
    Func<string, SaveResult> Save,
    Func<SaveResult, string> SavedLine,
    string KeptLine);
