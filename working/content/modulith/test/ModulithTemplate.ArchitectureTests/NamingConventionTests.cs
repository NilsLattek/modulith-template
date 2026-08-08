using System.Text.RegularExpressions;

using ArchUnitNET.Fluent;
using ArchUnitNET.xUnitV3;

using static ArchUnitNET.Fluent.ArchRuleDefinition;

namespace ModulithTemplate.ArchitectureTests;

/// <summary>
/// Enforces the naming and placement conventions for the feature building blocks described in
/// CLAUDE.md: specifications, mappers, domain services, commands, queries and DTOs. Each
/// convention is checked in both directions — every type in the home namespace carries the
/// suffix, and every type carrying the suffix resides in the home namespace.
/// </summary>
/// <remarks>
/// Unlike <see cref="FeatureLayerTests"/> and <see cref="FeatureModuleTests"/>, these rules
/// deliberately carry no "assembly was discovered" presence guard, and none should be added.
/// A layering rule that matches nothing is a false green over code that exists; a naming rule
/// that matches nothing simply means the solution has no specifications yet, which is the
/// normal state of a freshly scaffolded project. These are conditional tripwires armed for
/// code the user has not written yet. ArchUnitNET's default behavior is the opposite of what
/// that requires — a rule whose predicate ("That()") matches zero types throws rather than
/// passing, so each helper below opts out via <c>WithoutRequiringPositiveResults()</c>; this
/// is unrelated to the presence-guard question above, since it still fails on any real
/// violation among the types that do match. The <c>AreNotNested()</c> filter is not
/// load-bearing — ArchUnitNET already excludes compiler-generated nested types on its own —
/// it is retained purely as insurance against a hand-written nested type slipping through.
/// </remarks>
public class NamingConventionTests
{
    /// <summary>A building block, the namespace it belongs in, and the type-name suffixes it may carry.</summary>
    private sealed record NamingConvention(string Name, string NamespaceSuffix, string[] TypeSuffixes);

    private static readonly NamingConvention[] Conventions =
    [
        new("specification", "Domain.Specifications", ["Spec"]),
        new("mapper", "Application.Mappers", ["Mapper"]),
        new("domain service", "Domain.Services", ["DomainService"]),
        new("command", "Application.Commands", ["Command", "CommandHandler"]),
        new("query", "Application.Queries", ["Query", "QueryHandler"]),
        new("dto", "Application.Dtos", ["Dto"]),
    ];

    /// <summary>Every type in a feature's Specifications namespace must be named <c>*Spec</c>.</summary>
    [Fact]
    public void Types_in_Specifications_end_with_Spec() => AssertNaming("specification");

    /// <summary>Every type named <c>*Spec</c> must reside in a feature's Specifications namespace.</summary>
    [Fact]
    public void Types_named_Spec_reside_in_Specifications() => AssertPlacement("specification");

    /// <summary>Every type in a feature's Mappers namespace must be named <c>*Mapper</c>.</summary>
    [Fact]
    public void Types_in_Mappers_end_with_Mapper() => AssertNaming("mapper");

    /// <summary>Every type named <c>*Mapper</c> must reside in a feature's Mappers namespace.</summary>
    [Fact]
    public void Types_named_Mapper_reside_in_Mappers() => AssertPlacement("mapper");

    /// <summary>Every type in a feature's Domain Services namespace must be named <c>*DomainService</c>.</summary>
    [Fact]
    public void Types_in_Domain_Services_end_with_DomainService() => AssertNaming("domain service");

    /// <summary>Every type named <c>*DomainService</c> must reside in a feature's Domain Services namespace.</summary>
    [Fact]
    public void Types_named_DomainService_reside_in_Domain_Services() => AssertPlacement("domain service");

    /// <summary>Every type in a feature's Commands namespace must be named <c>*Command</c> or <c>*CommandHandler</c>.</summary>
    [Fact]
    public void Types_in_Commands_end_with_Command_or_CommandHandler() => AssertNaming("command");

    /// <summary>Every type named <c>*Command</c> or <c>*CommandHandler</c> must reside in a feature's Commands namespace.</summary>
    [Fact]
    public void Types_named_Command_or_CommandHandler_reside_in_Commands() => AssertPlacement("command");

    /// <summary>Every type in a feature's Queries namespace must be named <c>*Query</c> or <c>*QueryHandler</c>.</summary>
    [Fact]
    public void Types_in_Queries_end_with_Query_or_QueryHandler() => AssertNaming("query");

    /// <summary>Every type named <c>*Query</c> or <c>*QueryHandler</c> must reside in a feature's Queries namespace.</summary>
    [Fact]
    public void Types_named_Query_or_QueryHandler_reside_in_Queries() => AssertPlacement("query");

    /// <summary>Every type in a feature's Dtos namespace must be named <c>*Dto</c>.</summary>
    [Fact]
    public void Types_in_Dtos_end_with_Dto() => AssertNaming("dto");

    /// <summary>Every type named <c>*Dto</c> must reside in a feature's Dtos namespace.</summary>
    [Fact]
    public void Types_named_Dto_reside_in_Dtos() => AssertPlacement("dto");

    private static void AssertNaming(string conventionName)
    {
        var convention = Find(conventionName);

        IArchRule rule = Types().That()
            .ResideInNamespaceMatching(FeatureNamespacePattern(convention.NamespaceSuffix))
            .And().AreNotNested()
            .Should().HaveNameMatching(TypeNamePattern(convention.TypeSuffixes))
            .Because($"every type in a feature's {convention.NamespaceSuffix} namespace is a {convention.Name} and must be named {string.Join(" or ", convention.TypeSuffixes.Select(suffix => "*" + suffix))}.")
            .WithoutRequiringPositiveResults();

        rule.Check(SolutionAssemblies.Architecture);
    }

    private static void AssertPlacement(string conventionName)
    {
        var convention = Find(conventionName);

        IArchRule rule = Types().That()
            .HaveNameMatching(TypeNamePattern(convention.TypeSuffixes))
            .And().AreNotNested()
            .Should().ResideInNamespaceMatching(FeatureNamespacePattern(convention.NamespaceSuffix))
            .Because($"a {convention.Name} must live in its feature's {convention.NamespaceSuffix} namespace.")
            .WithoutRequiringPositiveResults();

        rule.Check(SolutionAssemblies.Architecture);
    }

    private static NamingConvention Find(string conventionName) =>
        Conventions.Single(c => string.Equals(c.Name, conventionName, StringComparison.Ordinal));

    /// <summary>
    /// Builds a namespace regex matching one building block's home namespace in any feature,
    /// e.g. <c>Domain.Specifications</c> becomes
    /// <c>.*\.Features\..*\.Domain\.Specifications(\..*)?$</c>. The trailing group admits
    /// sub-namespaces while still rejecting a sibling whose name merely starts with the same
    /// text, because after the suffix the pattern requires either end-of-string or a dot.
    /// </summary>
    private static string FeatureNamespacePattern(string namespaceSuffix) =>
        @".*\.Features\..*\." + Regex.Escape(namespaceSuffix) + @"(\..*)?$";

    /// <summary>
    /// Builds a type-name regex matching a suffix while tolerating the CLR arity suffix that
    /// generic types carry, e.g. a generic <c>ByIdSpec&lt;T&gt;</c> reports its
    /// <see cref="ArchUnitNET.Domain.IType.Name"/> as <c>ByIdSpec`1</c>, not <c>ByIdSpec</c>.
    /// A plain <c>HaveNameEndingWith</c> check on either side of these rules would therefore
    /// wrongly fail a correctly-named generic specification, and would silently fail to select
    /// a misplaced one at all — the optional trailing <c>(`\d+)?</c> group admits that suffix
    /// without weakening the match for ordinary, non-generic types. A convention may carry more
    /// than one suffix — a <c>Commands</c> namespace legitimately holds both <c>*Command</c> and
    /// <c>*CommandHandler</c> types — so the suffixes are combined into an alternation.
    /// </summary>
    private static string TypeNamePattern(string[] typeSuffixes) =>
        ".*(" + string.Join("|", typeSuffixes.Select(Regex.Escape)) + @")(`\d+)?$";
}
