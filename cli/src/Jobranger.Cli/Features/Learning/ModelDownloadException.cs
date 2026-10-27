using System.Globalization;

namespace Jobranger.Cli.Features.Learning;

internal sealed class ModelDownloadException(string fileName, TimeSpan idleTimeout)
    : Exception(
        $"{fileName} download stalled, no bytes for "
        + $"{idleTimeout.TotalSeconds.ToString(CultureInfo.InvariantCulture)} s");
