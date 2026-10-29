using ArchUnitNET.Fluent;
using ArchUnitNET.Fluent.Syntax.Elements.Types;
using ArchUnitNET.xUnitV3;

using Mediator;

using ModulithTemplate.SharedKernel.Application.Events;

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
    public void Every_integration_event_has_an_outbox_handler()
    {
        var handledEventNames = ClosedInterfacesImplementedByFeatureTypes(typeof(IOutboxMessageHandler<>))
            .Select(contract => contract.GetGenericArguments()[0].FullName)
            .ToHashSet(StringComparer.Ordinal);

        var undelivered = SolutionAssemblies.FeatureTypes
            .Where(type => !type.IsAbstract && typeof(IIntegrationEvent).IsAssignableFrom(type))
            .Where(type => !handledEventNames.Contains(type.FullName))
            .Select(type => type.FullName)
            .ToArray();

        // The mediator's MSG0005 catches an event with no Consumer at all, but not one whose outbox row
        // nothing claims: that one is published, saved, and never delivered.
        Assert.True(
            undelivered.Length == 0,
            $"Nothing delivers {string.Join(", ", undelivered)} from the outbox. The publishing feature declares a "
                + "body-less IntegrationEventOutboxHandler for it under Infrastructure/OutboxHandlers/.");
    }

    [Fact]
    public void Plain_notification_handlers_of_integration_events_live_in_Infrastructure()
    {
        var misplaced = SolutionAssemblies.FeatureTypes
            .Where(type => type is { IsAbstract: false, IsClass: true }
                && !IsInboxHandler(type)
                && HandlesIntegrationEventAsPlainNotification(type)
                && !type.Assembly.GetName().Name!.EndsWith(".Infrastructure", StringComparison.Ordinal))
            .Select(type => type.FullName)
            .ToArray();

        // A plain handler runs at least once, beside every Consumer's staging; it is for forwarding the
        // event outside the process. Written in Application, it is a Consumer that skipped its inbox.
        Assert.True(
            misplaced.Length == 0,
            $"{string.Join(", ", misplaced)} handle an Integration Event outside Infrastructure. A feature reacting "
                + "to one declares an IntegrationEventInboxHandler under Infrastructure/InboxHandlers/.");

        static bool IsInboxHandler(Type type) =>
            type.GetInterfaces().Any(contract =>
                contract.IsGenericType && contract.GetGenericTypeDefinition() == typeof(IInboxMessageHandler<>));

        static bool HandlesIntegrationEventAsPlainNotification(Type type) =>
            type.GetInterfaces().Any(contract =>
                contract.IsGenericType
                && contract.GetGenericTypeDefinition() == typeof(INotificationHandler<>)
                && typeof(IIntegrationEvent).IsAssignableFrom(contract.GetGenericArguments()[0]));
    }

    [Fact]
    public void No_feature_assembly_overrides_the_pinned_AssemblyVersion()
    {
        // An inbox row stores its envelope's type name, which embeds each type argument's assembly
        // version: a feature releasing under another one orphans every row still pending.
        var pinned = new Version(1, 0, 0, 0);
        var assemblyNames = SolutionAssemblies.FeatureAssemblies.Select(assembly => assembly.GetName());
        var drifted = assemblyNames
            .Where(name => name.Version != pinned)
            .Select(name => $"{name.Name} ({name.Version})")
            .ToArray();

        Assert.True(
            drifted.Length == 0,
            $"{string.Join(", ", drifted)} override AssemblyVersion, which Directory.Build.props pins to {pinned}. "
                + "Version releases through Version, FileVersion or InformationalVersion instead.");
    }

    /// <summary>The closed forms of a generic interface that concrete feature types implement.</summary>
    private static IEnumerable<Type> ClosedInterfacesImplementedByFeatureTypes(Type definition) =>
        SolutionAssemblies.FeatureTypes
            .Where(type => type is { IsAbstract: false, IsClass: true })
            .SelectMany(type => type.GetInterfaces())
            .Where(contract => contract.IsGenericType && contract.GetGenericTypeDefinition() == definition);

    /// <summary>Every type implementing the Integration Event contract, wherever it was declared.</summary>
    private static GivenTypesConjunction IntegrationEvents() =>
        Types().That()
            .ResideInAssemblyMatching(SolutionAssemblies.AnyFeatureLayerPattern)
            .And().ImplementInterface(typeof(IIntegrationEvent));
}
