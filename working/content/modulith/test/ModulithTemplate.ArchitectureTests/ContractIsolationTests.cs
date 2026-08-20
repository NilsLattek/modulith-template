using ArchUnitNET.Fluent;
using ArchUnitNET.xUnitV3;

using static ArchUnitNET.Fluent.ArchRuleDefinition;

namespace ModulithTemplate.ArchitectureTests;

/// <summary>
/// Fences off a feature's <c>Contracts</c> project in both directions, leaving <c>Application</c>
/// as the only layer that may touch one.
/// </summary>
/// <remarks>
/// <see cref="FeatureModuleTests"/> drops Contracts types out of its slices so features can call
/// each other's module APIs, which also blinds it to anything routed through a Contracts project.
/// These rules are what make that exemption safe.
/// </remarks>
public class ContractIsolationTests
{
    private const string ContractsLayer = "Contracts";

    /// <summary>The only layer that may touch a <c>Contracts</c> project.</summary>
    private const string ConsumingLayer = "Application";

    private static readonly string[] FeatureInternalLayers = ["Domain", "Application", "Infrastructure", "Web"];

    /// <summary>Every other internal layer, derived so a new layer needs no second list updated.</summary>
    private static readonly string[] NonConsumingLayers =
        [.. FeatureInternalLayers.Where(layer => !string.Equals(layer, ConsumingLayer, StringComparison.Ordinal))];

    [Fact]
    public void Contracts_do_not_depend_on_any_feature_internal_layer()
    {
        AssertLayerDiscovered(ContractsLayer, "contract purity");

        IArchRule rule = Types().That()
            .ResideInAssemblyMatching(SolutionAssemblies.FeatureLayerPattern(ContractsLayer))
            .And().DoNotResideInNamespaceMatching(SolutionAssemblies.InstrumentationNamespacePattern)
            .Should().NotDependOnAnyTypesThat()
            .ResideInAssemblyMatching(SolutionAssemblies.FeatureLayerPattern(SolutionAssemblies.Alternation(FeatureInternalLayers)))
            .Because("a feature's Contracts project is its published API: it must depend on nothing but the shared "
                + "Shared.Application abstractions, so a consumer referencing it does not transitively gain access "
                + "to the owning feature's internals. Shared.Application also carries the pipeline behaviours, so "
                + "FluentValidation and Logging.Abstractions arrive here transitively — an accepted cost.");

        rule.Check(SolutionAssemblies.Architecture);
    }

    [Fact]
    public void Only_the_Application_layer_depends_on_Contracts()
    {
        AssertLayerDiscovered(ContractsLayer, "contract consumption");
        foreach (var layer in NonConsumingLayers)
        {
            AssertLayerDiscovered(layer, "contract consumption");
        }

        IArchRule rule = Types().That()
            .ResideInAssemblyMatching(SolutionAssemblies.FeatureLayerPattern(SolutionAssemblies.Alternation(NonConsumingLayers)))
            .And().DoNotResideInNamespaceMatching(SolutionAssemblies.InstrumentationNamespacePattern)
            .Should().NotDependOnAnyTypesThat()
            .ResideInAssemblyMatching(SolutionAssemblies.FeatureLayerPattern(ContractsLayer))
            .Because($"only an {ConsumingLayer} layer may consume a Contracts project — its own, which implements the "
                + $"module API, or a neighbouring feature's, which calls it. A {string.Join("/", NonConsumingLayers)} "
                + "layer reaching for one is a cross-feature dependency FeatureModuleTests cannot see.");

        rule.Check(SolutionAssemblies.Architecture);
    }

    /// <summary>Fails loudly on a missing layer assembly, so a rule cannot pass vacuously.</summary>
    /// <param name="layerSuffix">The layer's assembly-name suffix.</param>
    /// <param name="ruleName">The rule the missing layer would silently disarm.</param>
    private static void AssertLayerDiscovered(string layerSuffix, string ruleName) =>
        Assert.True(
            SolutionAssemblies.HasFeatureLayer(layerSuffix),
            $"No assembly for the '{layerSuffix}' feature layer was found; the {ruleName} rule would pass vacuously.");
}
