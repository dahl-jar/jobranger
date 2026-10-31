namespace Jobranger.Cli.Tests.Mocks;

internal static class FileModes {
    public const UnixFileMode OwnerReadWrite = UnixFileMode.UserRead | UnixFileMode.UserWrite;
    public const UnixFileMode OwnerOnlyDir = OwnerReadWrite | UnixFileMode.UserExecute;
    public const UnixFileMode SharedDir =
        OwnerOnlyDir | UnixFileMode.GroupRead | UnixFileMode.GroupExecute | UnixFileMode.OtherRead
        | UnixFileMode.OtherExecute;
}
