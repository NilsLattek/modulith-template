# The generated CLAUDE.md defers Blazor component design to the dotnet-blazor skills

The generated `CLAUDE.md` once said "Components send it with `IScopedMediator`" and phrased its
`IMediator` warning per component. Loaded every turn, that read as "every component injects the
mediator", so agents skipped the on-demand `dotnet-blazor` skills and wrote components that each
fetched and saved their own data instead of passing data down and raising events up.

`CLAUDE.md` now says only *how* Blazor sends a message (`IScopedMediator`), never *which* component
sends it, and tells agents to load `plan-ui-change`, `author-component` and `collect-user-input`
before touching a `.razor` file. Component structure and state ownership are left to those skills.

## Consequences

- `collect-user-input` also covers static-SSR forms; `adding-a-blazor-form` says to skip those, since
  interactivity is global.
