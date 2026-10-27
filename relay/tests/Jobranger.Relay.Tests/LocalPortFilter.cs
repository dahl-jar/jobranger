using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;

namespace Jobranger.Relay.Tests;

internal sealed class LocalPortFilter : IStartupFilter
{
    public const string PortHeader = "X-Test-Local-Port";
    public const int HttpPort = 8080;
    public const int MetricsPort = 9464;

    public Action<IApplicationBuilder> Configure(Action<IApplicationBuilder> next) => app =>
    {
        app.Use((context, nextMiddleware) =>
        {
            context.Connection.LocalPort = int.TryParse(context.Request.Headers[PortHeader], out int port) ? port : HttpPort;
            return nextMiddleware(context);
        });
        next(app);
    };
}
