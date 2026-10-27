using System.Reflection;
using System.Text.RegularExpressions;
using Jobranger.Cli.Logging;
using Microsoft.Extensions.Logging;

namespace Jobranger.Cli.Tests.Logging;

public sealed partial class LogEventsTests {
    private const BindingFlags anyMethod =
        BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static | BindingFlags.Instance | BindingFlags.DeclaredOnly;

    private static readonly string[] secretWords = ["key", "password", "token", "secret"];

    private static readonly (MethodInfo Method, LoggerMessageAttribute Attribute)[] events = [
        .. typeof(CliServices).Assembly.GetTypes()
            .SelectMany(type => type.GetMethods(anyMethod))
            .Select(method => (method, method.GetCustomAttribute<LoggerMessageAttribute>()))
            .Where(found => found.Item2 is not null)
            .Select(found => (found.method, found.Item2!)),
    ];

    [Fact]
    public void ShouldTakeNoSecretParameter_WhenLogEventIsDeclared() {
        string[] secretParameters = [
            .. events.SelectMany(found => found.Method.GetParameters()
                .Where(parameter => Words(parameter.Name!).Intersect(secretWords).Any())
                .Select(parameter => $"{found.Method.DeclaringType!.Name}.{found.Method.Name}({parameter.Name})")),
        ];

        Assert.NotEmpty(events);
        Assert.Empty(secretParameters);
    }

    [Fact]
    public void ShouldMatchEventPattern_WhenLogEventIsDeclared() {
        string?[] broken = [
            .. events.Select(found => found.Attribute.EventName)
                .Where(name => LogFields.EventName(name) != name),
        ];

        Assert.NotEmpty(events);
        Assert.Empty(broken);
    }

    [Fact]
    public void ShouldAllowEveryPlaceholder_WhenLogEventIsDeclared() {
        string[] dropped = [
            .. events.Where(found => found.Method.DeclaringType == typeof(LogEvents))
                .SelectMany(found => PlaceholderPattern().Matches(found.Attribute.Message))
                .Select(match => LogFields.FieldName(match.Groups[1].Value))
                .Where(name => !LogFields.Allowed.Contains(name)),
        ];

        Assert.NotEmpty(events);
        Assert.Empty(dropped);
    }

    private static IEnumerable<string> Words(string name) =>
        WordPattern().Matches(name).Select(match => match.Value.ToLowerInvariant());

    [GeneratedRegex("[A-Z]?[a-z0-9]+|[A-Z]+(?![a-z])")]
    private static partial Regex WordPattern();

    [GeneratedRegex(@"\{(\w+)\}")]
    private static partial Regex PlaceholderPattern();
}
