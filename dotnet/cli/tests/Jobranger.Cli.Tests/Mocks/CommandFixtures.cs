using System.Collections;
using System.Text.Json.Nodes;
using Jobranger.Cli.Files;
using Jobranger.Cli.Logging;

namespace Jobranger.Cli.Tests.Mocks;

internal static class CommandFixtures {
    private const string logFileName = "2026-10-09.jsonl";

    public static Hashtable Environment(TempDir temp, string language) => new() {
        ["HOME"] = temp.Path,
        ["LANG"] = language,
        ["JOBRANGER_CONFIG_DIR"] = Path.Combine(temp.Path, "config"),
        ["JOBRANGER_DATA_DIR"] = Path.Combine(temp.Path, "data"),
        ["JOBRANGER_STATE_DIR"] = Path.Combine(temp.Path, "state"),
        ["JOBRANGER_SECRET_STORE"] = "file",
    };

    public static string LogPath(Paths paths) => Path.Combine(paths.LogDir(), logFileName);

    public static string LoggedFailure(Paths paths) {
        JsonObject entry = LogLines.Read(LogPath(paths))[^1];
        return $"{entry[LogFields.Event]} {entry["failure"]}";
    }
}
