using System.Collections;
using Jobranger.Cli.Config;
using Jobranger.Cli.Secrets;

namespace Jobranger.Cli.Features.Setup;

internal sealed record SetupSeams(
    Func<ProviderName?, HttpMessageHandler> CreateModelHandler,
    Func<IDictionary, bool, CancellationToken, Task<ISecretStore?>> OpenSecrets,
    Func<EmailConfig, string?, CancellationToken, Task<string?>> SendTestEmail);
