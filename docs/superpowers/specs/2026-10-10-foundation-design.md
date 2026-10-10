# Foundation — design

Slice 1 of the [roadmap](../roadmap.md). Status: approved design, pending implementation plan.

## Context

The scaffold provided by N2JSoft is a single `WebApi` project (Minimal API, EF Core + PostgreSQL, an
unused Redis client), an Aspire AppHost wiring Postgres, Redis, the WebApi and an Angular 22 / Nx
frontend that still shows the Nx welcome page. There is no test project. CI builds the backend on
ubuntu, windows and macOS and runs lint/test/build on the frontend.

The functional work (users, expense reports, expenses) is split into vertical slices 2–4. Before
those, this slice puts in place the architecture, the API conventions and the three backend test
tiers, proven end to end on the one endpoint that already exists (`GET /users`), so every later
slice copies patterns that are known to work.

## Goals

- Restructure the backend into Clean Architecture projects with an enforced dependency rule.
- Establish the lightweight-CQRS shape (command handlers, query interfaces) and the error contract.
- Add API versioning, and OpenAPI + Scalar in Development.
- Remove the unused Redis.
- Add the Unit, Acceptance (Reqnroll + Testcontainers) and End-to-end (Aspire + Playwright) tiers,
  each proven by a real test.
- Replace the Nx welcome page with a minimal French shell listing users.
- Split CI so Docker-dependent tiers run on ubuntu only.

## Non-goals

- Any change to the `User` model beyond moving it (full model is slice 2).
- Write-side abstractions (`IUnitOfWork`, repositories): the first command arrives in slice 2, which
  creates them following the conventions below.
- Forms, mapping error `code`s to French messages, and pagination controls in the frontend.
- A generated TypeScript API client (noted as a possible improvement).
- Migrating the frontend to zoneless change detection.

## Decisions

| Topic                | Decision                                                                                | Why                                                                                                                       |
|----------------------|-----------------------------------------------------------------------------------------|---------------------------------------------------------------------------------------------------------------------------|
| Backend structure    | `Domain` / `Application` / `Infrastructure` / `WebApi` projects                         | Clear layering; business rules isolated and unit-testable                                                                |
| Application style    | Lightweight CQRS without a mediator                                                     | Writes carry real rules, reads are DTO-shaped; one handler per use case keeps plan tasks small; MediatR is commercial |
| Data access          | Write-side repositories + `IUnitOfWork`; read-side query interfaces with EF projections | Command handlers unit-testable with NSubstitute; Application has no EF dependency; repositories don't bloat with reads |
| Validation           | .NET 10 built-in minimal-API validation (DataAnnotations)                               | No extra dependency, standard `ValidationProblemDetails`                                                                 |
| Business errors      | Typed exceptions → one `IExceptionHandler` → RFC 9457 ProblemDetails                    | One mapping point; simple handlers                                                                                         |
| Versioning           | `Asp.Versioning.Http`, URL segment `/v1/...`                                            | Visible in URLs and Scalar; works with the existing `/api` proxy rewrite                                                 |
| API docs             | Built-in OpenAPI + `Asp.Versioning.OpenApi` + `Scalar.AspNetCore`, Development only    | No Swashbuckle; one document per version                                                                                 |
| Redis                | Removed from WebApi and AppHost                                                         | Unused (YAGNI); would force a Redis container into every test tier                                                       |
| Test stack           | xUnit v3 on Microsoft.Testing.Platform, Shouldly, NSubstitute                           | First-class Reqnroll and Aspire support; Shouldly avoids FluentAssertions' commercial license                             |
| Gherkin language     | English                                                                                 | Same language as the code                                                                                                 |
| E2E scope            | Browser journeys (Playwright) on the AppHost booted by Aspire.Hosting.Testing         | Tests what the other tiers can't; no overlap with Acceptance                                                              |

## Architecture

### Projects

```text
backend/
  src/
    Domain/          Domain.csproj          entities, value objects, domain exceptions
    Application/     Application.csproj     per-feature Commands/ and Queries/, Abstractions/, exceptions
    Infrastructure/  Infrastructure.csproj  AppDbContext, entity configurations, migrations, seeding,
                                            repository and query implementations, DI extension
    WebApi/          WebApi.csproj          Program, endpoints, versioning, OpenAPI/Scalar, exception handler
  tests/
    UnitTests/        UnitTests.csproj
    AcceptanceTests/  AcceptanceTests.csproj
    EndToEndTests/    EndToEndTests.csproj
```

The project keeps the name `WebApi` so the AppHost's `Projects.WebApi` reference is unchanged. Root
namespaces match project names. All projects are added to `N2JSoft.slnx` under the existing
`/backend/src/` and `/backend/tests/` folders.

### Dependency rule

```text
Domain  ←  Application  ←  Infrastructure
               ↑                ↑
               └──── WebApi ────┘
```

- `Domain` references nothing outside the BCL.
- `Application` references `Domain` only (no EF Core, no ASP.NET Core).
- `Infrastructure` references `Application` and `Domain`, plus EF Core / Npgsql / Aspire client packages.
- `WebApi` references `Application` and `Infrastructure` and is the only composition root.

Types consumed across projects are `public`; everything else stays `internal`. `Infrastructure`
grants `InternalsVisibleTo` to `AcceptanceTests` so tests can use `AppDbContext`; `WebApi` grants it
to `UnitTests` so the exception handler can be unit-tested.

### Application layout (convention for all slices)

```text
Application/
  <Feature>/
    Commands/   <UseCase>.cs        request record + handler class (sealed), one file per use case
    Queries/    I<Feature>Queries.cs + DTO records
  Abstractions/ I<Aggregate>Repository.cs, IUnitOfWork.cs     (created from slice 2 onward)
  Exceptions/   NotFoundException.cs
  Paging/       PageRequest.cs, PagedResult.cs
  DependencyInjection.cs   AddApplication(this IServiceCollection)  (created with the first handler, slice 2)
```

- **Command handlers** load aggregates through write-side repositories, call domain methods (where the
  rules are enforced), and persist through `IUnitOfWork.SaveChangesAsync`. Unit-tested.
- **Queries** are interfaces returning DTOs; `Infrastructure` implements them with `AsNoTracking`
  projections (`Select`, `Skip`/`Take`). No logic; covered by the Acceptance tier.
- **Lists are paged.** A list query takes a `PageRequest(Page, PageSize)` (1-based, default size 20, at
  most 100) and returns `PagedResult<T>(Items, Page, PageSize, TotalCount)`, sorted on a unique key last
  so pages stay stable. Endpoints read `?page=&pageSize=` and validate them with `[Range]` (`400` otherwise).
- Endpoints inject a handler or a query interface directly; there is no mediator.

### Walking skeleton in this slice

- `Domain/Entities/User.cs` — moved as is (`Id`, `FirstName`, `LastName`), made `public`.
- `Application/Users/Queries/IUserQueries.cs` — `Task<PagedResult<UserSummary>> ListAsync(PageRequest, CancellationToken)`;
  `UserSummary(Guid Id, string Name)`.
- `Infrastructure/Users/UserQueries.cs` — EF projection, `Name = FirstName + " " + LastName`.
- `Infrastructure/Persistence/` — `AppDbContext`, `UserEntityTypeConfiguration`, `SeedAsync` (same two
  seeded users), migrations. The `Initial` migration moves to the new namespace and keeps its ID
  (`20251015145618_Initial`), so a database that already applied it still migrates.
- `Infrastructure/DependencyInjection.cs` — `AddInfrastructure(this IHostApplicationBuilder)` registers
  the Aspire Npgsql DbContext (connection name `database`, async seeding) and query implementations;
  a `MigrateDatabaseAsync(this IServiceProvider)` helper replaces the inline migration in `Program`.
- `WebApi/Endpoints/UserEndpoints.cs` — `GET /v1/users?page=&pageSize=` → `IUserQueries.ListAsync`.

## WebApi host

- **Versioning.** `AddApiVersioning` with `DefaultApiVersion = 1.0`, `ReportApiVersions = true`, URL
  segment reader; API explorer with `GroupNameFormat = "'v'VVV"` and `SubstituteApiVersionInUrl = true`.
  Each feature maps a versioned group: `app.NewVersionedApi("Users").MapGroup("/v{version:apiVersion}/users").HasApiVersion(1.0)`.
  `/health` stays unversioned.
- **OpenAPI + Scalar.** Built-in `AddOpenApi()` integrated with `Asp.Versioning.OpenApi` to produce one
  document per version. In Development: `MapOpenApi()` and `MapScalarApiReference()` at `/scalar`.
  The AppHost adds a dashboard link "Scalar" on the `webapi` resource pointing to `/scalar`.
- **Validation.** `builder.Services.AddValidation()`; request records carry DataAnnotations; failures
  return `400` `ValidationProblemDetails` with an `errors` dictionary. Registered in this slice, first
  exercised by slice 2's create-user request.
- **Error contract.** `AddProblemDetails()` + `UseExceptionHandler()` with one `IExceptionHandler`:

  | Exception                               | Defined in  | Status | Body                                                     |
  |-----------------------------------------|-------------|--------|----------------------------------------------------------|
  | `NotFoundException`                     | Application | 404    | ProblemDetails, `detail` names the missing resource      |
  | `BusinessRuleViolationException` (abstract base; concrete rules per slice) | Domain | 422 | ProblemDetails + `code` extension (e.g. `expense.quota_exceeded`) |
  | anything else                           | —           | 500    | ProblemDetails; exception detail only in Development     |

  `detail` is English developer text; the frontend maps `code` to its French user message.
- **Unchanged.** `/health` health checks, HTTPS redirection, Development-only migration at startup
  (log and stop on failure, as today). The process now exits with code 1, so Aspire shows the failure.
- **Redis removal.** Delete `AddRedisClient`, the `cache` resource and `WithReference(cache)` /
  `WaitFor(cache)` in the AppHost, and the `Aspire.StackExchange.Redis` / `Aspire.Hosting.Redis`
  package entries.

## Frontend skeleton

- Delete `nx-welcome.ts`; `app.html` becomes a header with a nav link "Utilisateurs" and `<router-outlet>`.
- `app.config.ts`: `registerLocaleData(localeFr)` and `{ provide: LOCALE_ID, useValue: 'fr-FR' }`.
  Slices 3–4 rely on it (`fullDate` → "mercredi 15 octobre 2025", French month names).
- Layout:

  ```text
  src/app/
    core/
      paged-result.ts      interface PagedResult<T> { items; page; pageSize; totalCount }
    features/users/
      user-summary.ts      interface UserSummary { id: string; name: string }
      users.api.ts         injectable; list(): Observable<PagedResult<UserSummary>> → GET /api/v1/users
      user-list.page.ts    standalone, consumes the API with toSignal, renders a <ul> of the first page's
                           names, or a French message if the request fails
  ```

- `app.routes.ts`: `''` and unknown URLs (`**`) redirect to `users`; `users` lazy-loads `user-list.page`.
- UI text in French; Tailwind used sparingly; zone.js setup unchanged.
- Vitest specs: `users.api.spec.ts` (calls `GET /api/v1/users`, via `HttpTestingController`) and
  `user-list.page.spec.ts` (renders the names returned by the API). The existing `app.spec.ts` is
  updated to match the new shell.

## Test tiers

Common to all three backend projects:

- xUnit v3 running on Microsoft.Testing.Platform (`"test": { "runner": "Microsoft.Testing.Platform" }`
  in `global.json`), Shouldly for assertions.
- Coverage through `Microsoft.Testing.Extensions.CodeCoverage`, Cobertura output.
- A `backend/tests/Directory.Build.props` (importing the root one) and an `.editorconfig` section scoped
  to `backend/tests/**` relax only the analyzer rules that conflict with test conventions (e.g. CA1707
  underscores in test names, CA2007 `ConfigureAwait` which xUnit discourages in tests), each with a
  comment stating why. `TreatWarningsAsErrors` stays on.

### UnitTests

- References `Domain`, `Application` and `WebApi`; NSubstitute for test doubles.
- Skeleton content:
  - architecture tests asserting the dependency rule through `Assembly.GetReferencedAssemblies()`:
    `Domain` references none of `Application`, `Infrastructure`, `Microsoft.EntityFrameworkCore*`,
    `Microsoft.AspNetCore*`; `Application` references neither `Infrastructure` nor EF Core nor
    ASP.NET Core;
  - exception handler tests (nothing throws the mapped exceptions yet in this slice): a
    `NotFoundException` yields `404`, a test-only `BusinessRuleViolationException` subclass yields
    `422` with its `code` extension, any other exception is left to the default `500` handling.
- Domain and command-handler tests start with slice 2.

### AcceptanceTests

- Packages: `Reqnroll.xunit.v3`, `Testcontainers.PostgreSql`, `Microsoft.AspNetCore.Mvc.Testing`,
  `Respawn`, Shouldly. References `WebApi` and `Infrastructure`.
- Lifecycle (Reqnroll hooks):
  - `[BeforeTestRun]`: start one Postgres container (image major version aligned with Aspire's
    default); create `WebApplicationFactory<Program>` with environment `Testing` and
    `ConnectionStrings:database` set to the container; apply migrations through `AppDbContext`
    (this also runs the dev seed, which the next hook wipes); create the Respawn checkpoint
    (Postgres adapter, `__EFMigrationsHistory` excluded).
  - `[BeforeScenario]`: reset the database with Respawn.
  - `[AfterTestRun]`: dispose the factory and the container.
- Step convention: *Given* inserts data through a scoped `AppDbContext`; *When* calls the HTTP API
  through an `ApiDriver` (wraps `HttpClient`, keeps the last response); *Then* asserts on status,
  body and ProblemDetails. Step classes grouped by domain (`UserSteps`), state shared through
  Reqnroll context injection.
- Test parallelization disabled for the assembly (scenarios share one database).
- Skeleton: `Features/Users.feature`

  ```gherkin
  Feature: List users

    Scenario: Listing returns every user
      Given the following users exist
        | First name | Last name |
        | Juste      | Leblanc   |
        | Marc       | Assin     |
      When I list the users
      Then the response status is 200
      And the users listed are
        | Name          |
        | Juste Leblanc |
        | Marc Assin    |
  ```

### EndToEndTests

- Packages: `Aspire.Hosting.Testing`, `Microsoft.Playwright`, Shouldly. ProjectReference to
  `tools/N2JSoft.Aspire`.
- An xUnit v3 assembly fixture (`[assembly: AssemblyFixture(typeof(AppFixture))]`):
  1. `DistributedApplicationTestingBuilder.CreateAsync<Projects.N2JSoft_Aspire>()`, build, start;
  2. wait for `webapi` healthy and `angular` running, then poll the `angular` HTTP endpoint until it
     answers (first `npm install` and Angular compile are slow: startup timeout ≈ 5 minutes);
  3. launch headless Chromium; each test gets a fresh browser context.
- Data: Postgres starts empty on each run and the webapi runs in Development, so the dev seed is
  present. Later slices' journeys create their own data through the UI.
- Skeleton test: open the app root, get redirected to the users page, see "Juste Leblanc" and
  "Marc Assin" (`getByRole('listitem')` / `getByText`).
- One-time local setup, documented in the README: `pwsh backend/tests/EndToEndTests/bin/Debug/net10.0/playwright.ps1 install chromium`.

## CI

`.github/workflows/CI.yml`:

| Job                  | Runners                  | Steps                                                                                                                                   |
|----------------------|--------------------------|-----------------------------------------------------------------------------------------------------------------------------------------|
| `backend`            | ubuntu, windows, macOS   | restore, build Release, ReSharper format check (ubuntu), `UnitTests` with coverage, upload coverage (ubuntu)                           |
| `backend-acceptance` | ubuntu                   | restore, build, `AcceptanceTests` (Docker from the runner)                                                                              |
| `e2e`                | ubuntu                   | setup .NET + Node 24, `npm ci` in `frontend/` (cached), build, install Playwright Chromium with deps, `EndToEndTests`; on failure upload screenshots and traces |
| `frontend`           | ubuntu                   | unchanged: `nx run-many -t lint test build`                                                                                             |

Tiers are selected by project path (`dotnet test --project backend/tests/<Tier>`), not by traits.

## Documentation

- `docs/superpowers/roadmap.md`: slice status set to "Done" for Foundation when merged.
- README "Mes Notes" (in French): what Foundation delivered; the decisions above and their reasons
  (Clean Architecture, lightweight CQRS, versioning, Scalar, Redis removal); how to run each test tier
  and the Playwright browser install; improvement ideas (generated API client, zoneless Angular).

## Risks to verify first

The implementation plan starts with one spike task per item; a failing spike changes the plan before
any dependent work.

1. `Reqnroll.xunit.v3` generates and runs scenarios under Microsoft.Testing.Platform (fallback: keep
   VSTest for all projects).
2. .NET 10 generates a `public partial class Program` usable by `WebApplicationFactory<Program>`
   (fallback: add the partial declaration in `Program.cs`).
3. `Asp.Versioning.OpenApi` produces the `v1` document with the built-in OpenAPI and Scalar shows it.
4. Under Aspire.Hosting.Testing, the `angular` resource reaches a state that can be awaited, its
   HTTP endpoint is reachable from the test process, and the `webapi` runs with the Development
   environment (so migrations and the dev seed apply).
5. The exact Aspire 13.6 API for adding a custom dashboard URL ("Scalar") to the `webapi` resource.

## Definition of done

- Unit, Acceptance, E2E and frontend suites green locally and in CI.
- `dotnet run --project tools/N2JSoft.Aspire` shows the users page listing the seeded users.
- Scalar opens from the Aspire dashboard and shows the `v1` document with `GET /v1/users`.
- No Redis left in code, packages or AppHost.
- README "Mes Notes" and the roadmap updated; PR `feat/foundation` → `develop` opened.
