using System.Runtime.InteropServices;
using System.Runtime.Versioning;

namespace Jobranger.Cli.Platform;

[UnsupportedOSPlatform("windows")]
internal static partial class Rlimits {
    private const string macLibc = "libc";
    private const string linuxLibc = "libc.so.6";
    private const ulong macInfinity = long.MaxValue;
    private const ulong linuxInfinity = ulong.MaxValue;
    private const int softIndex = 0;
    private const int hardIndex = 1;
    private const int failed = -1;

    public enum Resource {
        Cpu = 0,
        FileSize = 1,
    }

    public static void LowerSoft(Resource resource, ulong value) {
        Span<ulong> limits = stackalloc ulong[2];
        if (Get((int)resource, limits) == failed) {
            throw new InvalidOperationException($"getrlimit failed resource={resource} errno={Marshal.GetLastPInvokeError()}");
        }
        ulong infinity = OperatingSystem.IsMacOS() ? macInfinity : linuxInfinity;
        ulong lowered = value;
        foreach (ulong limit in limits) {
            if (limit != infinity) {
                lowered = Math.Min(lowered, limit);
            }
        }
        limits[softIndex] = lowered;
        if (Set((int)resource, limits) == failed) {
            throw new InvalidOperationException(
                $"setrlimit failed resource={resource} soft={lowered} hard={limits[hardIndex]} errno={Marshal.GetLastPInvokeError()}");
        }
    }

    private static int Get(int resource, Span<ulong> limits) =>
        OperatingSystem.IsMacOS() ? MacGet(resource, limits) : LinuxGet(resource, limits);

    private static int Set(int resource, Span<ulong> limits) =>
        OperatingSystem.IsMacOS() ? MacSet(resource, limits) : LinuxSet(resource, limits);

    [LibraryImport(macLibc, EntryPoint = "getrlimit", SetLastError = true)]
    private static partial int MacGet(int resource, Span<ulong> limits);

    [LibraryImport(linuxLibc, EntryPoint = "getrlimit", SetLastError = true)]
    private static partial int LinuxGet(int resource, Span<ulong> limits);

    [LibraryImport(macLibc, EntryPoint = "setrlimit", SetLastError = true)]
    private static partial int MacSet(int resource, Span<ulong> limits);

    [LibraryImport(linuxLibc, EntryPoint = "setrlimit", SetLastError = true)]
    private static partial int LinuxSet(int resource, Span<ulong> limits);
}
