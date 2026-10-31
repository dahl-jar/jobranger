using System.Collections;
using System.Runtime.Versioning;
using System.Text;
using System.Text.Json.Nodes;
using Jobranger.Cli.Files;
using Jobranger.Cli.Logging;
using Jobranger.Cli.Storage;
using Microsoft.Data.Sqlite;
using Microsoft.Extensions.Logging.Abstractions;
using Spectre.Console;

namespace Jobranger.Cli.Tests.Mocks;

internal static class CommandFixtures {
    public const string FileAccessFailed = "Cannot read a jobranger file. Check the folder permissions.\n";

    private const string logFileName = "2026-10-09.jsonl";
    private const string schemaBeforeDevices = "3";

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

    [UnsupportedOSPlatform("windows")]
    public static void RewindSchema(string dbPath) {
        using SqliteConnection database = Database.Open(dbPath, NullLogger.Instance);
        new StateStore(database).Set(StateStore.SchemaVersionKey, schemaBeforeDevices);
    }

    public static IAnsiConsole BrokenConsole(Type failure) =>
        AnsiConsole.Create(new AnsiConsoleSettings {
            Ansi = AnsiSupport.No,
            ColorSystem = ColorSystemSupport.NoColors,
            Interactive = InteractionSupport.No,
            Out = new AnsiConsoleOutput(new BrokenWriter((Exception)Activator.CreateInstance(failure)!)),
            EnvironmentVariables = [],
        });

    private sealed class BrokenWriter(Exception failure) : TextWriter {
        public override Encoding Encoding => Encoding.UTF8;

        public override void Write(char value) => throw failure;
    }
}
