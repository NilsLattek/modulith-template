# Only the screen component sends; the CLAUDE.md defers to the dotnet-blazor skills for components

The generated `CLAUDE.md` once said "Components send it with `IScopedMediator`", and its
`IMediator` warning was phrased per component. Loaded every turn, that read as "every component
injects `IScopedMediator`", so agents skipped the on-demand `dotnet-blazor` skills and wrote
components that each fetched and saved their own data, with copies of state that drifted apart.

The feature component a host page renders is now named the **screen component**. It owns the
screen's state and sends every message; components below it take `[Parameter]`s and raise
`EventCallback`s. A screen whose state many components share gets one state-owning editor, cascaded,
which the screen still drives. `CLAUDE.md` tells agents to load `plan-ui-change`, `author-component`
and `collect-user-input` before touching a `.razor` file, and maps the skills' "page" to the screen
component, because host pages here are thin and route only.

## Considered Options

- **Each component injects `IScopedMediator`**: self-contained, but state is copied at every level.
- **Fluxor or a store**: suits state shared across pages, which a screen's state is not.

## Consequences

- `collect-user-input` covers static-SSR forms too; `adding-a-blazor-form` says to skip those, since
  interactivity is global.
