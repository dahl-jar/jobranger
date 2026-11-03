using System.Runtime.Versioning;
using Jobranger.Cli.Config;
using Jobranger.Cli.Features.Delivery;
using Jobranger.Cli.Platform;
using Jobranger.Cli.Secrets;
using Jobranger.Cli.Text;
using Spectre.Console;

namespace Jobranger.Cli.Commands;

[UnsupportedOSPlatform("windows")]
internal sealed record InitSeams(
    Func<IAnsiConsole, IMessages, IPrompter?> CreatePrompter,
    Func<ProviderName?, HttpMessageHandler> CreateModelHandler,
    SecretStores.Opener OpenSecrets,
    DeliveryTransport Transport) {
    public static readonly InitSeams Production = new(
        SpectrePrompter.For,
        RunSeams.CreateModelHandler,
        SecretStores.OpenAsync,
        DeliveryTransport.Production);
}
