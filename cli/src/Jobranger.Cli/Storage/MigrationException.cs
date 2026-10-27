using System.Globalization;

namespace Jobranger.Cli.Storage;

internal sealed class MigrationException(int version, Exception inner) : Exception(string.Create(
    CultureInfo.InvariantCulture,
    $"migration {version} failed and was rolled back"), inner);
