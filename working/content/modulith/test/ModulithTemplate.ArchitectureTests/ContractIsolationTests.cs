using ArchUnitNET.Fluent;
using ArchUnitNET.xUnitV3;

namespace ModulithTemplate.ArchitectureTests;

/// <summary>
/// Fences off a feature's <c>Contracts</c> project in both directions: it depends on no feature's
/// internals, and only <c>Application</c> and <c>Infrastructure</c> may consume one, their own or a sibling's.
/// </summary>
/// <remarks>
/// <see cref="FeatureModuleTests"/> drops Contracts types out of its slices so features can call
/// each other's module APIs, which also blinds it to anything routed through a Contracts project.
/// These rules are what make that exemption safe.
/// </remarks>
public class ContractIsolationTests
{
    private const string ContractsLayer = "Contracts";

    /// <summary>The layers that may consume any feature's <c>Contracts</c>, as its public contract.</summary>
    private static readonly string[] ConsumingLayers = ["Application", "Infrastructure"];

    private static readonly string[] FeatureInternalLayers = ["Domain", "Application", "Infrastructure", "Web"];

    /// <summary>Every layer that may not reach a Contracts project at all.</summary>
    private static readonly string[] NonConsumingLayers =
        [.. FeatureInternalLayers.Except(ConsumingLayers, StringComparer.Ordinal)];

    // SharedKernel.Application carries the mediator pipeline behaviours, so FluentResults, FluentValidation,
    // and Microsoft.Extensions.Logging.Abstractions arrive in Contracts projects transitively — an accepted cost.
    // Matching no types passes: a freshly scaffolded Contracts project is empty, and the guard below
    // already fails if no Contracts assembly exists at all.
    [Fact]
    public void Contracts_do_not_depend_on_any_feature_internal_layer()
    {
        // Arrange
        AssertLayerDiscovered(ContractsLayer, "contract purity");

        IArchRule rule = SolutionAssemblies.SelectFeatureLayerTypes(SolutionAssemblies.BuildFeatureLayerPattern(ContractsLayer))
            .Should().NotDependOnAny(SolutionAssemblies.SelectFeatureLayerTypes(
                SolutionAssemblies.BuildFeatureLayerPattern(SolutionAssemblies.BuildRegexAlternation(FeatureInternalLayers))))
            .Because("a feature's Contracts project is its published API: it must depend on nothing but the shared "
                + "Shared.Application abstractions, so a consumer referencing it does not transitively gain access "
                + "to the owning feature's internals.")
            .WithoutRequiringPositiveResults();

        // Act & Assert
        rule.Check(SolutionAssemblies.Architecture);
    }

    [Fact]
    public void Only_Application_and_Infrastructure_depend_on_Contracts()
    {
        // Arrange
        AssertLayerDiscovered(ContractsLayer, "contract consumption");
        foreach (var layer in NonConsumingLayers)
        {
            AssertLayerDiscovered(layer, "contract consumption");
        }

        IArchRule rule = SolutionAssemblies.SelectFeatureLayerTypes(
                SolutionAssemblies.BuildFeatureLayerPattern(SolutionAssemblies.BuildRegexAlternation(NonConsumingLayers)))
            .Should().NotDependOnAny(SolutionAssemblies.SelectFeatureLayerTypes(SolutionAssemblies.BuildFeatureLayerPattern(ContractsLayer)))
            .Because($"a Contracts project is consumed by {string.Join(" and ", ConsumingLayers)}: the module API is "
                + "implemented and called from Application, and events are published and consumed through "
                + $"Infrastructure's handlers. A {string.Join("/", NonConsumingLayers)} layer reaching for one is a "
                + "cross-feature dependency FeatureModuleTests cannot see.");

        // Act & Assert
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
