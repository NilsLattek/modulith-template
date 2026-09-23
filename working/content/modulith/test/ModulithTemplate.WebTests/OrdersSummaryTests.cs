using Bunit;

using FluentResults;

using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.DependencyInjection;

using ModulithTemplate.Features.Orders.Application.Commands.AddSomeEntity;
using ModulithTemplate.Features.Orders.Application.Queries.GetSomeEntityCount;
using ModulithTemplate.Features.Orders.Web.Components;
using ModulithTemplate.SharedKernel.Application.Errors;
using ModulithTemplate.SharedKernel.Web;

using NSubstitute;

namespace ModulithTemplate.WebTests;

/// <summary>Tests the Orders add form: client-side rules, server errors and the count refresh.</summary>
public sealed class OrdersSummaryTests : IDisposable
{
    private readonly BunitContext _context = new();
    private readonly IScopedMediator _mediator = Substitute.For<IScopedMediator>();

    public OrdersSummaryTests() => _context.Services.AddSingleton(_mediator);

    public void Dispose() => _context.Dispose();

    [Fact]
    public async Task Adding_an_entity_reloads_the_count()
    {
        _mediator.Send(Arg.Any<GetSomeEntityCountQuery>(), Arg.Any<CancellationToken>())
            .Returns(Result.Ok(0), Result.Ok(1));
        _mediator.Send(Arg.Any<AddSomeEntityCommand>(), Arg.Any<CancellationToken>())
            .Returns(Result.Ok());
        var component = _context.Render<OrdersSummary>();

        await component.Find("#name").ChangeAsync(new ChangeEventArgs { Value = "Widget" });
        await component.Find("#amount").ChangeAsync(new ChangeEventArgs { Value = "12.50" });
        await component.Find("form").SubmitAsync();

        await _mediator.Received(1).Send(new AddSomeEntityCommand("Widget", 12.50m), Arg.Any<CancellationToken>());
        Assert.Equal("1", component.Find("strong").TextContent);
        Assert.Equal(string.Empty, component.Find("#name").GetAttribute("value") ?? string.Empty);
    }

    [Theory]
    [InlineData("", "12.50", "Enter a name.")]
    [InlineData("Widget", "0", "The amount must be between")]
    [InlineData("Widget", "1.005", "at most 2 decimal places")]
    public async Task Invalid_input_is_rejected_without_sending_the_command(string name, string amount, string message)
    {
        _mediator.Send(Arg.Any<GetSomeEntityCountQuery>(), Arg.Any<CancellationToken>()).Returns(Result.Ok(0));
        var component = _context.Render<OrdersSummary>();

        await component.Find("#name").ChangeAsync(new ChangeEventArgs { Value = name });
        await component.Find("#amount").ChangeAsync(new ChangeEventArgs { Value = amount });
        await component.Find("form").SubmitAsync();

        await _mediator.DidNotReceive().Send(Arg.Any<AddSomeEntityCommand>(), Arg.Any<CancellationToken>());
        Assert.Contains(message, component.Find(".validation-message").TextContent, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Server_validation_errors_are_shown_on_their_field()
    {
        _mediator.Send(Arg.Any<GetSomeEntityCountQuery>(), Arg.Any<CancellationToken>()).Returns(Result.Ok(0));
        _mediator.Send(Arg.Any<AddSomeEntityCommand>(), Arg.Any<CancellationToken>())
            .Returns(Result.Fail(new ValidationError(nameof(AddSomeEntityCommand.Name), "Name is taken.")));
        var component = _context.Render<OrdersSummary>();

        await component.Find("#name").ChangeAsync(new ChangeEventArgs { Value = "Widget" });
        await component.Find("#amount").ChangeAsync(new ChangeEventArgs { Value = "1" });
        await component.Find("form").SubmitAsync();

        Assert.Equal("Name is taken.", component.Find(".validation-message").TextContent);
        Assert.Equal("0", component.Find("strong").TextContent);
    }
}
