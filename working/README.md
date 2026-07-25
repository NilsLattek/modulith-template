# README

## Templates in this package

- `dotnet new modulith` — scaffolds a new modular-monolith solution.
- `dotnet new modulith-feature --appName <App> -n <Name>` — run from a scaffolded solution's
  root to add one feature's four layer projects (`Domain`/`Application`/`Infrastructure`/`Web`)
  plus their four matching test projects, eight projects in all, registered in the solution
  automatically.

## DB Migrations

For database migrations install ef-core tooling (https://learn.microsoft.com/en-us/ef/core/get-started/overview/first-app?tabs=netcore-cli):

```bash
dotnet tool install --global dotnet-ef
dotnet add package Microsoft.EntityFrameworkCore.Design
dotnet ef migrations add InitialCreate -o Data/Migrations/ --project src/YourAppName.Infrastructure/ -s src/YourAppName.WebApi/
dotnet ef database update --project src/YourAppName.Infrastructure/ -s src/YourAppName.WebApi/
```
