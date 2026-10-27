using System.Runtime.InteropServices;
using System.Runtime.Versioning;

namespace Jobranger.Cli.Secrets;

[SupportedOSPlatform("macos")]
internal static partial class MacSecurity {
    private const string securityPath = "/System/Library/Frameworks/Security.framework/Security";
    private const string coreFoundationPath = "/System/Library/Frameworks/CoreFoundation.framework/CoreFoundation";
    private const uint utf8Encoding = 0x08000100;

    private static readonly nint security = NativeLibrary.Load(securityPath);
    private static readonly nint coreFoundation = NativeLibrary.Load(coreFoundationPath);

    public static readonly nint KeyCallbacks = NativeLibrary.GetExport(coreFoundation, "kCFTypeDictionaryKeyCallBacks");
    public static readonly nint ValueCallbacks = NativeLibrary.GetExport(coreFoundation, "kCFTypeDictionaryValueCallBacks");
    public static readonly nint BooleanTrue = Constant(coreFoundation, "kCFBooleanTrue");
    public static readonly nint ClassKey = Constant(security, "kSecClass");
    public static readonly nint GenericPassword = Constant(security, "kSecClassGenericPassword");
    public static readonly nint ServiceKey = Constant(security, "kSecAttrService");
    public static readonly nint AccountKey = Constant(security, "kSecAttrAccount");
    public static readonly nint ValueDataKey = Constant(security, "kSecValueData");
    public static readonly nint ReturnDataKey = Constant(security, "kSecReturnData");
    public static readonly nint MatchLimitKey = Constant(security, "kSecMatchLimit");
    public static readonly nint MatchLimitOne = Constant(security, "kSecMatchLimitOne");

    public static nint CreateString(string text) => CreateCFString(nint.Zero, text, utf8Encoding);

    public static nint CreateData(byte[] bytes) => CreateCFData(nint.Zero, bytes, bytes.Length);

    public static byte[] Bytes(nint data) {
        byte[] bytes = new byte[(int)DataLength(data)];
        Marshal.Copy(DataBytes(data), bytes, 0, bytes.Length);
        return bytes;
    }

    [LibraryImport(securityPath, EntryPoint = "SecItemCopyMatching")]
    public static partial int CopyMatching(nint query, out nint result);

    [LibraryImport(securityPath, EntryPoint = "SecItemAdd")]
    public static partial int AddItem(nint attributes, nint result);

    [LibraryImport(securityPath, EntryPoint = "SecItemDelete")]
    public static partial int DeleteItem(nint query);

    [LibraryImport(coreFoundationPath, EntryPoint = "CFDictionaryCreate")]
    public static partial nint CreateDictionary(
        nint allocator, nint[] keys, nint[] values, nint count, nint keyCallbacks, nint valueCallbacks);

    private static nint Constant(nint library, string symbol) => Marshal.ReadIntPtr(NativeLibrary.GetExport(library, symbol));

    [LibraryImport(coreFoundationPath, EntryPoint = "CFStringCreateWithCString", StringMarshalling = StringMarshalling.Utf8)]
    private static partial nint CreateCFString(nint allocator, string text, uint encoding);

    [LibraryImport(coreFoundationPath, EntryPoint = "CFDataCreate")]
    private static partial nint CreateCFData(nint allocator, byte[] bytes, nint length);

    [LibraryImport(coreFoundationPath, EntryPoint = "CFDataGetBytePtr")]
    private static partial nint DataBytes(nint data);

    [LibraryImport(coreFoundationPath, EntryPoint = "CFDataGetLength")]
    private static partial nint DataLength(nint data);

    [LibraryImport(coreFoundationPath, EntryPoint = "CFRelease")]
    private static partial void Release(nint value);

    internal sealed class CoreFoundationObjects : IDisposable {
        private readonly List<nint> created = [];

        public nint Add(nint value) {
            created.Add(value);
            return value;
        }

        public void Dispose() {
            foreach (nint value in created.Where(value => value != nint.Zero)) {
                Release(value);
            }
        }
    }
}
