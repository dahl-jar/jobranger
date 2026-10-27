using System.Runtime.InteropServices;
using System.Runtime.Versioning;
using Microsoft.Win32.SafeHandles;

namespace Jobranger.Cli.Files;

[UnsupportedOSPlatform("windows")]
internal static partial class UnixFiles {
    private const string macLibc = "libc";
    private const string linuxLibc = "libc.so.6";
    private const string runtimeNative = "libSystem.Native";
    private const int failed = -1;
    private const int fileStatusSize = 256;
    private const int modeOffset = 4;
    private const int fileTypeMask = 0xF000;
    private const int regularFile = 0x8000;
    private const int readWrite = 0x2;
    private const int writeOnly = 0x1;
    private const int macAppend = 0x8;
    private const int linuxAppend = 0x400;
    private const int lockExclusive = 2;
    private const int lockNonBlocking = 4;
    private const int macNonBlock = 0x4;
    private const int macNoFollow = 0x100;
    private const int macCloseOnExec = 0x1000000;
    private const int linuxNonBlock = 0x800;
    private const int linuxNoFollow = 0x20000;
    private const int linuxArmNoFollow = 0x8000;
    private const int linuxCloseOnExec = 0x80000;
    private const int noEntry = 2;
    private const int alreadyExists = 17;
    private const int macLinkLoop = 62;
    private const int linuxLinkLoop = 40;
    private const int macWouldBlock = 35;
    private const int linuxWouldBlock = 11;
    private const int runtimeWriteOnly = 0x1;
    private const int runtimeCloseOnExec = 0x10;
    private const int runtimeCreate = 0x20;
    private const int runtimeExclusive = 0x40;
    private const int runtimeNoFollow = 0x200;
    private const int createNewFlags =
        runtimeWriteOnly | runtimeCreate | runtimeExclusive | runtimeNoFollow | runtimeCloseOnExec;

    private static readonly int readNoFollowFlags = OperatingSystem.IsMacOS()
        ? macNonBlock | macNoFollow | macCloseOnExec
        : linuxNonBlock | LinuxNoFollow() | linuxCloseOnExec;
    private static readonly int appendNoFollowFlags = OperatingSystem.IsMacOS()
        ? writeOnly | macAppend | macNonBlock | macNoFollow | macCloseOnExec
        : writeOnly | linuxAppend | linuxNonBlock | LinuxNoFollow() | linuxCloseOnExec;

    public static SafeFileHandle? OpenReadNoFollow(string path, out int error) =>
        Open(path, readNoFollowFlags, out error);

    public static SafeFileHandle? OpenLockNoFollow(string path, out int error) =>
        Open(path, readNoFollowFlags | readWrite, out error);

    public static bool TryCreateNew(string path, UnixFileMode mode, out int error) {
        nint descriptor = NativeOpen(path, createNewFlags, (int)mode);
        if (descriptor == failed) {
            error = Marshal.GetLastPInvokeError();
            return false;
        }
        new SafeFileHandle(descriptor, ownsHandle: true).Dispose();
        error = 0;
        return true;
    }

    public static SafeFileHandle? OpenAppendNoFollow(string path, out int error) =>
        Open(path, appendNoFollowFlags, out error);

    public static bool Write(SafeFileHandle handle, ReadOnlySpan<byte> content) {
        ReadOnlySpan<byte> rest = content;
        while (!rest.IsEmpty) {
            int written = NativeWrite(handle, rest, rest.Length);
            if (written <= 0) {
                return false;
            }
            rest = rest[written..];
        }
        return true;
    }

    public static bool IsRegularPath(string path) {
        Span<byte> status = stackalloc byte[fileStatusSize];
        return LinkStatus(path, status) != failed && IsRegular(status);
    }

    public static bool TryLockExclusive(SafeFileHandle handle, out int error) {
        int operation = lockExclusive | lockNonBlocking;
        int result = OperatingSystem.IsMacOS() ? MacLock(handle, operation) : LinuxLock(handle, operation);
        error = result == failed ? Marshal.GetLastPInvokeError() : 0;
        return result != failed;
    }

    public static bool IsMissing(int error) => error == noEntry;

    public static bool IsExisting(int error) => error == alreadyExists;

    public static bool IsLink(int error) => error == (OperatingSystem.IsMacOS() ? macLinkLoop : linuxLinkLoop);

    public static bool IsWouldBlock(int error) =>
        error == (OperatingSystem.IsMacOS() ? macWouldBlock : linuxWouldBlock);

    public static bool IsRegularFile(SafeFileHandle handle) {
        Span<byte> status = stackalloc byte[fileStatusSize];
        return FileStatus(handle, status) != failed && IsRegular(status);
    }

    private static bool IsRegular(ReadOnlySpan<byte> status) =>
        (MemoryMarshal.Read<int>(status[modeOffset..]) & fileTypeMask) == regularFile;

    private static SafeFileHandle? Open(string path, int flags, out int error) {
        int descriptor = OperatingSystem.IsMacOS() ? MacOpen(path, flags) : LinuxOpen(path, flags);
        if (descriptor == failed) {
            error = Marshal.GetLastPInvokeError();
            return null;
        }
        error = 0;
        return new SafeFileHandle(descriptor, ownsHandle: true);
    }

    private static int LinuxNoFollow() =>
        RuntimeInformation.ProcessArchitecture is Architecture.Arm64 or Architecture.Arm
            ? linuxArmNoFollow
            : linuxNoFollow;

    [LibraryImport(macLibc, EntryPoint = "open", StringMarshalling = StringMarshalling.Utf8, SetLastError = true)]
    private static partial int MacOpen(string path, int flags);

    [LibraryImport(linuxLibc, EntryPoint = "open", StringMarshalling = StringMarshalling.Utf8, SetLastError = true)]
    private static partial int LinuxOpen(string path, int flags);

    [LibraryImport(runtimeNative, EntryPoint = "SystemNative_Open", StringMarshalling = StringMarshalling.Utf8, SetLastError = true)]
    private static partial nint NativeOpen(string path, int flags, int mode);

    [LibraryImport(runtimeNative, EntryPoint = "SystemNative_Write", SetLastError = true)]
    private static partial int NativeWrite(SafeFileHandle handle, ReadOnlySpan<byte> buffer, int bufferSize);

    [LibraryImport(runtimeNative, EntryPoint = "SystemNative_LStat", StringMarshalling = StringMarshalling.Utf8)]
    private static partial int LinkStatus(string path, Span<byte> status);

    [LibraryImport(runtimeNative, EntryPoint = "SystemNative_FStat")]
    private static partial int FileStatus(SafeFileHandle handle, Span<byte> status);

    [LibraryImport(macLibc, EntryPoint = "flock", SetLastError = true)]
    private static partial int MacLock(SafeFileHandle handle, int operation);

    [LibraryImport(linuxLibc, EntryPoint = "flock", SetLastError = true)]
    private static partial int LinuxLock(SafeFileHandle handle, int operation);
}
