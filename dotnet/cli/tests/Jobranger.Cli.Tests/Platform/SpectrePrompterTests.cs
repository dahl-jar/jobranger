using Jobranger.Cli.Platform;
using Jobranger.Cli.Text;
using Spectre.Console;

namespace Jobranger.Cli.Tests.Platform;

public sealed class SpectrePrompterTests {
    [Theory]
    [InlineData(AnsiSupport.Yes, InteractionSupport.No, false)]
    [InlineData(AnsiSupport.No, InteractionSupport.Yes, false)]
    [InlineData(AnsiSupport.Yes, InteractionSupport.Yes, true)]
    public void ShouldOfferPrompter_OnlyWhenConsoleCanPrompt(AnsiSupport ansi, InteractionSupport interaction, bool expected) {
        using StringWriter output = new();
        IAnsiConsole console = AnsiConsole.Create(new AnsiConsoleSettings {
            Ansi = ansi,
            Interactive = interaction,
            Out = new AnsiConsoleOutput(output),
            EnvironmentVariables = [],
        });

        IPrompter? prompter = SpectrePrompter.For(console, new EnglishMessages());

        Assert.Equal(expected, prompter is not null);
    }
}
