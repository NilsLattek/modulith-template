---
name: adding-a-blazor-form
description: Add a Blazor form that sends a command — the `*FormModel` and its validation, the `*Form` component, the component that sends the command, and their bUnit tests. Use when adding a form, input screen or edit dialog, sharing one form between create and edit, or choosing between a form model and a command validator.
---

# Adding a Blazor form

A form is three pieces, each with one job:

```
Web/Components/
  OrderDetailsFormModel.cs   what the user types: validation, To<Command>(), From(dto)
  OrderDetailsForm.razor     renders the model, raises OnValidSubmit — sends nothing
  PlaceOrder.razor           consumer: sends PlaceOrderCommand
  ChangeOrderDetails.razor   consumer: sends ChangeOrderDetailsCommand
```

The form and its model are named after **what is typed**; each consumer after **its command**.

Once `Components/` fills up, group by concept in subfolders (`Components/OrderDetails/`). A
subfolder is a sub-namespace: a component outside it needs `@using …Web.Components.OrderDetails`,
or add the line to `Components/_Imports.razor`.

Every component renders in interactive server mode, so the form model is validated on the server — a
client cannot skip it. Load the `dotnet-blazor` skill `collect-user-input` for input and `EditForm`
mechanics, but skip its static-SSR patterns (`[SupplyParameterFromForm]`, `FormName`, `Enhance`):
interactivity is global here.

## The form model

A `public sealed class` marked `[ValidatableType]`, with settable properties and DataAnnotations. The
feature's `<Name>Module.cs` already calls `AddValidation()`; it must stay there, because the
validation source generator only emits metadata for the assembly that calls it.

- **Copy the entity's limits by value** — lengths, ranges, scale. `Web` cannot see `Domain`. If the
  copy drifts, the entity still rejects the input; the user just gets the consumer's generic message.
- **A rule that no attribute expresses** — a cross-field check, decimal places — goes in
  `IValidatableObject.Validate`, yielding a `ValidationResult` with the member name.
- **A value-type field the user must fill is nullable** (`decimal?`, `DateOnly?`) plus `[Required]`,
  so an empty input shows "Enter an amount" rather than silently becoming `0`.
- **One `To<CommandName>()` per consuming command** handles that nullability, once validation has
  passed: `ToPlaceOrderCommand() => new(Name, Amount.GetValueOrDefault())`. Values the user does not
  type arrive as parameters: `ToChangeOrderDetailsCommand(Guid orderId)`.
- **`static From(OrderDetailsDto dto)`** fills the model for an edit form.

The form model checks shape only. Rules that depend on domain state — uniqueness, "may this order
still change" — live in the entity or a domain service, where they hold for every caller.

**The command gets no FluentValidation validator** while forms are its only callers: the form model
and the entity already cover it. Add a `*CommandValidator` when a caller without a form appears — an
endpoint, a consumer, a job (skill: `adding-a-command-or-query`).

## The form component

Input and validation only. It injects no mediator and names no command:

```razor
<EditForm Model="Model" OnValidSubmit="() => OnValidSubmit.InvokeAsync(Model)">
    <DataAnnotationsValidator />
    <InputText id="name" class="form-control" @bind-Value="Model.Name" />
    <ValidationMessage For="() => Model.Name" />
    ...
    <button type="submit" class="btn btn-primary" disabled="@Busy">@SubmitLabel</button>
</EditForm>

@code {
    [Parameter, EditorRequired] public OrderDetailsFormModel Model { get; set; } = null!;
    [Parameter] public EventCallback<OrderDetailsFormModel> OnValidSubmit { get; set; }
    [Parameter] public bool Busy { get; set; }
    [Parameter] public string SubmitLabel { get; set; } = "Save";
}
```

Use this contract even for a form with one consumer, so a second consumer needs no rewrite.

**Share a form only between commands whose users type the same fields.** `PlaceOrder(Name, Amount)`
and `ChangeOrderDetails(OrderId, Name, Amount)` share one; the id comes from the consumer.
`ChangeShippingAddress` types different fields and gets its own form. If a shared form needs an
`IsEdit` flag, or a field only one consumer shows, it is two forms.

## The consumer

The top-level component a page renders — the one that owns the page's state (see the `dotnet-blazor`
skills). It owns the model instance, sends the command with `IScopedMediator`, and decides what
success and failure look like; a component between it and the form only passes `OnValidSubmit` up.
Here the page renders `<ChangeOrderDetails OrderId="@OrderId" />` with the id from its route:

```razor
@inject IScopedMediator Mediator
@inject NavigationManager Navigation

@if (_model is not null)
{
    <OrderDetailsForm Model="_model" OnValidSubmit="SaveAsync" Busy="_saving" SubmitLabel="Save" />
    @if (_error is not null) { <p class="text-danger">@_error</p> }
}

@code {
    [Parameter] public Guid OrderId { get; set; }

    private OrderDetailsFormModel? _model;
    private bool _saving;
    private string? _error;

    protected override async Task OnInitializedAsync()
    {
        var result = await Mediator.Send(new GetOrderDetailsQuery(OrderId));
        if (result.IsSuccess) { _model = OrderDetailsFormModel.From(result.Value); }
        else { _error = "The order could not be loaded."; }
    }

    private async Task SaveAsync(OrderDetailsFormModel model)
    {
        _saving = true;
        _error = null;
        try
        {
            var result = await Mediator.Send(model.ToChangeOrderDetailsCommand(OrderId));
            if (result.IsSuccess) { Navigation.NavigateTo($"/orders/{OrderId}"); }
            else
            {
                _error = result.Errors.OfType<BusinessError>().FirstOrDefault()?.Message
                    ?? "The order could not be saved.";
            }
        }
        finally { _saving = false; }
    }
}
```

- **Success:** navigate away, or replace the model with a fresh one to clear the form.
- **Failure:** one form-level message. The model stays as it was, so the user's input survives.
  Show a `BusinessError`'s message, which is written for the user; any other error gets fixed text,
  because an `ExceptionalError`'s message can leak SQL or internals.

**In a list, a row is never the consumer.** A row that shows a form for inline editing raises an
`EventCallback` with the item's id and the model; the component that owns the list sends the command
and replaces the item from the result. A row that sends its own command keeps a copy of the item that
drifts from the list's.

## Tests

Two bUnit test classes in `<Feature>.WebTests`, with NSubstitute for `IScopedMediator`:

- **Form:** invalid input shows the field's message and does not invoke `OnValidSubmit`; valid input
  invokes it with the model.
- **Consumer:** it sends the command built from the model; on success it resets or navigates; on a
  failed `Result` it shows the message and keeps the input.

Test the copied limits on the entity, in `<Feature>.DomainTests`, not rule by rule through the form.

Done when `dotnet build -warnaserror` is clean and both test classes pass.
