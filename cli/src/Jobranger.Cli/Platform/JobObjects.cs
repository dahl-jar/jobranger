using System.Buffers.Binary;
using System.ComponentModel;
using System.Runtime.InteropServices;
using System.Runtime.Versioning;

namespace Jobranger.Cli.Platform;

[SupportedOSPlatform("windows")]
internal static partial class JobObjects {
    private const string kernel = "kernel32.dll";
    private const int extendedLimitInformation = 9;
    private const uint processMemoryLimitFlag = 0x100;
    private const int informationSize = 144;
    private const int limitFlagsOffset = 16;
    private const int processMemoryLimitOffset = 112;

    public static void LimitMemory(ulong bytes) {
        nint job = CreateJobObject(0, null);
        if (job == 0) {
            throw new Win32Exception(Marshal.GetLastPInvokeError(), "CreateJobObject failed");
        }
        Span<byte> information = stackalloc byte[informationSize];
        information.Clear();
        BinaryPrimitives.WriteUInt32LittleEndian(information[limitFlagsOffset..], processMemoryLimitFlag);
        BinaryPrimitives.WriteUInt64LittleEndian(information[processMemoryLimitOffset..], bytes);
        if (!SetInformationJobObject(job, extendedLimitInformation, information, informationSize)) {
            throw new Win32Exception(Marshal.GetLastPInvokeError(), "SetInformationJobObject failed");
        }
        if (!AssignProcessToJobObject(job, GetCurrentProcess())) {
            throw new Win32Exception(Marshal.GetLastPInvokeError(), "AssignProcessToJobObject failed");
        }
    }

    [LibraryImport(kernel, EntryPoint = "CreateJobObjectW", SetLastError = true, StringMarshalling = StringMarshalling.Utf16)]
    private static partial nint CreateJobObject(nint attributes, string? name);

    [LibraryImport(kernel, SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static partial bool SetInformationJobObject(nint job, int informationClass, ReadOnlySpan<byte> information, uint length);

    [LibraryImport(kernel, SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static partial bool AssignProcessToJobObject(nint job, nint process);

    [LibraryImport(kernel)]
    private static partial nint GetCurrentProcess();
}
