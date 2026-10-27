namespace Jobranger.Relay.Core;

public static class RelayRoles
{
    private const string cliWire = "cli";
    private const string webWire = "web";

    public static string Wire(RelayRole role) => role switch
    {
        RelayRole.Cli => cliWire,
        RelayRole.Web => webWire,
        _ => throw new ArgumentOutOfRangeException(nameof(role), role, "role must be Cli or Web"),
    };

    public static RelayRole Other(RelayRole role) => role == RelayRole.Cli ? RelayRole.Web : RelayRole.Cli;

    public static bool TryParse(string? value, out RelayRole role)
    {
        switch (value)
        {
            case cliWire:
                role = RelayRole.Cli;
                return true;
            case webWire:
                role = RelayRole.Web;
                return true;
            default:
                role = default;
                return false;
        }
    }
}
