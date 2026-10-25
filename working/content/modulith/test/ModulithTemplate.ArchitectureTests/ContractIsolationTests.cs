using ArchUnitNET.Fluent;
using ArchUnitNET.xUnitV3;

namespace ModulithTemplate.ArchitectureTests;

/// <summary>
/// Fences off a feature's <c>Contracts</c> project in both directions: <c>Application</c> may consume
/// any, <c>Infrastructure</c> its own and, from its inbox handlers, a sibling's, and no other layer may
/// touch one at all.
/// </summary>
/// <remarks>
/// <see cref="FeatureModuleTests"/> drops Contracts types out of its slices so features can call
/// each other's module APIs, which also blinds it to anything routed through a Contracts project.
/// These rules are what make that exemption safe.
/// </remarks>
public class ContractIsolationTests
{
    private const string ContractsLayer = "Contracts";

    /// <summary>The layer that may consume any feature's <c>Contracts</c>, its own and a sibling's.</summary>
    private const string ConsumingLayer = "Application";

    /// <summary>The layer that may consume its <b>own</b> feature's <c>Contracts</c>, and a sibling's only from its inbox handlers.</summary>
    /// <remarks>
    /// An outbox handler forwarding this feature's event names its own contract; an inbox handler under
    /// <c>Infrastructure/InboxHandlers/</c> names the sibling's event it consumes. Anywhere else in this
    /// layer a sibling's Contracts would be a cross-feature dependency with none of that justification.
    /// </remarks>
    private const string OwnContractsOnlyLayer = "Infrastructure";

    /// <summary>The namespace within <see cref="OwnContractsOnlyLayer"/> that may name a sibling's event.</summary>
    private const string InboxHandlersNamespacePattern = @".*\.Infrastructure\.InboxHandlers(\..*)?$";

    /// <summary>Where the outbox source generator emits its registration, which names every handled type.</summary>
    private const string HandlerRegistrationNamespacePattern = @"^Underground\.Outbox\.Configuration$";

    private static readonly string[] FeatureInternalLayers = ["Domain", "Application", "Infrastructure", "Web"];

    /// <summary>Every layer that may not reach a Contracts project at all, derived from the two above.</summary>
    private static readonly string[] NonConsumingLayers =
        [.. FeatureInternalLayers.Where(layer =>
            !string.Equals(layer, ConsumingLayer, StringComparison.Ordinal)
            && !string.Equals(layer, OwnContractsOnlyLayer, StringComparison.Ordinal))];

    // SharedKernel.Application carries the mediator pipeline behaviours, so FluentResults, FluentValidation,
    // and Microsoft.Extensions.Logging.Abstractions arrive in Contracts projects transitively — an accepted cost.
    // Matching no types passes: a freshly scaffolded Contracts project is empty, and the guard below
    // already fails if no Contracts assembly exists at all.
    [Fact]
    public void Contracts_do_not_depend_on_any_feature_internal_layer()
    {
        AssertLayerDiscovered(ContractsLayer, "contract purity");

        IArchRule rule = SolutionAssemblies.FeatureLayerTypes(SolutionAssemblies.FeatureLayerPattern(ContractsLayer))
            .Should().NotDependOnAny(SolutionAssemblies.FeatureLayerTypes(
                SolutionAssemblies.FeatureLayerPattern(SolutionAssemblies.Alternation(FeatureInternalLayers))))
            .Because("a feature's Contracts project is its published API: it must depend on nothing but the shared "
                + "Shared.Application abstractions, so a consumer referencing it does not transitively gain access "
                + "to the owning feature's internals.")
            .WithoutRequiringPositiveResults();

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

        IArchRule rule = SolutionAssemblies.FeatureLayerTypes(
                SolutionAssemblies.FeatureLayerPattern(SolutionAssemblies.Alternation(NonConsumingLayers)))
            .Should().NotDependOnAny(SolutionAssemblies.FeatureLayerTypes(SolutionAssemblies.FeatureLayerPattern(ContractsLayer)))
            .Because($"only an {ConsumingLayer} layer may consume any Contracts project — its own, which implements "
                + "the module API, or a neighbouring feature's, which calls it. A "
                + $"{string.Join("/", NonConsumingLayers)} layer reaching for one is a cross-feature dependency "
                + $"FeatureModuleTests cannot see. {OwnContractsOnlyLayer} sits between the two: it may name its "
                + "own feature's contract, and the rule below is what holds it to that.");

        rule.Check(SolutionAssemblies.Architecture);
    }

    /// <summary>The Infrastructure exception, held to the feature that owns the contract outside its inbox handlers.</summary>
    /// <param name="feature">The feature whose Infrastructure layer is checked.</param>
    [Theory]
    [MemberData(nameof(FeatureNames))]
    public void Infrastructure_depends_on_a_siblings_Contracts_only_from_its_inbox_handlers(string feature)
    {
        var siblings = SolutionAssemblies.FeatureNames
            .Where(other => !string.Equals(other, feature, StringComparison.Ordinal))
            .ToArray();

        // A solution with one feature has no sibling to reach for, so there is nothing to assert.
        if (siblings.Length == 0)
        {
            return;
        }

        IArchRule rule = SolutionAssemblies.FeatureLayerTypes(
                SolutionAssemblies.NamedFeatureLayerPattern(feature, OwnContractsOnlyLayer))
            .And().DoNotResideInNamespaceMatching(InboxHandlersNamespacePattern)
            .And().DoNotResideInNamespaceMatching(HandlerRegistrationNamespacePattern)
            .Should().NotDependOnAny(SolutionAssemblies.FeatureLayerTypes(
                SolutionAssemblies.NamedFeatureLayerPattern(SolutionAssemblies.Alternation(siblings), ContractsLayer)))
            .Because($"{feature}'s {OwnContractsOnlyLayer} layer may name its own published contract, and a "
                + "sibling's event only from an inbox handler consuming it; anywhere else that is the cross-feature "
                + $"dependency an {ConsumingLayer} layer is the only one allowed to take on purpose.")
            .WithoutRequiringPositiveResults();

        rule.Check(SolutionAssemblies.Architecture);
    }

    /// <summary>Every discovered feature, for the theory above.</summary>
    public static TheoryData<string> FeatureNames => new(SolutionAssemblies.FeatureNames);

    /// <summary>Fails loudly on a missing layer assembly, so a rule cannot pass vacuously.</summary>
    /// <param name="layerSuffix">The layer's assembly-name suffix.</param>
    /// <param name="ruleName">The rule the missing layer would silently disarm.</param>
    private static void AssertLayerDiscovered(string layerSuffix, string ruleName) =>
        Assert.True(
            SolutionAssemblies.HasFeatureLayer(layerSuffix),
            $"No assembly for the '{layerSuffix}' feature layer was found; the {ruleName} rule would pass vacuously.");
}
