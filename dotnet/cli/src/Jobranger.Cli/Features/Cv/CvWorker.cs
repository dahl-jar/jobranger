using System.Buffers;
using System.Text;
using System.Text.Json;
using Jobranger.Cli.Platform;

namespace Jobranger.Cli.Features.Cv;

internal static class CvWorker {
    public const string PdfSuffix = ".pdf";
    public const string DocxSuffix = ".docx";
    public const string TextKey = "text";
    public const string ReasonKey = "reason";

    private const ulong memoryLimitBytes = 1UL << 30;
    private const ulong cpuLimitSeconds = 20;
    private const ulong fileSizeLimitBytes = 0;

    public static async Task RunAsync(string suffix, Stream input, Stream output) {
        LimitResources();
        using MemoryStream data = new();
        await input.CopyToAsync(data).ConfigureAwait(false);
        byte[] reply = Reply(suffix, data.ToArray());
        await output.WriteAsync(reply).ConfigureAwait(false);
        await output.FlushAsync().ConfigureAwait(false);
    }

    public static byte[] Reply(string suffix, byte[] data) {
        try {
            return TextReply(Extract(suffix, data));
        } catch (CvUnreadableException error) {
            return Write(ReasonKey, error.Reason);
        } catch (Exception) {
            return Write(ReasonKey, CvUnreadableException.Unreadable);
        }
    }

    public static byte[] TextReply(string text) => Write(TextKey, WithoutLoneSurrogates(text));

    private static string Extract(string suffix, byte[] data) => suffix switch {
        PdfSuffix => PdfText.Extract(data),
        DocxSuffix => DocxText.Extract(data),
        _ => throw new CvUnreadableException(CvUnreadableException.Unreadable),
    };

    private static byte[] Write(string key, string value) {
        ArrayBufferWriter<byte> buffer = new();
        using (Utf8JsonWriter writer = new(buffer)) {
            writer.WriteStartObject();
            writer.WriteString(key, value);
            writer.WriteEndObject();
        }
        return buffer.WrittenSpan.ToArray();
    }

    private static string WithoutLoneSurrogates(string text) {
        StringBuilder kept = new(text.Length);
        ReadOnlySpan<char> rest = text;
        while (!rest.IsEmpty) {
            OperationStatus status = Rune.DecodeFromUtf16(rest, out _, out int consumed);
            if (status == OperationStatus.Done) {
                kept.Append(rest[..consumed]);
            }
            rest = rest[consumed..];
        }
        return kept.ToString();
    }

    private static void LimitResources() {
        if (OperatingSystem.IsWindows()) {
            JobObjects.LimitMemory(memoryLimitBytes);
            return;
        }
        Rlimits.LowerSoft(Rlimits.Resource.Cpu, cpuLimitSeconds);
        Rlimits.LowerSoft(Rlimits.Resource.FileSize, fileSizeLimitBytes);
    }
}
