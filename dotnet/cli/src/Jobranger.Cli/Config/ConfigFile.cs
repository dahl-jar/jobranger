using System.Runtime.Versioning;
using System.Text;
using System.Text.Json.Nodes;
using Jobranger.Cli.Config.Yaml;
using Jobranger.Cli.Files;
using Jobranger.Cli.Text;
using Microsoft.Extensions.Logging;

namespace Jobranger.Cli.Config;

internal static class ConfigFile {
    private const char byteOrderMark = '\uFEFF';
    private const char carriageReturn = '\r';
    private const char newLine = '\n';
    private const string windowsLineEnd = "\r\n";
    private const string unixLineEnd = "\n";

    private static readonly UTF8Encoding strictUtf8 =
        new(encoderShouldEmitUTF8Identifier: false, throwOnInvalidBytes: true);

    public static AppConfig Load(string path, string home) => Parse(ReadText(path), path, home);

    public static AppConfig Parse(string text, string path, string home) {
        YamlTree tree = new YamlReader(text, Path.GetFileName(path)).Read();
        ConfigReader reader = new(home);
        AppConfig? config = tree.Errors.Count == 0 ? reader.Read(tree.Root, Path.GetFileName(path)) : null;
        List<ConfigError> errors = [.. tree.Errors, .. reader.Errors];
        if (config is not null) {
            errors.AddRange(AppConfigValidator.Validate(config));
        }
        if (errors.Count > 0 || config is null) {
            throw new ConfigInvalidException(path, errors);
        }
        return config;
    }

    [UnsupportedOSPlatform("windows")]
    public static void Save(string path, AppConfig config, string home, ILogger logger) {
        IReadOnlyList<ConfigError> problems = AppConfigValidator.Validate(config);
        if (problems.Count > 0) {
            throw new ConfigInvalidException(path, problems);
        }
        YamlTree? previous = null;
        if (File.Exists(path)) {
            previous = ReadTree(path);
            if (previous.Errors.Count > 0) {
                throw new ConfigInvalidException(path, previous.Errors);
            }
        }
        JsonObject model = ConfigWriter.ToTree(config, home);
        PrivateFile.Write(path, strictUtf8.GetBytes(YamlEmitter.Emit(model, previous)), logger);
    }

    [UnsupportedOSPlatform("windows")]
    public static void SaveText(string path, string text, string home, ILogger logger) {
        Parse(text, path, home);
        PrivateFile.Write(path, strictUtf8.GetBytes(text), logger);
    }

    public static Language? ReadLanguage(string path) {
        try {
            return ConfigReader.ReadLanguage(ReadTree(path).Root);
        } catch (Exception error) when (error is ConfigMissingException or ConfigInvalidException
            or IOException or UnauthorizedAccessException) {
            return null;
        }
    }

    public static YamlTree ReadTree(string path) => new YamlReader(ReadText(path), Path.GetFileName(path)).Read();

    public static string ReadText(string path) {
        byte[] bytes = [];
        try {
            bytes = File.ReadAllBytes(path);
        } catch (Exception error) when (error is FileNotFoundException or DirectoryNotFoundException) {
            throw new ConfigMissingException(path, error);
        } catch (Exception error) when (error is IOException or UnauthorizedAccessException) {
            throw new ConfigInvalidException(path, [new ConfigError(Path.GetFileName(path), ConfigProblem.Unreadable)]);
        }
        try {
            string text = strictUtf8.GetString(bytes)
                .Replace(windowsLineEnd, unixLineEnd, StringComparison.Ordinal)
                .Replace(carriageReturn, newLine);
            return text.Length > 0 && text[0] == byteOrderMark ? text[1..] : text;
        } catch (DecoderFallbackException) {
            throw new ConfigInvalidException(path, [new ConfigError(Path.GetFileName(path), ConfigProblem.NotUtf8)]);
        }
    }
}
