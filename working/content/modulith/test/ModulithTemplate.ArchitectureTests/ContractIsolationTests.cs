using ArchUnitNET.Fluent;
using ArchUnitNET.xUnitV3;

using static ArchUnitNET.Fluent.ArchRuleDefinition;

namespace ModulithTemplate.ArchitectureTests;

/// <summary>
/// Enforces that a feature's <c>Contracts</c> project stays a contract.
/// </summary>
/// <remarks>
/// <see cref="FeatureModuleTests"/> forbids features from depending on each other and exempts
/// <c>Contracts</c> from that rule, which is what lets one feature subscribe to another's events or
/// call its module API. That exemption is only safe while a Contracts project is genuinely
/// self-contained: the moment one references its own feature's Domain, every consumer inherits
/// visibility of that feature's entities, repositories and domain services, and the isolation the
/// exemption was granted for is gone — with the cross-feature rule still passing, because the
/// dependency now routes through an ignored namespace.
/// <para>
/// This is the rule that closes that hole, and it is why the exemption can be trusted.
/// </para>
/// </remarks>
public class ContractIsolationTests
{
    private const string ContractsLayer = "Contracts";

    private static readonly string[] FeatureInternalLayers = ["Domain", "Application", "Infrastructure", "Web"];

    [Fact]
    public void Contracts_do_not_depend_on_any_feature_internal_layer()
    {
        // Guard: fail loudly if no Contracts assembly was discovered, so the rule cannot pass
        // vacuously on a solution where the projects were renamed or never built.
        Assert.True(
            SolutionAssemblies.HasFeatureLayer(ContractsLayer),
            $"No assembly for the '{ContractsLayer}' feature layer was found; the contract purity rule would pass vacuously.");

        IArchRule rule = Types().That()
            .ResideInAssemblyMatching(SolutionAssemblies.FeatureLayerPattern(ContractsLayer))
            .Should().NotDependOnAnyTypesThat()
            .ResideInAssemblyMatching(SolutionAssemblies.FeatureLayerPattern(SolutionAssemblies.Alternation(FeatureInternalLayers)))
            .Because("a feature's Contracts project is its published API: it must depend on nothing but the shared "
                + "Application.Common abstractions, so that a consumer referencing it does not transitively gain "
                + "access to the owning feature's Domain, Application, Infrastructure or Web layer.");

        rule.Check(SolutionAssemblies.Architecture);
    }
}
