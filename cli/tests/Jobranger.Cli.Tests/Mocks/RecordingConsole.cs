using Spectre.Console;

namespace Jobranger.Cli.Tests.Mocks;

internal sealed class RecordingConsole : IDisposable {
    private readonly StringWriter output = new();
    private readonly StringWriter errors = new();

    public RecordingConsole() {
        Console = Create(output);
        ErrorConsole = Create(errors);
    }

    public IAnsiConsole Console { get; }

    public IAnsiConsole ErrorConsole { get; }

    public string Output => output.ToString();

    public string Errors => errors.ToString();

    public void Dispose() {
        output.Dispose();
        errors.Dispose();
    }

    private static IAnsiConsole Create(StringWriter writer) =>
        AnsiConsole.Create(new AnsiConsoleSettings {
            Ansi = AnsiSupport.No,
            ColorSystem = ColorSystemSupport.NoColors,
            Interactive = InteractionSupport.No,
            Out = new AnsiConsoleOutput(writer),
        });
}
