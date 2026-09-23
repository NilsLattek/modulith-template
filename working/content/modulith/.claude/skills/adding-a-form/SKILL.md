---
name: adding-a-form
description: Build a Blazor form that sends a command — its form model, instant client-side validation, and the server's answer shown in place. Use when adding a create or edit form, an input dialog, or any component that submits user input, or when deciding where a form's validation rules belong.
---

# Adding a form

A form is three parts: a **form model** the inputs bind to, **DataAnnotations** on it for feedback as
the user types, and **`<FormFeedback>`** for the server's answer. `AddSomeEntityForm.razor` in the
Orders feature's `Web/Components/` is the worked example, for as long as the placeholder entity
exists.

The component lives in the feature's `Web/Components/`; a routable page in the host renders it.

## Skeleton

```razor
@using System.ComponentModel.DataAnnotations
@using FluentResults
@inject IScopedMediator Mediator

<EditForm Model="_model" OnValidSubmit="SubmitAsync">
    <DataAnnotationsValidator />
    <FormFeedback Result="_result" />

    <InputText id="name" @bind-Value="_model.Name" />
    <ValidationMessage For="() => _model.Name" />

    <button type="submit" disabled="@_submitting">Save</button>
</EditForm>

@code {
    private FormModel _model = new();
    private IResultBase? _result;
    private bool _submitting;

    private async Task SubmitAsync()
    {
        _submitting = true;
        try
        {
            _result = await Mediator.Send(_model.ToCommand());
            if (_result.IsSuccess)
            {
                _model = new FormModel();
            }
        }
        finally
        {
            _submitting = false;
        }
    }

    public sealed class FormModel
    {
        [Required, StringLength(200)]
        public string? Name { get; set; }

        public RenameOrderCommand ToCommand() => new(Name!);
    }
}
```

## The form model

- **Its own class, nested in the component — never the command.** Commands are immutable records and
  `@bind-Value` needs a setter.
- **Nullable properties**, so a field starts empty rather than at `0`.
- **Property names match the command's.** That is how a server `ValidationError` finds its field.
- **`ToCommand()` is called only from `OnValidSubmit`**, when the annotations have passed — hence the
  `!` and `.Value`.
- **Reset by assigning a new instance**, never by clearing properties: `EditForm` builds a fresh
  `EditContext` only when `Model` changes, and the old one keeps every field marked as modified.

## Validation: two layers, duplicated on purpose

The annotations restate the command validator's **shape** rules — required, length, range — so the
user sees them before submitting. Write the limits as literals: the domain's constants are
deliberately unreachable from `Web`.

They are a convenience, not the authority. The command validator and the entity check everything
again, so a client rule that drifts too loose costs one round trip, and one too strict is a UX bug —
neither lets bad data in. A rule the annotations cannot express (a decimal scale, "that name is
taken") is simply left to the server; its answer lands beside the same field.

**No business rule goes in a form model.** A component enforcing a rule nothing else enforces is a
rule in the wrong place.

## The server's answer

`<FormFeedback Result="_result" />` goes inside the `EditForm` wherever feedback should appear:

| The result carries… | Shown as |
| --- | --- |
| `ValidationError` | beside the field with that property name, and in the summary |
| `ValidationError` for a property the form has no field for | in the summary |
| `NotFoundError`, `ConflictError` | an alert with its message, verbatim |
| `UnexpectedError`, or anything else | a generic alert with a support reference — never its text |

A field's server error clears when the user edits that field, and every one clears on the next
submit. **Never render `result.Errors` yourself** — that is the path by which exception text reaches
a page.

## Tripwires

- **Interactive rendering only.** The app renders every page interactively, which is what gives
  instant feedback. A page switched to static SSR validates only after posting and needs a different
  form pattern (`FormName`, `[SupplyParameterFromForm]`) this skill does not cover.
- **No `AddValidation()` / `[ValidatableType]`.** Their source generator cannot see a model declared
  in a `.razor` file, so form models use the reflection-based validator — the one accepted exception
  to the no-reflection rule.
- **Top-level fields only.** A `ValidationError` for a nested path (`Lines[0].Quantity`) appears in
  the summary, not beside its input.
- **Another component library** works if its inputs sit inside an `EditForm`: `FormFeedback` speaks
  to the `EditContext`, not to the inputs. A library's own form component (`MudForm`) is outside it.

## Tests

bUnit, with `IScopedMediator` substituted. Cover three paths: a valid submit sends the command with
the typed values and resets; an empty submit sends nothing; a server `ValidationError` lands beside
its field with the values kept. `AddSomeEntityFormTests` in the host's `WebTests` does all three.
