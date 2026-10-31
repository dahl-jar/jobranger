using System.Globalization;

namespace Jobranger.Cli.Storage;

internal sealed class SchemaTooNewException(int found, int supported) : Exception(string.Create(
    CultureInfo.InvariantCulture,
    $"database schema {found} is newer than this version supports ({supported})"));
