using System.Text.RegularExpressions;

using ArchUnitNET.Fluent;
using ArchUnitNET.xUnitV3;

using static ArchUnitNET.Fluent.ArchRuleDefinition;

namespace ModulithTemplate.ArchitectureTests;

/// <summary>
/// Enforces the naming and placement conventions for the feature building blocks described in
/// CLAUDE.md: specifications, mappers, domain services, commands, queries, DTOs, the module API and
/// the two kinds of event.
/// </summary>
/// <remarks>
/// Each convention is checked in <b>both</b> directions, and the two directions are driven by two
/// separate tables because they are not each other's mirror image:
/// <list type="bullet">
/// <item><description>
/// <see cref="Placements"/> answers "where may a type with this suffix live?" — and the answer is
/// sometimes more than one namespace. A <c>*Dto</c> is legitimate both as a feature's internal read
/// model and as part of its published contract; a <c>*Api</c> type is legitimate both as the
/// published interface and as the implementation behind it.
/// </description></item>
/// <item><description>
/// <see cref="Contents"/> answers "what may live in this namespace?" — and the answer is sometimes
/// more than one suffix. <c>Contracts.Api</c> holds the interface <i>and</i> the DTOs it returns.
/// </description></item>
/// </list>
/// Collapsing the two into one table is what a single-suffix-per-namespace assumption buys, and it
/// stops being true the moment a namespace holds an interface alongside its read models.
/// <para>
/// These rules deliberately carry no "assembly was discovered" presence guard, and none should be
/// added. A layering rule that matches nothing is a false green over code that exists; a naming rule
/// that matches nothing simply means the solution has no specifications yet, which is the normal
/// state of a freshly scaffolded project. These are conditional tripwires armed for code the user
/// has not written yet. ArchUnitNET's default behavior is the opposite of what that requires — a
/// rule whose predicate matches zero types throws rather than passing — so each helper opts out via
/// <c>WithoutRequiringPositiveResults()</c>. That still fails on any real violation among the types
/// that do match. The <c>AreNotNested()</c> filter is not load-bearing — ArchUnitNET already
/// excludes compiler-generated nested types — it is insurance against a hand-written nested type.
/// </para>
/// <para>
/// Every rule is scoped to types in a feature assembly. The shared projects define abstractions
/// whose names deliberately match these suffixes — <c>IIntegrationEvent</c> and
/// <c>IDomainEventHandler</c> in Application.Common — and those are contracts to implement, not
/// misplaced feature code. These conventions govern where a <i>feature</i> puts its own types.
/// </para>
/// </remarks>
public class NamingConventionTests
{
    /// <summary>Which namespaces a type carrying one of these suffixes may live in.</summary>
    private sealed record PlacementRule(string Name, string[] TypeSuffixes, string[] HomeNamespaces);

    /// <summary>Which suffixes a type living in this namespace may carry.</summary>
    private sealed record ContentRule(string Namespace, string[] AllowedSuffixes);

    private static readonly PlacementRule[] Placements =
    [
        new("specification", ["Spec"], ["Domain.Specifications"]),
        new("mapper", ["Mapper"], ["Application.Mappers"]),
        new("domain service", ["DomainService"], ["Domain.Services"]),
        new("command", ["Command", "CommandHandler", "CommandValidator"], ["Application.Commands"]),
        new("query", ["Query", "QueryHandler", "QueryValidator"], ["Application.Queries"]),

        // Two homes, meaning different things. Application.Dtos is the feature's own read model,
        // free to change with the feature. Contracts.Api is published, and changing it breaks every
        // consumer compiled against it.
        new("dto", ["Dto"], ["Application.Dtos", "Contracts.Api"]),

        // Likewise: the interface is published from Contracts.Api, while the implementation stays in
        // the owning feature's Application.Api, where it can reach the repository.
        new("module api", ["Api"], ["Contracts.Api", "Application.Api"]),

        new("integration event", ["IntegrationEvent"], ["Contracts.IntegrationEvents"]),
        new("integration event handler", ["IntegrationEventHandler"], ["Application.IntegrationEventHandlers"]),
        new("domain event", ["DomainEvent"], ["Domain.Events"]),
        new("domain event handler", ["DomainEventHandler"], ["Application.DomainEventHandlers"]),
    ];

    private static readonly ContentRule[] Contents =
    [
        new("Domain.Specifications", ["Spec"]),
        new("Application.Mappers", ["Mapper"]),
        new("Domain.Services", ["DomainService"]),
        new("Application.Commands", ["Command", "CommandHandler", "CommandValidator"]),
        new("Application.Queries", ["Query", "QueryHandler", "QueryValidator"]),
        new("Application.Dtos", ["Dto"]),

        // The published surface: the interface and the read models it returns, side by side.
        new("Contracts.Api", ["Api", "Dto"]),

        new("Application.Api", ["Api"]),
        new("Contracts.IntegrationEvents", ["IntegrationEvent"]),
        new("Application.IntegrationEventHandlers", ["IntegrationEventHandler"]),
        new("Domain.Events", ["DomainEvent"]),
        new("Application.DomainEventHandlers", ["DomainEventHandler"]),
    ];

    /// <summary>Names of every placement rule, for the theory below.</summary>
    public static TheoryData<string> PlacementNames => new(Placements.Select(rule => rule.Name));

    /// <summary>Every governed namespace, for the theory below.</summary>
    public static TheoryData<string> ContentNamespaces => new(Contents.Select(rule => rule.Namespace));

    /// <summary>A type carrying a convention's suffix must live in one of that convention's homes.</summary>
    /// <param name="ruleName">The placement rule to check.</param>
    [Theory]
    [MemberData(nameof(PlacementNames))]
    public void Types_carrying_a_convention_suffix_reside_in_a_home_namespace(string ruleName)
    {
        var rule = Placements.Single(candidate => string.Equals(candidate.Name, ruleName, StringComparison.Ordinal));

        IArchRule archRule = Types().That()
            .ResideInAssemblyMatching(FeatureAssemblyPattern)
            .And().HaveNameMatching(TypeNamePattern(rule.TypeSuffixes))
            .And().AreNotNested()
            .Should().ResideInNamespaceMatching(FeatureNamespacePattern(rule.HomeNamespaces))
            .Because($"a {rule.Name} must live in its feature's {string.Join(" or ", rule.HomeNamespaces)} namespace.")
            .WithoutRequiringPositiveResults();

        archRule.Check(SolutionAssemblies.Architecture);
    }

    /// <summary>A type in a governed namespace must carry one of the suffixes that namespace allows.</summary>
    /// <param name="namespaceSuffix">The namespace to check.</param>
    [Theory]
    [MemberData(nameof(ContentNamespaces))]
    public void Types_in_a_convention_namespace_carry_one_of_its_suffixes(string namespaceSuffix)
    {
        var rule = Contents.Single(candidate => string.Equals(candidate.Namespace, namespaceSuffix, StringComparison.Ordinal));

        IArchRule archRule = Types().That()
            .ResideInAssemblyMatching(FeatureAssemblyPattern)
            .And().ResideInNamespaceMatching(FeatureNamespacePattern([rule.Namespace]))
            .And().AreNotNested()
            .Should().HaveNameMatching(TypeNamePattern(rule.AllowedSuffixes))
            .Because($"every type in a feature's {rule.Namespace} namespace must be named {string.Join(" or ", rule.AllowedSuffixes.Select(suffix => "*" + suffix))}.")
            .WithoutRequiringPositiveResults();

        archRule.Check(SolutionAssemblies.Architecture);
    }

    /// <summary>
    /// Matches any feature-layer assembly, so the shared projects' abstractions are not judged by
    /// conventions that only govern where a feature puts its own types.
    /// </summary>
    private const string FeatureAssemblyPattern = @".*\.Features\..*";

    /// <summary>
    /// Builds a namespace regex matching one or more home namespaces in any feature, e.g.
    /// <c>Domain.Specifications</c> becomes
    /// <c>.*\.Features\..*\.(Domain\.Specifications)(\..*)?$</c>. The trailing group admits
    /// sub-namespaces while still rejecting a sibling whose name merely starts with the same text,
    /// because after the suffix the pattern requires either end-of-string or a dot.
    /// </summary>
    private static string FeatureNamespacePattern(string[] namespaceSuffixes) =>
        @".*\.Features\..*\.(" + string.Join("|", namespaceSuffixes.Select(Regex.Escape)) + @")(\..*)?$";

    /// <summary>
    /// Builds a type-name regex matching a suffix while tolerating the CLR arity suffix that
    /// generic types carry, e.g. a generic <c>ByIdSpec&lt;T&gt;</c> reports its
    /// <see cref="ArchUnitNET.Domain.IType.Name"/> as <c>ByIdSpec`1</c>, not <c>ByIdSpec</c>.
    /// A plain <c>HaveNameEndingWith</c> check on either side of these rules would therefore
    /// wrongly fail a correctly-named generic specification, and would silently fail to select
    /// a misplaced one at all — the optional trailing <c>(`\d+)?</c> group admits that suffix
    /// without weakening the match for ordinary, non-generic types.
    /// </summary>
    private static string TypeNamePattern(string[] typeSuffixes) =>
        ".*(" + string.Join("|", typeSuffixes.Select(Regex.Escape)) + @")(`\d+)?$";
}
