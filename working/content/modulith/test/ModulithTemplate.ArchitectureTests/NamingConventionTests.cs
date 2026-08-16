using System.Text.RegularExpressions;

using ArchUnitNET.Fluent;
using ArchUnitNET.xUnitV3;

using static ArchUnitNET.Fluent.ArchRuleDefinition;

namespace ModulithTemplate.ArchitectureTests;

/// <summary>
/// Enforces the naming and placement conventions for the feature building blocks described in
/// CLAUDE.md, in both directions: where a suffix may live, and what a namespace may hold.
/// </summary>
/// <remarks>
/// The two directions need two tables because neither is the other's mirror: a <c>*Dto</c> has two
/// homes (internal read model and published contract), and <c>Contracts.Api</c> holds two suffixes
/// (the interface and the DTOs it returns).
/// <para>
/// Matching nothing must pass, not fail — a naming rule with no matches means the solution has no
/// specifications <i>yet</i>, which is the normal state of a fresh scaffold. ArchUnitNET throws in
/// that case, so every helper opts out via <c>WithoutRequiringPositiveResults()</c>. Rules are
/// scoped to feature assemblies, because the shared projects deliberately define abstractions with
/// the same suffixes (<c>IDomainEvent</c>, <c>IDomainEventHandler</c>).
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
    /// Builds a namespace regex matching one or more home namespaces in any feature. The trailing
    /// group admits sub-namespaces while still rejecting a sibling that merely starts with the same
    /// text, since after the suffix the pattern requires end-of-string or a dot.
    /// </summary>
    private static string FeatureNamespacePattern(string[] namespaceSuffixes) =>
        @".*\.Features\..*\.(" + string.Join("|", namespaceSuffixes.Select(Regex.Escape)) + @")(\..*)?$";

    /// <summary>
    /// Builds a type-name regex matching a suffix, tolerating the CLR arity suffix generic types
    /// carry: <c>ByIdSpec&lt;T&gt;</c> reports its name as <c>ByIdSpec`1</c>, which a plain
    /// <c>HaveNameEndingWith</c> would both wrongly fail and fail to select.
    /// </summary>
    private static string TypeNamePattern(string[] typeSuffixes) =>
        ".*(" + string.Join("|", typeSuffixes.Select(Regex.Escape)) + @")(`\d+)?$";
}
