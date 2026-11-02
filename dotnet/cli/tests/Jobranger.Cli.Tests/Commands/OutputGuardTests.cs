using System.Text;
using Jobranger.Cli.Commands;
using Jobranger.Cli.Tests.Mocks;
using Spectre.Console;

namespace Jobranger.Cli.Tests.Commands;

public sealed class OutputGuardTests {
    private const int terminalWidth = 123;
    private const int terminalHeight = 45;
    private const string charCall = "char";
    private const string stringCall = "string";
    private const string lineCall = "line";
    private const string flushCall = "flush";
    private const string lineAsyncCall = "lineAsync";

    private readonly List<string> logged = [];

    [Theory]
    [InlineData(charCall, typeof(IOException))]
    [InlineData(stringCall, typeof(IOException))]
    [InlineData(lineCall, typeof(IOException))]
    [InlineData(lineCall, typeof(UnauthorizedAccessException))]
    [InlineData(flushCall, typeof(IOException))]
    [InlineData(lineAsyncCall, typeof(IOException))]
    [InlineData(lineAsyncCall, typeof(UnauthorizedAccessException))]
    public async Task ShouldLogOutputFailed_WhenWriterCallFails(string call, Type failure) {
        IAnsiConsole broken = CommandFixtures.BrokenConsole(failure);

        int exit = await GuardAsync(broken, () => CallAsync(broken.Profile.Out.Writer, call));

        Assert.Equal((CliExitCodes.Failure, ReadFailures.OutputFailed), (exit, Assert.Single(logged)));
    }

    [Theory]
    [InlineData(AnsiSupport.No)]
    [InlineData(AnsiSupport.Yes)]
    public async Task ShouldLogOutputFailed_WhenMarkupCannotBeWritten(AnsiSupport ansi) {
        IAnsiConsole broken = CommandFixtures.BrokenConsole(typeof(IOException), ansi);

        int exit = await GuardAsync(broken, () => {
            broken.MarkupLine("[bold]code[/]");
            return Task.FromResult(CliExitCodes.Ok);
        });

        Assert.Equal((CliExitCodes.Failure, ReadFailures.OutputFailed), (exit, Assert.Single(logged)));
    }

    [Fact]
    public async Task ShouldKeepTerminalSize_WhileGuardRuns() {
        using RecordingConsole console = new();
        using StringWriter writer = new();
        Profile profile = console.Console.Profile;
        profile.Out = new TerminalOutput(writer);
        (int Width, int Height, bool IsTerminal) seen = default;

        await GuardAsync(console.Console, () => {
            seen = (profile.Width, profile.Height, profile.Out.IsTerminal);
            return Task.FromResult(CliExitCodes.Ok);
        });

        Assert.Equal((terminalWidth, terminalHeight, true), seen);
    }

    [Fact]
    public async Task ShouldRestoreConsoleOutput_WhenWriteFails() {
        IAnsiConsole broken = CommandFixtures.BrokenConsole(typeof(IOException));
        IAnsiConsoleOutput original = broken.Profile.Out;

        await GuardAsync(broken, () => CallAsync(broken.Profile.Out.Writer, lineCall));

        Assert.Same(original, broken.Profile.Out);
    }

    private Task<int> GuardAsync(IAnsiConsole console, Func<Task<int>> action) =>
        OutputGuard.RunAsync(console, logged.Add, action)
            .WaitAsync(TestWaits.Step, TestContext.Current.CancellationToken);

    private static async Task<int> CallAsync(TextWriter writer, string call) {
        switch (call) {
            case charCall:
                writer.Write('x');
                break;
            case stringCall:
                writer.Write("x");
                break;
            case lineCall:
                writer.WriteLine("x");
                break;
            case flushCall:
                writer.Flush();
                break;
            case lineAsyncCall:
                await writer.WriteLineAsync("x");
                break;
        }
        return CliExitCodes.Ok;
    }

    private sealed class TerminalOutput(TextWriter writer) : IAnsiConsoleOutput {
        public TextWriter Writer { get; } = writer;

        public bool IsTerminal => true;

        public int Width => terminalWidth;

        public int Height => terminalHeight;

        public void SetEncoding(Encoding encoding) {
        }
    }
}
