using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging.Console;

namespace Jobranger.Relay.Telemetry;

internal static class ConsoleSetup {
    public static ILoggingBuilder AddRelayJsonConsole(this ILoggingBuilder logging) {
        logging.AddConsole(console => console.FormatterName = RelayJsonFormatter.Name);
        logging.Services.TryAddEnumerable(ServiceDescriptor.Singleton<ConsoleFormatter, RelayJsonFormatter>());
        return logging;
    }
}
