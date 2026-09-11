using Bunit;

using Microsoft.AspNetCore.Components;

namespace ModulithTemplate.Features.Payments.WebTests;

/// <summary>Smoke tests for the Payments web layer.</summary>
public class PaymentsWebSmokeTests
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
            builder.AddContent(1, "Payments");
            builder.CloseElement();
        };

        // Act
        var rendered = context.Render(fragment);

        // Assert
        rendered.MarkupMatches("<p>Payments</p>");
    }
}
