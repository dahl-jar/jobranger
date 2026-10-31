using System.Collections;
using System.Runtime.Versioning;
using Jobranger.Cli.Platform;

namespace Jobranger.Cli.Commands;

[UnsupportedOSPlatform("windows")]
internal sealed record EditServices(IDictionary Environment, TimeProvider Time, IEditorProcess Process, Func<string?> ReadLine) {
    public static EditServices Create() {
        IDictionary environment = System.Environment.GetEnvironmentVariables();
        return new EditServices(environment, TimeProvider.System, new EditorProcess(environment), Console.In.ReadLine);
    }
}
