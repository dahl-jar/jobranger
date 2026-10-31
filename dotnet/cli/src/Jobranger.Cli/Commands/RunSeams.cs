using System.Collections;
using System.Runtime.Versioning;
using Jobranger.Cli.Config;
using Jobranger.Cli.Features.Delivery;
using Jobranger.Cli.Features.Providers;
using Jobranger.Cli.Features.Sources;
using Jobranger.Cli.Http;
using Jobranger.Cli.Secrets;
using ProvidersEntry = Jobranger.Cli.Features.Providers.Providers;
using SourcesEntry = Jobranger.Cli.Features.Sources.Sources;

namespace Jobranger.Cli.Commands;

[UnsupportedOSPlatform("windows")]
internal sealed record RunSeams(
    Func<IDictionary, bool, CancellationToken, Task<ISecretStore?>> OpenSecrets,
    Func<SourcesConfig, IReadOnlyList<ISource>> EnabledSources,
    Func<TimeProvider, HttpClient> CreateSourceClient,
    RunSeams.ModelOpener OpenModel,
    DeliveryTransport Transport) {
    public static readonly RunSeams Production = new(
        (environment, canPrompt, token) => SecretStores.OpenAsync(environment, canPrompt, token),
        SourcesEntry.Enabled,
        time => SafeHttp.CreateSourceClient(new HostLimiter(time), time),
        (model, credentials, supportsEffort, time, token) =>
            new ProvidersEntry(CreateModelHandler, time).CreateAsync(model, credentials.GetSecretAsync, supportsEffort, token),
        DeliveryTransport.Production);

    public delegate Task<IModelClient> ModelOpener(
        ModelConfig model,
        Credentials credentials,
        bool supportsEffort,
        TimeProvider time,
        CancellationToken cancellationToken);

    private static SocketsHttpHandler CreateModelHandler() => new() { AllowAutoRedirect = false, UseCookies = false };
}
