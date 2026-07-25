using Bunit;

using Microsoft.AspNetCore.Components;

namespace ModulithApp.Features.FeatureName.WebTests;

/// <summary>Smoke tests for the FeatureName web layer.</summary>
public class FeatureNameWebSmokeTests
{
    /// <summary>
    /// Verifies the bUnit rendering pipeline is wired up, so feature components added later can
    /// be rendered and asserted against with <c>MarkupMatches</c>.
    /// </summary>
    [Fact]
    public void BunitContext_renders_a_render_fragment_to_markup()
    {
        // Arrange
        using var context = new BunitContext();
        RenderFragment fragment = builder =>
        {
            builder.OpenElement(0, "p");
            builder.AddContent(1, "FeatureName");
            builder.CloseElement();
        };

        // Act
        var rendered = context.Render(fragment);

        // Assert
        rendered.MarkupMatches("<p>FeatureName</p>");
    }
}
