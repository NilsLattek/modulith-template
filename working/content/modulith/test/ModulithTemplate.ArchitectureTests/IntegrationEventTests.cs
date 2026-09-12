using ArchUnitNET.Fluent;
using ArchUnitNET.Fluent.Syntax.Elements.Types;
using ArchUnitNET.xUnitV3;

using ModulithTemplate.SharedKernel.Application.Events;

using static ArchUnitNET.Fluent.ArchRuleDefinition;

namespace ModulithTemplate.ArchitectureTests;

/// <summary>
/// Keeps an Integration Event in the one place a sibling feature may reference: its owning
/// feature's <c>Contracts</c> project.
/// </summary>
/// <remarks>
/// <see cref="ContractIsolationTests"/> cannot see this. It deliberately exempts Contracts types
/// from the cross-feature rules so features can reference each other's published API, which means an
/// event declared in <c>Domain</c> or <c>Application</c> instead compiles, works, and quietly hands
/// its consumer a dependency on the publishing feature's internals.
/// <para>
/// Both directions, because neither implies the other: a correctly named record in the wrong
/// project and an <see cref="IIntegrationEvent"/> named anything else are the same mistake.
/// <see cref="NamingConventionTests"/> governs the namespace within the project.
/// </para>
/// </remarks>
public class IntegrationEventTests
{
    private const string ContractsLayer = "Contracts";

    /// <summary>Matches the type name an Integration Event must carry.</summary>
    private const string IntegrationEventNamePattern = ".*IntegrationEvent$";

    [Fact]
    public void Integration_events_are_declared_in_a_Contracts_project()
    {
        AssertAnyIntegrationEventDiscovered();

        IArchRule rule = IntegrationEvents()
            .Should().ResideInAssemblyMatching(SolutionAssemblies.FeatureLayerPattern(ContractsLayer))
            .Because("an Integration Event is the publishing feature's announcement to its siblings, so it must "
                + "live in the one project they may reference. Declared in Domain or Application it still works, "
                + "but every consumer of it takes a dependency on the publisher's internals — and the cross-feature "
                + "rules cannot catch that, because they exempt Contracts types by design.");

        rule.Check(SolutionAssemblies.Architecture);
    }

    [Fact]
    public void Integration_events_are_named_for_what_they_are()
    {
        AssertAnyIntegrationEventDiscovered();

        IArchRule rule = IntegrationEvents()
            .Should().HaveNameMatching(IntegrationEventNamePattern)
            .Because("an Integration Event is read by features that never see the code publishing it, so its name "
                + "must say what it is at the reference site.");

        rule.Check(SolutionAssemblies.Architecture);
    }

    [Fact]
    public void Types_named_as_integration_events_implement_the_contract()
    {
        AssertAnyIntegrationEventDiscovered();

        IArchRule rule = Types().That()
            .ResideInAssemblyMatching(SolutionAssemblies.AnyFeatureLayerPattern)
            .And().HaveNameMatching(IntegrationEventNamePattern)
            .Should().ImplementInterface(typeof(IIntegrationEvent))
            .Because($"a record named *IntegrationEvent that does not implement {nameof(IIntegrationEvent)} cannot "
                + "carry its own EventId or GroupKey, so it can neither be staged in the outbox nor ordered against "
                + "its siblings — and the rules above would never look at it.");

        rule.Check(SolutionAssemblies.Architecture);
    }

    /// <summary>Every type implementing the Integration Event contract, wherever it was declared.</summary>
    private static GivenTypesConjunction IntegrationEvents() =>
        Types().That()
            .ResideInAssemblyMatching(SolutionAssemblies.AnyFeatureLayerPattern)
            .And().ImplementInterface(typeof(IIntegrationEvent));

    /// <summary>
    /// Fails loudly when the solution declares no Integration Event, so deleting the sample one
    /// silently disarms these rules rather than leaving them to pass on an empty set.
    /// </summary>
    private static void AssertAnyIntegrationEventDiscovered() =>
        Assert.True(
            IntegrationEvents().GetObjects(SolutionAssemblies.Architecture).Any(),
            $"No type implementing {nameof(IIntegrationEvent)} was found; the Integration Event placement and "
                + "naming rules would pass vacuously.");
}
