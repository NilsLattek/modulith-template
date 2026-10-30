using ArchUnitNET.Fluent.Syntax.Elements.Types;
using ArchUnitNET.xUnitV3;

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
        new("Infrastructure", "Infrastructure", ["Web"]),
        new("Web", "Web", ["Domain"]),
    ];

    [Fact]
    public void Domain_depends_on_no_other_layer()
    {
        // Arrange
        var rule = BuildLayerRule("Domain");

        // Act & Assert
        rule.Check(SolutionAssemblies.Architecture);
    }

    [Fact]
    public void Application_depends_only_on_Domain()
    {
        // Arrange
        var rule = BuildLayerRule("Application");

        // Act & Assert
        rule.Check(SolutionAssemblies.Architecture);
    }

    // Infrastructure may name Application: the publisher marker is an Application port, and an
    // adapter implementing an inward port is what the dependency rule is for. Only the reverse
    // direction, Application reaching Infrastructure, is the one that has to stay closed.
    [Fact]
    public void Infrastructure_does_not_depend_on_Web()
    {
        // Arrange
        var rule = BuildLayerRule("Infrastructure");

        // Act & Assert
        rule.Check(SolutionAssemblies.Architecture);
    }

    [Fact]
    public void Web_does_not_access_Domain_directly()
    {
        // Arrange
        var rule = BuildLayerRule("Web");

        // Act & Assert
        rule.Check(SolutionAssemblies.Architecture);
    }

    /// <summary>Builds the layer's dependency rule, failing first if its assembly was never discovered.</summary>
    private static TypesShouldConjunctionWithDescription BuildLayerRule(string layerName)
    {
        var rule = Rules.Single(r => string.Equals(r.Name, layerName, StringComparison.Ordinal));

        // Guard: fail loudly if the layer's assembly was never discovered, so a future naming
        // drift cannot turn the rule into a vacuously-passing no-op.
        Assert.True(SolutionAssemblies.HasFeatureLayer(rule.Suffix),
            $"No assembly for the '{rule.Name}' feature layer was found; the layering rule would pass vacuously.");

        return SolutionAssemblies.SelectFeatureLayerTypes(SolutionAssemblies.BuildFeatureLayerPattern(rule.Suffix))
            .Should().NotDependOnAny(SolutionAssemblies.SelectFeatureLayerTypes(
                SolutionAssemblies.BuildFeatureLayerPattern(SolutionAssemblies.BuildRegexAlternation(rule.Forbidden))))
            .Because($"a feature's {rule.Name} layer must not depend on its {string.Join("/", rule.Forbidden)} layer(s).");
    }
}
