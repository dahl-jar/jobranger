namespace Jobranger.Cli.Platform;

internal interface IEditorProcess {
    Task<int> RunAsync(string program, IReadOnlyList<string> arguments, CancellationToken cancellationToken);
}
