using System.Collections;
using Jobranger.Cli.Platform;

namespace Jobranger.Cli.Commands;

internal sealed record EditSeams(IDictionary Environment, TimeProvider Time, IEditorProcess Process, Func<string?> ReadLine) {
    public static EditSeams System() => new(
        global::System.Environment.GetEnvironmentVariables(), TimeProvider.System, new EditorProcess(), Console.In.ReadLine);
}
