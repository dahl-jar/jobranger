using Jobranger.Cli.Config;
using Jobranger.Cli.Secrets;

namespace Jobranger.Cli.Features.Setup;

internal sealed record SetupSeams(
    Func<ProviderName?, HttpMessageHandler> CreateModelHandler,
    SecretStores.Opener OpenSecrets,
    Func<EmailConfig, string?, CancellationToken, Task<string?>> SendTestEmail);
