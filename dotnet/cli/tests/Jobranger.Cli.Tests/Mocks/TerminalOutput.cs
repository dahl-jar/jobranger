using System.Text;
using Spectre.Console;

namespace Jobranger.Cli.Tests.Mocks;

internal sealed class TerminalOutput(TextWriter writer) : IAnsiConsoleOutput {
    private const int terminalWidth = 80;
    private const int terminalHeight = 24;

    public TextWriter Writer => writer;

    public bool IsTerminal => true;

    public int Width => terminalWidth;

    public int Height => terminalHeight;

    public void SetEncoding(Encoding encoding) {
    }
}
