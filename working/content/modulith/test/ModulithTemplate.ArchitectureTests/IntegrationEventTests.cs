using ArchUnitNET.Fluent;
using ArchUnitNET.Fluent.Syntax.Elements.Types;
using ArchUnitNET.xUnitV3;

using ModulithTemplate.SharedKernel.Application.Events;
using ModulithTemplate.SharedKernel.Outbox.Events;

using Underground.Outbox;

using static ArchUnitNET.Fluent.ArchRuleDefinition;

namespace ModulithTemplate.ArchitectureTests;

/// <summary>
/// Keeps an Integration Event in the one place a sibling feature may reference: its owning
/// feature's <c>Contracts</c> project.
/// </summary>
/// <remarks>
/// Matching nothing passes: a solution whose features never need to talk — or that has only one —
/// rightly declares no Integration Event, so these rules opt out via <c>WithoutRequiringPositiveResults()</c>.
/// </remarks>
public class IntegrationEventTests
{
    private const string ContractsLayer = "Contracts";

    /// <summary>Matches the type name an Integration Event must carry.</summary>
    private const string IntegrationEventNamePattern = ".*IntegrationEvent$";

    [Fact]
    public void Integration_events_are_declared_in_a_Contracts_project()
    {
        IArchRule rule = IntegrationEvents()
            .Should().ResideInAssemblyMatching(SolutionAssemblies.FeatureLayerPattern(ContractsLayer))
            .Because("an Integration Event is the publishing feature's announcement to its siblings, so it must "
                + "live in the one project they may reference. Declared in Domain or Application it still works, "
                + "but every consumer of it takes a dependency on the publisher's internals — and the cross-feature "
                + "rules cannot catch that, because they exempt Contracts types by design.")
            .WithoutRequiringPositiveResults();

        rule.Check(SolutionAssemblies.Architecture);
    }

    [Fact]
    public void Integration_events_are_named_for_what_they_are()
    {
        IArchRule rule = IntegrationEvents()
            .Should().HaveNameMatching(IntegrationEventNamePattern)
            .Because("an Integration Event is read by features that never see the code publishing it, so its name "
                + "must say what it is at the reference site.")
            .WithoutRequiringPositiveResults();

        rule.Check(SolutionAssemblies.Architecture);
    }

    [Fact]
    public void Types_named_as_integration_events_implement_the_contract()
    {
        IArchRule rule = Types().That()
            .ResideInAssemblyMatching(SolutionAssemblies.AnyFeatureLayerPattern)
            .And().HaveNameMatching(IntegrationEventNamePattern)
            .Should().ImplementInterface(typeof(IIntegrationEvent))
            .Because($"a record named *IntegrationEvent that does not implement {nameof(IIntegrationEvent)} cannot "
                + "carry its own EventId or GroupKey, so it can neither be staged in the outbox nor ordered against "
                + "its siblings — and the rules above would never look at it.")
            .WithoutRequiringPositiveResults();

        rule.Check(SolutionAssemblies.Architecture);
    }

    [Fact]
    public void Every_integration_event_has_a_consumer_or_a_forwarder()
    {
        var types = SolutionAssemblies.FeatureAssemblies.SelectMany(assembly => assembly.GetTypes()).ToArray();
        var received = types
            .Where(type => type is { IsAbstract: false, IsClass: true })
            .SelectMany(type => type.GetInterfaces())
            .Where(contract => contract.IsGenericType)
            .Select(ReceivedEvent)
            .OfType<Type>()
            // By name: discovery loads each feature assembly from its file, so the same event can also
            // arrive as a second Type through another assembly's reference to it.
            .Select(type => type.FullName)
            .ToHashSet(StringComparer.Ordinal);

        var unheard = types
            .Where(type => !type.IsAbstract && typeof(IIntegrationEvent).IsAssignableFrom(type))
            .Where(type => !received.Contains(type.FullName))
            .Select(type => type.FullName)
            .ToArray();

        // The publisher throws for such an event, but only once something publishes it.
        Assert.True(
            unheard.Length == 0,
            $"Nothing receives {string.Join(", ", unheard)}. A consuming feature declares an "
                + "IntegrationEventInboxTransport for it under Infrastructure/InboxHandlers/, or an "
                + "IOutboxMessageHandler forwards it outside the process; an event nobody receives should not exist.");

        static Type? ReceivedEvent(Type contract)
        {
            var definition = contract.GetGenericTypeDefinition();
            var message = contract.GetGenericArguments()[0];

            if (definition == typeof(IOutboxMessageHandler<>))
            {
                return message;
            }

            return definition == typeof(IInboxMessageHandler<>)
                && message.IsGenericType
                && message.GetGenericTypeDefinition() == typeof(InboxEnvelope<,>)
                ? message.GetGenericArguments()[1]
                : null;
        }
    }

    [Fact]
    public void No_feature_assembly_overrides_the_pinned_AssemblyVersion()
    {
        // An inbox row stores its envelope's type name, which embeds each type argument's assembly
        // version: a feature releasing under another one orphans every row still pending.
        var pinned = new Version(1, 0, 0, 0);
        var drifted = SolutionAssemblies.FeatureAssemblies
            .Select(assembly => assembly.GetName())
            .Where(name => name.Version != pinned)
            .Select(name => $"{name.Name} ({name.Version})")
            .ToArray();

        Assert.True(
            drifted.Length == 0,
            $"{string.Join(", ", drifted)} override AssemblyVersion, which Directory.Build.props pins to {pinned}. "
                + "Version releases through Version, FileVersion or InformationalVersion instead.");
    }

    /// <summary>Every type implementing the Integration Event contract, wherever it was declared.</summary>
    private static GivenTypesConjunction IntegrationEvents() =>
        Types().That()
            .ResideInAssemblyMatching(SolutionAssemblies.AnyFeatureLayerPattern)
            .And().ImplementInterface(typeof(IIntegrationEvent));
}
