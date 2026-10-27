using System.Globalization;
using System.Text;
using System.Text.Json;

namespace Jobranger.Contracts.Payloads;

internal static class InstancePaths {
    private const char separator = '/';

    public static string FromPointer(string pointer, JsonElement instance) {
        StringBuilder path = new(ContractException.RootPath);
        JsonElement current = instance;
        foreach (string name in pointer.Split(separator, StringSplitOptions.RemoveEmptyEntries)) {
            if (current.ValueKind == JsonValueKind.Array) {
                int index = int.Parse(name, CultureInfo.InvariantCulture);
                _ = path.Append('[').Append(index).Append(']');
                current = current[index];
            } else {
                _ = path.Append('.').Append(name);
                current = current.GetProperty(name);
            }
        }
        return path.ToString();
    }
}
