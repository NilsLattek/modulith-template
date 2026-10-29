using ArchUnitNET.Domain;
using ArchUnitNET.Fluent;
using ArchUnitNET.Loader;
using ArchUnitNET.xUnitV3;

namespace ModulithTemplate.ArchitectureTests;

/// <summary>
/// The host references every feature's <c>Domain</c> only so the mediator source generator sees
/// its domain events; its own code must still not name a domain type.
/// </summary>
public class HostTests
{
    /// <summary>Namespaces the mediator source generator emits into the host.</summary>
    private const string GeneratedMediatorNamespacePattern =
        @"^(Mediator|Microsoft\.Extensions\.DependencyInjection)(\..*)?$";

    private static readonly Lazy<Architecture> HostArchitecture = new(() =>
        new ArchLoader().LoadAssemblies([typeof(Program).Assembly, .. SolutionAssemblies.FeatureAssemblies]).Build());

    [Fact]
    public void Host_does_not_depend_on_any_feature_Domain()
    {
        Assert.True(SolutionAssemblies.HasFeatureLayer("Domain"),
            "No feature Domain assembly was found; the rule would pass vacuously.");

        IArchRule rule = ArchRuleDefinition.Types().That()
            .ResideInAssembly(typeof(Program).Assembly)
            .And().DoNotResideInNamespaceMatching(GeneratedMediatorNamespacePattern)
            .Should().NotDependOnAny(SolutionAssemblies.SelectFeatureLayerTypes(SolutionAssemblies.BuildFeatureLayerPattern("Domain")))
            .Because("the host references each Domain only for the mediator generator; entities stay behind the feature's Application layer.");

        rule.Check(HostArchitecture.Value);
    }
}
