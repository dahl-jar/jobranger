using System.Globalization;
using System.Runtime.Versioning;
using System.Text;
using Jobranger.Cli.Config;
using Jobranger.Cli.Files;
using Jobranger.Cli.Text;
using Microsoft.Extensions.Logging;

namespace Jobranger.Cli.Features.Matching;

internal static class Guidance {
    private const int limit = 12_000;
    private const string byteOrderMark = "\uFEFF";
    private const string windowsNewLine = "\r\n";
    private const string carriageReturn = "\r";
    private const string newLine = "\n";

    private static readonly GuidanceText defaultText = new(string.Empty);
    private static readonly UTF8Encoding strictUtf8 =
        new(encoderShouldEmitUTF8Identifier: false, throwOnInvalidBytes: true);

    public static GuidanceText Load(string path) {
        if (!File.Exists(path) && !Directory.Exists(path)) {
            return defaultText;
        }
        byte[] bytes;
        try {
            bytes = File.ReadAllBytes(path);
        } catch (Exception error) when (error is IOException or UnauthorizedAccessException) {
            throw Invalid(path, ConfigProblem.Unreadable);
        }
        string raw;
        try {
            raw = strictUtf8.GetString(bytes);
        } catch (DecoderFallbackException) {
            throw Invalid(path, ConfigProblem.NotUtf8);
        }
        return new GuidanceText(Validated(path, raw));
    }

    [UnsupportedOSPlatform("windows")]
    public static GuidanceText Save(string path, string text, ILogger logger) {
        string normalized = Validated(path, text);
        if (normalized.Length == 0) {
            return Reset(path, logger);
        }
        PrivateFile.Write(path, strictUtf8.GetBytes(normalized + newLine), logger);
        return new GuidanceText(normalized);
    }

    [UnsupportedOSPlatform("windows")]
    public static GuidanceText Reset(string path, ILogger logger) {
        PrivateFile.Write(path, [], logger);
        return defaultText;
    }

    private static ConfigInvalidException Invalid(string path, ConfigProblem problem) =>
        new(path, [new ConfigError(Path.GetFileName(path), problem)]);

    private static string Validated(string path, string text) {
        string normalized = MatchText.Strip(text
            .Replace(byteOrderMark, string.Empty, StringComparison.Ordinal)
            .Replace(windowsNewLine, newLine, StringComparison.Ordinal)
            .Replace(carriageReturn, newLine, StringComparison.Ordinal));
        if (CodePoints.Count(normalized) > limit) {
            throw new ConfigInvalidException(path, [
                new ConfigError(
                    Path.GetFileName(path), ConfigProblem.TooLong, limit.ToString(CultureInfo.InvariantCulture)),
            ]);
        }
        return normalized;
    }
}
