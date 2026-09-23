using Bunit;

using FluentResults;

using Microsoft.Extensions.DependencyInjection;

using ModulithTemplate.Features.Orders.Application.Commands.AddSomeEntity;
using ModulithTemplate.Features.Orders.Web.Components;
using ModulithTemplate.SharedKernel.Application.Errors;
using ModulithTemplate.SharedKernel.Web;

using NSubstitute;

namespace ModulithTemplate.WebTests;

/// <summary>Tests for the reference form, <see cref="AddSomeEntityForm"/>.</summary>
public sealed class AddSomeEntityFormTests : IDisposable
{
    private readonly BunitContext _context = new();
    private readonly IScopedMediator _mediator = Substitute.For<IScopedMediator>();

    public AddSomeEntityFormTests() => _context.Services.AddSingleton(_mediator);

    public void Dispose() => _context.Dispose();

    private void ServerAnswers(Result result)
    {
        // CA2012 reads the arrangement as a stray ValueTask; it is NSubstitute's syntax for a stub.
#pragma warning disable CA2012
        _mediator.Send(Arg.Any<AddSomeEntityCommand>(), Arg.Any<CancellationToken>())
            .Returns(_ => ValueTask.FromResult(result));
#pragma warning restore CA2012
    }

    private static void Fill(IRenderedComponent<AddSomeEntityForm> form, string name, string amount)
    {
        form.Find("#name").Change(name);
        form.Find("#amount").Change(amount);
    }

    [Fact]
    public async Task A_valid_submit_sends_the_command_then_resets_the_form()
    {
        // Arrange
        ServerAnswers(Result.Ok());
        var added = false;
        var form = _context.Render<AddSomeEntityForm>(parameters => parameters.Add(f => f.OnAdded, () => added = true));
        Fill(form, "Widget", "12.50");

        // Act
        await form.Find("form").SubmitAsync();

        // Assert
        await _mediator.Received(1).Send(
            Arg.Is<AddSomeEntityCommand>(command => command.Name == "Widget" && command.Amount == 12.50m),
            Arg.Any<CancellationToken>());
        Assert.True(added);
        Assert.Equal("", form.Find("#name").GetAttribute("value") ?? "");
        Assert.Contains("Added.", form.Markup, StringComparison.Ordinal);
    }

    [Fact]
    public async Task An_empty_submit_is_stopped_on_the_client_and_sends_nothing()
    {
        // Arrange
        var form = _context.Render<AddSomeEntityForm>();

        // Act
        await form.Find("form").SubmitAsync();

        // Assert
        Assert.Equal(2, form.FindAll("div.validation-message").Count);
        Assert.Empty(_mediator.ReceivedCalls());
    }

    [Fact]
    public async Task A_server_validation_error_lands_beside_its_field_and_keeps_the_values()
    {
        // Arrange: three decimals pass the form's range check but not the command's scale rule
        ServerAnswers(Result.Fail(new ValidationError("Amount", "ScalePrecisionValidator", "At most 2 decimal places.")));
        var form = _context.Render<AddSomeEntityForm>();
        Fill(form, "Widget", "1.234");

        // Act
        await form.Find("form").SubmitAsync();

        // Assert
        var message = Assert.Single(form.FindAll("div.validation-message"));
        Assert.Equal("At most 2 decimal places.", message.TextContent);
        Assert.Equal("Widget", form.Find("#name").GetAttribute("value"));
        Assert.DoesNotContain("Added.", form.Markup, StringComparison.Ordinal);
    }
}
