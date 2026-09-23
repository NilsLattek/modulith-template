using Bunit;

using FluentResults;

using ModulithTemplate.SharedKernel.Application.Errors;
using ModulithTemplate.SharedKernel.Web;

namespace ModulithTemplate.WebTests;

/// <summary>Tests for <see cref="FormFeedback"/>, rendered inside <see cref="FeedbackHost"/>.</summary>
public class FormFeedbackTests
{
    private static IRenderedComponent<FeedbackHost> RenderHost(BunitContext context, IResultBase? result) =>
        context.Render<FeedbackHost>(parameters => parameters.Add(host => host.Result, result));

    private static string[] FieldMessages(IRenderedComponent<FeedbackHost> host) =>
        [.. host.FindAll("div.validation-message").Select(message => message.TextContent)];

    private static string[] SummaryMessages(IRenderedComponent<FeedbackHost> host) =>
        [.. host.FindAll("ul.validation-errors li").Select(message => message.TextContent)];

    [Fact]
    public void A_validation_error_for_a_field_on_the_form_shows_beside_that_field()
    {
        // Arrange
        using var context = new BunitContext();

        // Act
        var host = RenderHost(context, Result.Fail(new ValidationError("Name", "Test.NameTaken", "That name is taken.")));

        // Assert
        Assert.Equal(["That name is taken."], FieldMessages(host));
        Assert.Empty(host.FindAll(".alert"));
    }

    [Fact]
    public void A_validation_error_for_a_property_the_form_lacks_still_shows_in_the_summary()
    {
        // Arrange
        using var context = new BunitContext();

        // Act
        var host = RenderHost(context, Result.Fail(new ValidationError("Region", "Test.Region", "Pick a region.")));

        // Assert
        Assert.Empty(FieldMessages(host));
        Assert.Equal(["Pick a region."], SummaryMessages(host));
    }

    [Fact]
    public void A_conflict_shows_its_message_as_an_alert()
    {
        // Arrange
        using var context = new BunitContext();

        // Act
        var host = RenderHost(context, Result.Fail(new ConflictError("Test.Shipped", "This order has shipped.")));

        // Assert
        Assert.Equal("This order has shipped.", host.Find(".alert").TextContent.Trim());
        Assert.Empty(SummaryMessages(host));
    }

    [Fact]
    public void An_unexpected_error_shows_its_trace_id_as_a_reference()
    {
        // Arrange
        using var context = new BunitContext();

        // Act
        var host = RenderHost(context, Result.Fail(new UnexpectedError("4bf92f3577b34da6a3ce929d0e0e4736")));

        // Assert
        Assert.Contains("Reference: 4bf92f3577b34da6a3ce929d0e0e4736", host.Find(".alert").TextContent, StringComparison.Ordinal);
    }

    [Fact]
    public void An_error_from_outside_the_taxonomy_never_shows_its_text()
    {
        // Arrange
        using var context = new BunitContext();

        // Act
        var host = RenderHost(context, Result.Fail(new ExceptionalError(new InvalidOperationException("relation \"orders.x\" does not exist"))));

        // Assert
        var alert = host.Find(".alert").TextContent;
        Assert.Contains("Something went wrong.", alert, StringComparison.Ordinal);
        Assert.DoesNotContain("relation", alert, StringComparison.Ordinal);
    }

    [Fact]
    public void Editing_the_field_clears_its_server_error_and_a_re_render_does_not_bring_it_back()
    {
        // Arrange
        using var context = new BunitContext();
        var result = Result.Fail(new ValidationError("Name", "Test.NameTaken", "That name is taken."));
        var host = RenderHost(context, result);

        // Act
        host.Find("#name").Change("another");
        host.Render(parameters => parameters.Add(h => h.Result, result));

        // Assert
        Assert.Empty(FieldMessages(host));
    }

    [Fact]
    public void A_successful_result_shows_nothing()
    {
        // Arrange
        using var context = new BunitContext();
        var host = RenderHost(context, Result.Fail(new ConflictError("Test.Shipped", "This order has shipped.")));

        // Act
        host.Render(parameters => parameters.Add(h => h.Result, Result.Ok()));

        // Assert
        Assert.Empty(host.FindAll(".alert"));
        Assert.Empty(SummaryMessages(host));
    }

    [Fact]
    public void Outside_an_EditForm_it_refuses_to_render()
    {
        // Arrange
        using var context = new BunitContext();

        // Act
        var render = () => context.Render<FormFeedback>();

        // Assert
        Assert.Throws<InvalidOperationException>(render);
    }
}
