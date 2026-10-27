using ArchUnitNET.Domain;
using ArchUnitNET.Domain.Dependencies;
using ArchUnitNET.Loader;
using Jobranger.Relay.Composition;
using Jobranger.Relay.Core.Pairing;

namespace Jobranger.Relay.Tests;

public sealed class ArchitectureTests {
    private const string endpointsNamespace = "Jobranger.Relay.Endpoints";
    private const string webHostNamespace = "Microsoft.AspNetCore";
    private const string generatedRoutesNamespace = "Microsoft.AspNetCore.Http.Generated";
    private const char namespaceSeparator = '.';
    private const char nestedTypeSeparator = '+';

    private static readonly string[] routeMappingTypes = [
        "Microsoft.AspNetCore.Builder.EndpointRouteBuilderExtensions",
        "Microsoft.AspNetCore.Routing.RouteGroupBuilder",
    ];

    private static readonly System.Reflection.Assembly core = typeof(PairingStore).Assembly;

    private static readonly System.Reflection.Assembly host = typeof(RelayOptions).Assembly;

    private static readonly Architecture relay = new ArchLoader().LoadAssemblies(core, host).Build();

    [Fact]
    public void ShouldReferenceNoWebHost_WhenTypeIsInCore() {
        string[] violations = [.. RelayDependencies()
            .Where(dependency => IsInAssembly(dependency.Origin, core))
            .Where(dependency => IsWebHostType(dependency.Target))
            .Select(Describe)];

        Assert.Empty(violations);
    }

    [Fact]
    public void ShouldDetectRouteMapping_WhenTypeIsPairingEndpoints() {
        string[] mappers = RouteMappers();

        Assert.Contains($"{endpointsNamespace}.PairingEndpoints", mappers);
    }

    [Fact]
    public void ShouldMapMinimalApiRoutesOnlyInEndpoints() {
        string[] outside = [.. RouteMappers()
            .Where(mapper => !mapper.StartsWith(endpointsNamespace + namespaceSeparator, StringComparison.Ordinal))];

        Assert.Empty(outside);
    }

    private static string[] RouteMappers() =>
        [.. RelayDependencies()
            .Where(dependency => IsInAssembly(dependency.Origin, host))
            .Where(dependency => IsRouteMapping(dependency.Target))
            .Select(dependency => OuterName(dependency.Origin))
            .Distinct()];

    private static IEnumerable<ITypeDependency> RelayDependencies() =>
        relay.Types
            .Where(type => NamespaceOf(type).StartsWith(host.GetName().Name!, StringComparison.Ordinal))
            .SelectMany(type => type.Dependencies);

    private static bool IsRouteMapping(IType type) =>
        routeMappingTypes.Contains(type.FullName) || NamespaceOf(type) == generatedRoutesNamespace;

    private static bool IsWebHostType(IType type) =>
        NamespaceOf(type) == webHostNamespace
        || NamespaceOf(type).StartsWith(webHostNamespace + namespaceSeparator, StringComparison.Ordinal);

    private static bool IsInAssembly(IType type, System.Reflection.Assembly assembly) =>
        type.Assembly.FullName == assembly.FullName;

    private static string NamespaceOf(IType type) => type.Namespace?.FullName ?? string.Empty;

    private static string OuterName(IType type) => type.FullName.Split(nestedTypeSeparator)[0];

    private static string Describe(ITypeDependency dependency) =>
        $"{dependency.Origin.FullName} -> {dependency.Target.FullName}";
}
