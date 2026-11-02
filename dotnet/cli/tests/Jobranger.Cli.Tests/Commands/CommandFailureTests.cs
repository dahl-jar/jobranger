using System.Collections;
using Jobranger.Cli.Commands;
using Jobranger.Cli.Files;
using Jobranger.Cli.Text;

namespace Jobranger.Cli.Tests.Commands;

public sealed class CommandFailureTests {
    [Fact]
    public void ShouldReturnNoFailure_WhenErrorIsUnknown() {
        ReadFailure? failure = CommandFailure.Of(
            new InvalidOperationException(), new Paths(new Hashtable()), new EnglishMessages());

        Assert.Null(failure);
    }
}
