using ArchUnitNET.Fluent;
using ArchUnitNET.xUnitV3;

using static ArchUnitNET.Fluent.ArchRuleDefinition;

namespace ModulithTemplate.ArchitectureTests;

/// <summary>
/// Enforces the directional dependency rules between the layer projects inside a feature.
/// Layers are identified by assembly name suffix (e.g. <c>ModulithTemplate.Features.Orders.Domain</c>),
/// so the rules apply generically to every current and future feature.
/// </summary>
public class FeatureLayerTests
{
    /// <summary>A feature layer and the sibling layers it must not depend on.</summary>
    private sealed record LayerRule(string Name, string Suffix, string[] Forbidden);

    private static readonly LayerRule[] Rules =
    [
        new("Domain", "Domain", ["Application", "Infrastructure", "Web"]),
        new("Application", "Application", ["Infrastructure", "Web"]),
        new("Infrastructure", "Infrastructure", ["Application", "Web"]),
        new("Web", "Web", ["Domain"]),
    ];

    [Fact]
    public void Domain_depends_on_no_other_layer() => AssertLayerRule("Domain");

    [Fact]
    public void Application_depends_only_on_Domain() => AssertLayerRule("Application");

    [Fact]
    public void Infrastructure_depends_only_on_Domain() => AssertLayerRule("Infrastructure");

    [Fact]
    public void Web_does_not_access_Domain_directly() => AssertLayerRule("Web");

    private static void AssertLayerRule(string layerName)
    {
        var rule = Rules.Single(r => string.Equals(r.Name, layerName, StringComparison.Ordinal));

        // Guard: fail loudly if the layer's assembly was never discovered, so a future naming
        // drift cannot turn the rule into a vacuously-passing no-op.
        var layerPresent = SolutionAssemblies.FeatureAssemblies.Any(a =>
        {
            var name = a.GetName().Name!;
            return name.Contains(".Features.", StringComparison.Ordinal)
                && name.EndsWith("." + rule.Suffix, StringComparison.Ordinal);
        });
        Assert.True(layerPresent,
            $"No assembly for the '{rule.Name}' feature layer was found; the layering rule would pass vacuously.");

        IArchRule layerRule = Types().That().ResideInAssemblyMatching(FeaturePattern(rule.Suffix))
            .Should().NotDependOnAnyTypesThat().ResideInAssemblyMatching(FeaturePattern(Alternation(rule.Forbidden)))
            .Because($"a feature's {rule.Name} layer must not depend on its {string.Join("/", rule.Forbidden)} layer(s).");

        layerRule.Check(SolutionAssemblies.Architecture);
    }

    /// <summary>
    /// Builds an assembly-name regex matching a feature layer by its suffix expression.
    /// ArchUnitNET matches against the assembly's fully-qualified name
    /// (e.g. <c>ModulithTemplate.Features.Orders.Domain, Version=1.0.0.0, Culture=neutral, PublicKeyToken=null</c>),
    /// so the suffix is anchored at the simple-name boundary (end of string or the version comma),
    /// not at the end of the whole string.
    /// </summary>
    private static string FeaturePattern(string suffixExpression) =>
        @".*\.Features\..*\." + suffixExpression + @"(,.*)?$";

    /// <summary>Combines one or more layer suffixes into a regex alternation.</summary>
    private static string Alternation(string[] suffixes) =>
        suffixes.Length == 1 ? suffixes[0] : "(" + string.Join("|", suffixes) + ")";
}
