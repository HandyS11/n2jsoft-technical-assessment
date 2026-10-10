# Foundation Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Restructure the backend into Clean Architecture projects with an enforced dependency rule, establish
the API conventions (versioning, OpenAPI + Scalar, error contract), remove Redis, add the Unit / Acceptance /
End-to-end test tiers, replace the Nx welcome page with a French users shell, and split CI by tier.

**Architecture:** `Domain ← Application ← Infrastructure`, with `WebApi` as the only composition root. Reads go
through query interfaces declared in `Application` and implemented in `Infrastructure` as EF Core projections.
Errors flow through one `IExceptionHandler` into RFC 9457 ProblemDetails. Everything is proven end to end on
the single existing endpoint, `GET /v1/users`.

**Tech Stack:** .NET 10 (SDK 10.0.401), ASP.NET Core minimal APIs, EF Core 10 + Npgsql through Aspire 13.6.1,
Asp.Versioning 10.2, Scalar, xUnit v3 4.x on Microsoft.Testing.Platform, Shouldly, NSubstitute, Reqnroll,
Testcontainers, Respawn, Aspire.Hosting.Testing, Playwright for .NET, Angular 22 + Nx 23 + Vitest.

**Spec:** [docs/superpowers/specs/2026-10-10-foundation-design.md](../specs/2026-10-10-foundation-design.md)

## Verified during planning

The spec's "Risks to verify first" were spiked in throwaway projects while writing this plan, so no spike tasks
remain.

- **Under the repo's analyzer set** (warnings as errors): the test-stack and WebApi spikes, and Task 5's frontend
  code. These compiled and ran.
- **Without the analyzers**: the Aspire/Playwright spike. Task 6's glue code may need small analyzer fixes, and
  the build tells you which.

1. **Reqnroll under MTP**: `Reqnroll.xunit.v3` 3.3.4 + `xunit.v3` 4.0.2 generate and run scenarios on
   Microsoft.Testing.Platform. `global.json` **must** contain `"test": { "runner": "Microsoft.Testing.Platform" }`,
   otherwise `dotnet test` fails with "Testing with VSTest target is no longer supported".
2. **`Program` for `WebApplicationFactory`**: the .NET 10 SDK generates `public partial class Program`; no manual
   declaration is needed.
3. **Versioned OpenAPI**: `AddApiVersioning(...).AddApiExplorer(...).AddOpenApi()` plus
   `app.MapOpenApi().WithDocumentPerVersion()` serves `/openapi/v1.json` with the path `/v1/users`. The
   Asp.Versioning analyzers fail the build (AV0029/AV0030) if the plain `builder.Services.AddOpenApi()` stays.
   Scalar is served at `/scalar/`.
4. **Aspire.Hosting.Testing**: the AppHost boots, `webapi` runs in Development (migrations + seed apply), the
   `angular` endpoint named `http` is reachable from the test process, a warm run takes about 30 s.
   `WaitForResourceHealthyAsync` only waits for real health when the resource has `WithHttpHealthCheck`.
5. **Dashboard URL**: `.WithUrlForEndpoint("https", _ => new() { Url = "/scalar", DisplayText = "Scalar" })`.

## Spec deviations (decided while planning)

- **Playwright browser install**: the E2E fixture installs Chromium itself through
  `Microsoft.Playwright.Program.Main(["install", "chromium"])` (0.2 s once cached). No manual `pwsh` step is
  needed locally; `pwsh` is not installed on the dev machine. CI still installs system libraries with
  `--with-deps`.
- **Listing order**: `GET /v1/users` returns users sorted by last name, then first name, so the list is stable.
- **Error contract additions**:
  - `app.UseStatusCodePages()` turns body-less 404/400 responses (unknown route, unsupported API version) into
    ProblemDetails.
  - `BadHttpRequestException` maps to its own status code (400). Otherwise malformed JSON returns 500 in
    Development, where `ThrowOnBadRequest` is on.
  - The built-in 500 response never carries exception detail. The spec wants it in Development, so
    `ProblemDetailsOptions.CustomizeProblemDetails` adds it there only.
- **`DefaultApiVersion`** is not set: 1.0 is already the default (Asp.Versioning analyzer AV0011).
- **Class naming**: the DI extension classes are named `DependencyInjectionExtensions`, the name the repo
  already uses. A class named `DependencyInjection` fails CA1724, because it clashes with
  `Microsoft.Extensions.DependencyInjection`.
- **Analyzer relaxations** (global, in `.editorconfig`, with a reason comment):
  - CA1812, because internal classes created by DI are reported as never instantiated.
  - CA1032 and RCS1194, because exceptions require their context and parameterless constructors would allow
    meaningless instances.
  - The existing `[tests/**]` section never matched `backend/tests`; it becomes `[backend/tests/**]`.
- **Testing the API documentation**: OpenAPI and Scalar are checked by Acceptance scenarios against a
  Development-environment `WebApplicationFactory`. E2E checks the dashboard "Scalar" link.
- **AppHost health check**: `webapi` gets `.WithHttpHealthCheck("/health")`, so Aspire, the dashboard and the
  E2E fixture see real health.
- **Postgres image** for Acceptance: `postgres:18.3`, Aspire 13.6.1's default.

## Global Constraints

- SDK `10.0.401`, `rollForward: latestMinor`. `global.json` gains `"test": { "runner": "Microsoft.Testing.Platform" }`.
- Central package management: versions live only in `Directory.Packages.props`. New versions, exactly:
  `xunit.v3` 4.0.2, `Shouldly` 4.3.0, `NSubstitute` 6.2.0, `NSubstitute.Analyzers.CSharp` 1.0.17,
  `Microsoft.Testing.Extensions.CodeCoverage` 18.12.0, `Reqnroll.xunit.v3` 3.3.4, `Testcontainers.PostgreSql`
  4.16.0, `Respawn` 7.0.0, `Microsoft.AspNetCore.Mvc.Testing` 10.0.12, `Asp.Versioning.Http` 10.2.3,
  `Asp.Versioning.Mvc.ApiExplorer` 10.2.1, `Asp.Versioning.OpenApi` 10.2.3, `Scalar.AspNetCore` 2.17.14,
  `Aspire.Hosting.Testing` 13.6.1, `Microsoft.Playwright` 1.63.0.
- Do not add `Microsoft.NET.Test.Sdk` or `xunit.runner.visualstudio`: they bring VSTest back and break `dotnet test`.
- `TreatWarningsAsErrors` stays on. No `#pragma warning disable`, no `<NoWarn>`. The only new rule relaxations are
  the ones this plan lists, each in `.editorconfig` with a reason comment.
- Projects and root namespaces: `Domain`, `Application`, `Infrastructure`, `WebApi` (the name stays so the
  AppHost's `Projects.WebApi` is unchanged); tests `UnitTests`, `AcceptanceTests`, `EndToEndTests`. Test project
  folder names must contain `Tests` (the `.editorconfig` test section matches `**/*Tests*/**.cs`).
- Dependency rule: `Domain` references nothing; `Application` references `Domain` only (no EF Core, no ASP.NET
  Core); `Infrastructure` references `Application` and `Domain`; `WebApi` references `Application` and
  `Infrastructure`.
- Types consumed across projects are `public`; everything else is `internal`.
- Every method returning `Task`/`ValueTask` ends with `Async`, Reqnroll steps and hooks included (VSTHRD200).
  xUnit `[Fact]`/`[Theory]` methods are exempt and use underscore sentences.
- English for code, Gherkin, commit messages and ProblemDetails `detail`. French for UI text and README
  "Mes Notes".
- Run every command from the repository root. `.csproj` files use 4-space indentation, like the existing ones.
- Before each backend commit, run `dotnet tool restore` (once) then
  `dotnet jb cleanupcode N2JSoft.slnx --profile="ReformatAndReorder" --verbosity=ERROR`. CI fails on any diff it
  produces. If it moves code, rerun the task's tests before committing.
- Commits: conventional commits, small. **No `Co-Authored-By` trailer and no "Generated with" line**. This is a
  repository rule that overrides any default attribution.

## Review Focus

Inputs the spec implies but does not spell out, most likely to bite first. Each one has a test in the owning task.

1. **Listing order**: users appear in a stable order (last name, then first name), not in whatever order
   Postgres returns. Pinned in Task 2.
2. **Dev seed leaking into acceptance scenarios**: migrating the test database runs the dev seed, and a scenario
   that expects no users must see none. Pinned in Task 2.
3. **Exception detail outside Development**: an unhandled exception must not expose its message or stack trace
   in Production. Pinned in Task 3.
4. **Wrong API version / old URL**: `GET /v2/users` and the old `GET /users` answer 404 *with* a ProblemDetails
   body, not an empty response. Pinned in Task 4.
5. **Users page before or without data**: while the request is pending the page says "Chargement…", and an
   empty list says "Aucun utilisateur." instead of rendering nothing. Pinned in Task 5.

---

## File map

```text
global.json                                   + test runner = Microsoft.Testing.Platform           (T1)
Directory.Packages.props                      − Redis packages, + test/versioning/docs packages      (T1–T6)
.editorconfig                                 CA1812/CA1032/RCS1194 relaxed, [backend/tests/**]     (T1, T3)
N2JSoft.slnx                                  + all new projects                                    (T1, T2, T6)
backend/src/Domain/
  Domain.csproj
  Entities/User.cs                            moved from WebApi, now public                         (T1)
  Exceptions/BusinessRuleViolationException.cs                                                      (T3)
backend/src/Application/
  Application.csproj
  Users/Queries/IUserQueries.cs, UserSummary.cs                                                     (T1)
  Exceptions/NotFoundException.cs                                                                   (T3)
backend/src/Infrastructure/
  Infrastructure.csproj
  DependencyInjectionExtensions.cs            AddInfrastructure, MigrateDatabaseAsync               (T1)
  Persistence/AppDbContext.cs, UserEntityTypeConfiguration.cs, Migrations/*   moved/regenerated     (T1)
  Users/UserQueries.cs                                                                              (T1, T2)
backend/src/WebApi/
  WebApi.csproj, Program.cs, WebApi.http                                                            (T1, T3, T4)
  Endpoints/UserEndpoints.cs                                                                        (T1, T4)
  Errors/ApiExceptionHandler.cs, ErrorHandlingExtensions.cs                                         (T3)
  Domain/, Infrastructure/                    deleted                                               (T1)
backend/tests/Directory.Build.props           shared test setup                                     (T1)
backend/tests/UnitTests/                      architecture + error handling tests                   (T1, T3)
backend/tests/AcceptanceTests/                Reqnroll features, steps, hooks, support              (T2, T3, T4)
backend/tests/EndToEndTests/                  Aspire + Playwright                                   (T6)
tools/N2JSoft.Aspire/AppHost.cs, N2JSoft.Aspire.csproj   − Redis (T1); + health check, Scalar link (T6)
frontend/src/...                              French shell, users feature                           (T5)
.github/workflows/CI.yml, .github/dependabot.yml                                                    (T7)
README.md, docs/superpowers/roadmap.md                                                              (T8)
```

---

### Task 1: Remove Redis, restructure into Clean Architecture, guard it with architecture tests

**Files:**

- Modify: `backend/src/WebApi/Program.cs`, `backend/src/WebApi/WebApi.csproj`, `tools/N2JSoft.Aspire/AppHost.cs`,
  `tools/N2JSoft.Aspire/N2JSoft.Aspire.csproj`, `Directory.Packages.props`, `global.json`, `.editorconfig`,
  `N2JSoft.slnx`
- Create: `backend/tests/Directory.Build.props`, `backend/tests/UnitTests/UnitTests.csproj`,
  `backend/tests/UnitTests/Architecture/DependencyRuleTests.cs`
- Create: `backend/src/Domain/Domain.csproj`, `backend/src/Domain/Entities/User.cs`
- Create: `backend/src/Application/Application.csproj`, `backend/src/Application/Users/Queries/IUserQueries.cs`,
  `backend/src/Application/Users/Queries/UserSummary.cs`
- Create: `backend/src/Infrastructure/Infrastructure.csproj`,
  `backend/src/Infrastructure/DependencyInjectionExtensions.cs`,
  `backend/src/Infrastructure/Persistence/AppDbContext.cs`,
  `backend/src/Infrastructure/Persistence/UserEntityTypeConfiguration.cs`,
  `backend/src/Infrastructure/Persistence/Migrations/*` (generated), `backend/src/Infrastructure/Users/UserQueries.cs`
- Create: `backend/src/WebApi/Endpoints/UserEndpoints.cs`
- Delete: `backend/src/WebApi/Domain/`, `backend/src/WebApi/Infrastructure/` (including the old migrations)

**Interfaces:**

- Consumes: nothing.
- Produces:
  - `Domain.Entities.User` (public sealed class): `Guid Id { get; set; }`, `required string FirstName { get; init; }`,
    `required string LastName { get; init; }`.
  - `Application.Users.Queries.IUserQueries`: `Task<IReadOnlyList<UserSummary>> ListAsync(CancellationToken cancellationToken)`.
  - `Application.Users.Queries.UserSummary`: `public sealed record UserSummary(Guid Id, string Name)`.
  - `Infrastructure.DependencyInjectionExtensions`:
    - `IHostApplicationBuilder AddInfrastructure(this IHostApplicationBuilder builder)`
    - `Task MigrateDatabaseAsync(this IServiceProvider services, CancellationToken cancellationToken = default)`
  - `Infrastructure.Persistence.AppDbContext` (internal sealed; Task 2 makes it visible to `AcceptanceTests`).
  - `WebApi.Endpoints.UserEndpoints.MapUserEndpoints(this IEndpointRouteBuilder)`: maps `GET /users` (Task 4
    versions it).
  - `backend/tests/Directory.Build.props`: every test project gets `xunit.v3`, `Shouldly`, the coverage extension,
    `OutputType Exe`, the MTP runner, and global usings `Xunit` and `Shouldly`.

- [ ] **Step 1: Remove Redis from the WebApi**

  In `backend/src/WebApi/Program.cs` delete the line:

  ```csharp
  builder.AddRedisClient(connectionName: "cache");
  ```

  In `backend/src/WebApi/WebApi.csproj` delete:

  ```xml
          <PackageReference Include="Aspire.StackExchange.Redis" />
  ```

- [ ] **Step 2: Remove Redis from the AppHost**

  Replace `tools/N2JSoft.Aspire/AppHost.cs` with:

  ```csharp
  var builder = DistributedApplication.CreateBuilder(args);

  var postgres = builder.AddPostgres("postgres");
  var mainDatabase = postgres.AddDatabase("database");

  var webapi = builder.AddProject<Projects.WebApi>("webapi")
      .WithReference(mainDatabase)
      .WaitFor(postgres)
      .WaitFor(mainDatabase);

  builder.AddJavaScriptApp("angular", "../../frontend", "start")
      .WithReference(webapi)
      .WaitFor(webapi)
      .WithHttpEndpoint(env: "PORT")
      .WithExternalHttpEndpoints();

  await builder.Build().RunAsync().ConfigureAwait(false);
  ```

  In `tools/N2JSoft.Aspire/N2JSoft.Aspire.csproj` delete `<PackageReference Include="Aspire.Hosting.Redis" />`.
  In `Directory.Packages.props` delete the `Aspire.Hosting.Redis` and `Aspire.StackExchange.Redis` lines.

- [ ] **Step 3: Verify that Redis is gone and the solution builds**

  Run: `grep -rniE 'redis' --include='*.cs' --include='*.csproj' --include='*.props' backend tools Directory.Packages.props`
  Expected: no output.

  Run: `dotnet build N2JSoft.slnx`
  Expected: `Build succeeded`, 0 warnings.

- [ ] **Step 4: Commit**

  ```bash
  git add backend/src/WebApi/Program.cs backend/src/WebApi/WebApi.csproj tools/N2JSoft.Aspire Directory.Packages.props
  git commit -m "chore: remove unused Redis cache"
  ```

- [ ] **Step 5: Switch `dotnet test` to Microsoft.Testing.Platform**

  Replace `global.json` with:

  ```json
  {
    "sdk": {
      "version": "10.0.401",
      "rollForward": "latestMinor",
      "allowPrerelease": false
    },
    "test": {
      "runner": "Microsoft.Testing.Platform"
    }
  }
  ```

- [ ] **Step 6: Add the shared test packages**

  In `Directory.Packages.props`, add this group after the "ASP.NET Core & EF Core" group:

  ```xml
    <ItemGroup>
      <!-- Testing -->
      <PackageVersion Include="Microsoft.Testing.Extensions.CodeCoverage" Version="18.12.0" />
      <PackageVersion Include="Shouldly" Version="4.3.0" />
      <PackageVersion Include="xunit.v3" Version="4.0.2" />
    </ItemGroup>
  ```

- [ ] **Step 7: Create `backend/tests/Directory.Build.props`**

  ```xml
  <Project>
      <Import Project="$([MSBuild]::GetPathOfFileAbove('Directory.Build.props', '$(MSBuildThisFileDirectory)../'))" />

      <PropertyGroup>
          <!-- xUnit v3 test projects are executables running on Microsoft.Testing.Platform -->
          <OutputType>Exe</OutputType>
          <UseMicrosoftTestingPlatformRunner>true</UseMicrosoftTestingPlatformRunner>
      </PropertyGroup>

      <ItemGroup>
          <PackageReference Include="Microsoft.Testing.Extensions.CodeCoverage" />
          <PackageReference Include="Shouldly" />
          <PackageReference Include="xunit.v3" />
      </ItemGroup>

      <ItemGroup>
          <Using Include="Shouldly" />
          <Using Include="Xunit" />
      </ItemGroup>
  </Project>
  ```

- [ ] **Step 8: Fix the `.editorconfig` test section and relax CA1812**

  In `.editorconfig`, in the `# Dotnet Diagnostic Rules` list, add after the CA1848 line:

  ```ini
  dotnet_diagnostic.CA1812.severity = none # Avoid uninstantiated internal classes — internal classes created by DI (queries, DbContext, handlers) are reported as never instantiated
  ```

  Replace the section header `[tests/**]` with `[backend/tests/**]`. It was anchored to a root `tests/` folder
  that does not exist, so its VSTHRD003/VSTHRD103 relaxations never applied.

- [ ] **Step 9: Write the failing architecture tests**

  Create `backend/tests/UnitTests/UnitTests.csproj`:

  ```xml
  <Project Sdk="Microsoft.NET.Sdk">

      <ItemGroup>
          <ProjectReference Include="..\..\src\Application\Application.csproj" />
          <ProjectReference Include="..\..\src\Domain\Domain.csproj" />
          <ProjectReference Include="..\..\src\WebApi\WebApi.csproj" />
      </ItemGroup>

  </Project>
  ```

  Create `backend/tests/UnitTests/Architecture/DependencyRuleTests.cs`:

  ```csharp
  using System.Reflection;
  using Application.Users.Queries;
  using Domain.Entities;

  namespace UnitTests.Architecture;

  public sealed class DependencyRuleTests
  {
      private static readonly Assembly DomainAssembly = typeof(User).Assembly;
      private static readonly Assembly ApplicationAssembly = typeof(IUserQueries).Assembly;

      [Theory]
      [InlineData("Application")]
      [InlineData("Infrastructure")]
      [InlineData("WebApi")]
      [InlineData("Microsoft.EntityFrameworkCore")]
      [InlineData("Microsoft.AspNetCore")]
      public void Domain_does_not_depend_on(string forbidden)
      {
          ReferencedAssemblyNames(DomainAssembly)
              .ShouldNotContain(name => DependsOn(name, forbidden), $"Domain must not reference {forbidden}");
      }

      [Theory]
      [InlineData("Infrastructure")]
      [InlineData("WebApi")]
      [InlineData("Microsoft.EntityFrameworkCore")]
      [InlineData("Microsoft.AspNetCore")]
      public void Application_does_not_depend_on(string forbidden)
      {
          ReferencedAssemblyNames(ApplicationAssembly)
              .ShouldNotContain(name => DependsOn(name, forbidden), $"Application must not reference {forbidden}");
      }

      private static string[] ReferencedAssemblyNames(Assembly assembly)
      {
          return [.. assembly.GetReferencedAssemblies().Select(name => name.Name ?? string.Empty)];
      }

      // "Microsoft.AspNetCore" also forbids "Microsoft.AspNetCore.Http.Abstractions" and the like.
      private static bool DependsOn(string assemblyName, string forbidden)
      {
          return assemblyName == forbidden || assemblyName.StartsWith(forbidden + ".", StringComparison.Ordinal);
      }
  }
  ```

  Only assemblies that the compiled code actually uses appear in `GetReferencedAssemblies()`. The tests
  therefore guard what the code does, not what the `.csproj` declares.

- [ ] **Step 10: Run the tests to verify they fail**

  Run: `dotnet test --project backend/tests/UnitTests`
  Expected: FAIL. Restore or build errors, because `Domain.csproj`/`Application.csproj` do not exist yet
  (`MSB...` project-not-found or `CS0246` for `User` / `IUserQueries`).

- [ ] **Step 11: Create the Domain project and move `User`**

  `backend/src/Domain/Domain.csproj`:

  ```xml
  <Project Sdk="Microsoft.NET.Sdk">

  </Project>
  ```

  `backend/src/Domain/Entities/User.cs`:

  ```csharp
  namespace Domain.Entities;

  public sealed class User
  {
      public Guid Id { get; set; }
      public required string FirstName { get; init; }
      public required string LastName { get; init; }
  }
  ```

- [ ] **Step 12: Create the Application project with the user query contract**

  `backend/src/Application/Application.csproj`:

  ```xml
  <Project Sdk="Microsoft.NET.Sdk">

      <ItemGroup>
          <ProjectReference Include="..\Domain\Domain.csproj" />
      </ItemGroup>

  </Project>
  ```

  `backend/src/Application/Users/Queries/UserSummary.cs`:

  ```csharp
  namespace Application.Users.Queries;

  public sealed record UserSummary(Guid Id, string Name);
  ```

  `backend/src/Application/Users/Queries/IUserQueries.cs`:

  ```csharp
  namespace Application.Users.Queries;

  public interface IUserQueries
  {
      Task<IReadOnlyList<UserSummary>> ListAsync(CancellationToken cancellationToken);
  }
  ```

- [ ] **Step 13: Create the Infrastructure project and move persistence into it**

  `backend/src/Infrastructure/Infrastructure.csproj`:

  ```xml
  <Project Sdk="Microsoft.NET.Sdk">

      <ItemGroup>
          <PackageReference Include="Aspire.Npgsql.EntityFrameworkCore.PostgreSQL" />
      </ItemGroup>

      <ItemGroup>
          <ProjectReference Include="..\Application\Application.csproj" />
          <ProjectReference Include="..\Domain\Domain.csproj" />
      </ItemGroup>

  </Project>
  ```

  `backend/src/Infrastructure/Persistence/AppDbContext.cs` is the old file with the new namespaces. The
  `[SuppressMessage("...CA1812...")]` attribute and its `using` are dropped, because CA1812 is now relaxed
  globally (Step 8):

  ```csharp
  using Domain.Entities;
  using Microsoft.EntityFrameworkCore;

  namespace Infrastructure.Persistence;

  internal sealed class AppDbContext(DbContextOptions<AppDbContext> options) : DbContext(options)
  {
      protected override void OnModelCreating(ModelBuilder modelBuilder)
      {
          base.OnModelCreating(modelBuilder);
          modelBuilder.ApplyConfiguration(new UserEntityTypeConfiguration());
      }

      internal static async Task SeedAsync(DbContext context, CancellationToken cancellationToken)
      {
          if (!await context.Set<User>().AnyAsync(cancellationToken).ConfigureAwait(false))
          {
              await context.Set<User>().AddRangeAsync(
                  [
                      new User
                      {
                          FirstName = "Juste", LastName = "Leblanc"
                      },
                      new User
                      {
                          FirstName = "Marc", LastName = "Assin"
                      },
                  ],
                  cancellationToken).ConfigureAwait(false);
              await context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
          }
      }
  }
  ```

  `backend/src/Infrastructure/Persistence/UserEntityTypeConfiguration.cs`:

  ```csharp
  using Domain.Entities;
  using Microsoft.EntityFrameworkCore;
  using Microsoft.EntityFrameworkCore.Metadata.Builders;

  namespace Infrastructure.Persistence;

  internal sealed class UserEntityTypeConfiguration : IEntityTypeConfiguration<User>
  {
      public void Configure(EntityTypeBuilder<User> builder)
      {
          builder.ToTable("users");
      }
  }
  ```

  `backend/src/Infrastructure/Users/UserQueries.cs`:

  ```csharp
  using Application.Users.Queries;
  using Domain.Entities;
  using Infrastructure.Persistence;
  using Microsoft.EntityFrameworkCore;

  namespace Infrastructure.Users;

  internal sealed class UserQueries(AppDbContext dbContext) : IUserQueries
  {
      public async Task<IReadOnlyList<UserSummary>> ListAsync(CancellationToken cancellationToken)
      {
          return await dbContext.Set<User>()
              .AsNoTracking()
              .Select(user => new UserSummary(user.Id, $"{user.FirstName} {user.LastName}"))
              .ToListAsync(cancellationToken)
              .ConfigureAwait(false);
      }
  }
  ```

  `backend/src/Infrastructure/DependencyInjectionExtensions.cs`:

  ```csharp
  using Application.Users.Queries;
  using Infrastructure.Persistence;
  using Infrastructure.Users;
  using Microsoft.EntityFrameworkCore;
  using Microsoft.Extensions.DependencyInjection;
  using Microsoft.Extensions.Hosting;

  namespace Infrastructure;

  public static class DependencyInjectionExtensions
  {
      public static IHostApplicationBuilder AddInfrastructure(this IHostApplicationBuilder builder)
      {
          builder.AddNpgsqlDbContext<AppDbContext>(connectionName: "database",
              configureDbContextOptions: options =>
                  options.UseAsyncSeeding(async (context, _, cancellationToken) =>
                      await AppDbContext.SeedAsync(context, cancellationToken).ConfigureAwait(false)));

          builder.Services.AddScoped<IUserQueries, UserQueries>();

          return builder;
      }

      public static async Task MigrateDatabaseAsync(
          this IServiceProvider services,
          CancellationToken cancellationToken = default)
      {
          using var scope = services.CreateScope();
          await scope.ServiceProvider.GetRequiredService<AppDbContext>()
              .Database.MigrateAsync(cancellationToken).ConfigureAwait(false);
      }
  }
  ```

- [ ] **Step 14: Rewire the WebApi as the composition root**

  `backend/src/WebApi/WebApi.csproj`:

  ```xml
  <Project Sdk="Microsoft.NET.Sdk.Web">

      <ItemGroup>
          <PackageReference Include="Microsoft.AspNetCore.OpenApi" />
          <PackageReference Include="Microsoft.EntityFrameworkCore.Design">
              <PrivateAssets>all</PrivateAssets>
              <IncludeAssets>runtime; build; native; contentfiles; analyzers; buildtransitive</IncludeAssets>
          </PackageReference>
      </ItemGroup>

      <ItemGroup>
          <ProjectReference Include="..\Application\Application.csproj" />
          <ProjectReference Include="..\Infrastructure\Infrastructure.csproj" />
      </ItemGroup>

  </Project>
  ```

  `backend/src/WebApi/Endpoints/UserEndpoints.cs`:

  ```csharp
  using Application.Users.Queries;
  using Microsoft.AspNetCore.Http.HttpResults;

  namespace WebApi.Endpoints;

  internal static class UserEndpoints
  {
      public static IEndpointRouteBuilder MapUserEndpoints(this IEndpointRouteBuilder endpoints)
      {
          endpoints.MapGet("/users", ListUsersAsync);

          return endpoints;
      }

      private static async Task<Ok<IReadOnlyList<UserSummary>>> ListUsersAsync(
          IUserQueries userQueries,
          CancellationToken cancellationToken)
      {
          var users = await userQueries.ListAsync(cancellationToken).ConfigureAwait(false);
          return TypedResults.Ok(users);
      }
  }
  ```

  `backend/src/WebApi/Program.cs`:

  ```csharp
  using Infrastructure;
  using WebApi.Endpoints;

  var builder = WebApplication.CreateBuilder(args);

  builder.AddInfrastructure();
  builder.Services.AddOpenApi();
  builder.Services.AddHealthChecks();

  var app = builder.Build();

  if (app.Environment.IsDevelopment())
  {
      try
      {
          await app.Services.MigrateDatabaseAsync().ConfigureAwait(false);
      }
      catch (Exception e)
      {
          app.Logger.LogError(e, "An error occurred while migrating the database.");
          return;
      }

      app.MapOpenApi();
  }

  app.UseHttpsRedirection();

  app.MapUserEndpoints();
  app.MapHealthChecks("/health");

  await app.RunAsync().ConfigureAwait(false);
  ```

  Delete the old folders:

  ```bash
  git rm -r backend/src/WebApi/Domain backend/src/WebApi/Infrastructure
  ```

- [ ] **Step 15: Register the projects in the solution**

  In `N2JSoft.slnx`, replace the two backend folders with:

  ```xml
    <Folder Name="/backend/src/">
      <Project Path="backend/src/Application/Application.csproj" />
      <Project Path="backend/src/Domain/Domain.csproj" />
      <Project Path="backend/src/Infrastructure/Infrastructure.csproj" />
      <Project Path="backend/src/WebApi/WebApi.csproj" />
    </Folder>
    <Folder Name="/backend/tests/">
      <Project Path="backend/tests/UnitTests/UnitTests.csproj" />
    </Folder>
  ```

- [ ] **Step 16: Regenerate the `Initial` migration under the new namespace**

  Nothing is deployed, so the history is not kept. `dotnet ef` runs `Program` up to `builder.Build()`, and the
  Aspire Npgsql component refuses to build the DbContext without a connection string. A dummy one is passed
  after `--`; no database is contacted.

  ```bash
  dotnet tool restore
  dotnet ef migrations add Initial \
    --project backend/src/Infrastructure \
    --startup-project backend/src/WebApi \
    --output-dir Persistence/Migrations \
    -- --ConnectionStrings:database "Host=localhost;Database=design;Username=design;Password=design"
  ```

  Expected: `Done.` and three files under `backend/src/Infrastructure/Persistence/Migrations/`
  (`<timestamp>_Initial.cs`, `<timestamp>_Initial.Designer.cs`, `AppDbContextModelSnapshot.cs`) in namespace
  `Infrastructure.Persistence.Migrations`. `<timestamp>_Initial.cs` creates the `users` table with `Id uuid`,
  `FirstName text`, `LastName text` and `PK_users`, like the deleted migration.

- [ ] **Step 17: Run the tests to verify they pass**

  Run: `dotnet build N2JSoft.slnx`
  Expected: `Build succeeded`, 0 warnings.

  Run: `dotnet test --project backend/tests/UnitTests`
  Expected: PASS, 9 tests (5 Domain + 4 Application cases).

- [ ] **Step 18: Format and commit**

  ```bash
  dotnet jb cleanupcode N2JSoft.slnx --profile="ReformatAndReorder" --verbosity=ERROR
  git add -A backend global.json Directory.Packages.props .editorconfig N2JSoft.slnx
  git commit -m "refactor(backend): split into Domain, Application, Infrastructure and WebApi projects"
  ```

  `GET /users` is now served through `IUserQueries`. Task 2's acceptance scenarios cover it against a real
  database.

---

### Task 2: Acceptance tier (Reqnroll + Testcontainers + Respawn) on the users listing

**Files:**

- Modify: `Directory.Packages.props`, `N2JSoft.slnx`, `backend/src/Infrastructure/Infrastructure.csproj`,
  `backend/src/Infrastructure/Users/UserQueries.cs`
- Create: `backend/tests/AcceptanceTests/AcceptanceTests.csproj`, `AssemblyInfo.cs`,
  `Support/TestEnvironment.cs`, `Support/ApiFactory.cs`, `Support/ApiDriver.cs`, `Hooks/TestRunHooks.cs`,
  `StepDefinitions/UserSteps.cs`, `StepDefinitions/HttpSteps.cs`, `Features/Users.feature` (all under
  `backend/tests/AcceptanceTests/`)

**Interfaces:**

- Consumes: `Infrastructure.DependencyInjectionExtensions.MigrateDatabaseAsync`,
  `Infrastructure.Persistence.AppDbContext`, `Domain.Entities.User`, `Application.Users.Queries.UserSummary`,
  `GET /users` (Task 1).
- Produces (used by Tasks 3 and 4):
  - `AcceptanceTests.Support.TestEnvironment` (public static):
    - `WebApplicationFactory<Program> Factory`
    - `Task StartAsync()`, `Task ResetDatabaseAsync()`, `Task StopAsync()`
  - `AcceptanceTests.Support.ApiDriver` (context-injected, one per scenario):
    - `HttpResponseMessage Response`
    - `Task GetAsync(string path)`
  - Step `Then the response status is {int}` (in `HttpSteps`).
  - `UserSteps.UsersPath` = `"/users"`. Task 4 changes it to `/v1/users`.

- [ ] **Step 1: Add the acceptance packages**

  In `Directory.Packages.props`, extend the `<!-- Testing -->` group so it reads:

  ```xml
    <ItemGroup>
      <!-- Testing -->
      <PackageVersion Include="Microsoft.AspNetCore.Mvc.Testing" Version="10.0.12" />
      <PackageVersion Include="Microsoft.Testing.Extensions.CodeCoverage" Version="18.12.0" />
      <PackageVersion Include="Reqnroll.xunit.v3" Version="3.3.4" />
      <PackageVersion Include="Respawn" Version="7.0.0" />
      <PackageVersion Include="Shouldly" Version="4.3.0" />
      <PackageVersion Include="Testcontainers.PostgreSql" Version="4.16.0" />
      <PackageVersion Include="xunit.v3" Version="4.0.2" />
    </ItemGroup>
  ```

- [ ] **Step 2: Create the project and let the tests see `AppDbContext`**

  `backend/tests/AcceptanceTests/AcceptanceTests.csproj`:

  ```xml
  <Project Sdk="Microsoft.NET.Sdk">

      <PropertyGroup>
          <!-- Generated *.feature.cs files go to obj/ instead of next to the .feature files -->
          <ReqnrollUseIntermediateOutputPathForCodeBehind>true</ReqnrollUseIntermediateOutputPathForCodeBehind>
      </PropertyGroup>

      <ItemGroup>
          <PackageReference Include="Microsoft.AspNetCore.Mvc.Testing" />
          <PackageReference Include="Reqnroll.xunit.v3" />
          <PackageReference Include="Respawn" />
          <PackageReference Include="Testcontainers.PostgreSql" />
      </ItemGroup>

      <ItemGroup>
          <ProjectReference Include="..\..\src\Infrastructure\Infrastructure.csproj" />
          <ProjectReference Include="..\..\src\WebApi\WebApi.csproj" />
      </ItemGroup>

  </Project>
  ```

  `Npgsql` (needed by Respawn) comes transitively through `Infrastructure`.

  `backend/tests/AcceptanceTests/AssemblyInfo.cs`:

  ```csharp
  using Xunit.Sdk;
  using Xunit.v3;

  // Scenarios share one database that is reset before each scenario: they must not run concurrently.
  [assembly: Parallelization(Mode = ParallelMode.None)]
  ```

  In `backend/src/Infrastructure/Infrastructure.csproj`, add:

  ```xml
      <ItemGroup>
          <InternalsVisibleTo Include="AcceptanceTests" />
      </ItemGroup>
  ```

  Add `<Project Path="backend/tests/AcceptanceTests/AcceptanceTests.csproj" />` to the `/backend/tests/` folder of
  `N2JSoft.slnx`.

- [ ] **Step 3: Write the feature**

  `backend/tests/AcceptanceTests/Features/Users.feature`:

  ```gherkin
  Feature: List users

    Scenario: Listing returns every user sorted by last name then first name
      Given the following users exist
        | First name | Last name |
        | Juste      | Leblanc   |
        | Marc       | Assin     |
        | Albert     | Leblanc   |
      When I list the users
      Then the response status is 200
      And the users listed are
        | Name           |
        | Marc Assin     |
        | Albert Leblanc |
        | Juste Leblanc  |

    Scenario: Listing returns no user when none exists
      When I list the users
      Then the response status is 200
      And no user is listed
  ```

  The second scenario also proves that the dev seed, which runs when the hooks migrate the database, is wiped
  before each scenario (Review Focus 2).

- [ ] **Step 4: Run the feature to verify it fails**

  Run: `dotnet test --project backend/tests/AcceptanceTests`
  Expected: FAIL. Both scenarios report undefined steps ("No matching step definition found").

- [ ] **Step 5: Write the test environment**

  `backend/tests/AcceptanceTests/Support/ApiFactory.cs`:

  ```csharp
  using Microsoft.AspNetCore.Hosting;
  using Microsoft.AspNetCore.Mvc.Testing;

  namespace AcceptanceTests.Support;

  public sealed class ApiFactory(string connectionString) : WebApplicationFactory<Program>
  {
      protected override void ConfigureWebHost(IWebHostBuilder builder)
      {
          // "Testing" skips the Development-only startup migration: the hooks migrate the container themselves.
          builder
              .UseEnvironment("Testing")
              .UseSetting("ConnectionStrings:database", connectionString);
      }
  }
  ```

  `backend/tests/AcceptanceTests/Support/TestEnvironment.cs`:

  ```csharp
  using Infrastructure;
  using Microsoft.AspNetCore.Mvc.Testing;
  using Npgsql;
  using Respawn;
  using Testcontainers.PostgreSql;

  namespace AcceptanceTests.Support;

  public static class TestEnvironment
  {
      // Same Postgres major as the one Aspire runs (Aspire 13.6.1 default image).
      private const string PostgresImage = "postgres:18.3";

      private static PostgreSqlContainer? _postgres;
      private static ApiFactory? _factory;
      private static Respawner? _respawner;

      public static WebApplicationFactory<Program> Factory =>
          _factory ?? throw new InvalidOperationException("The test environment is not started.");

      private static string ConnectionString =>
          _postgres?.GetConnectionString() ?? throw new InvalidOperationException("Postgres is not started.");

      public static async Task StartAsync()
      {
          _postgres = new PostgreSqlBuilder(PostgresImage).Build();
          await _postgres.StartAsync();

          _factory = new ApiFactory(ConnectionString);
          // Migrating also runs the dev seed; ResetDatabaseAsync wipes it before each scenario.
          await _factory.Services.MigrateDatabaseAsync();

          await using var connection = new NpgsqlConnection(ConnectionString);
          await connection.OpenAsync();
          _respawner = await Respawner.CreateAsync(connection, new RespawnerOptions
          {
              DbAdapter = DbAdapter.Postgres,
              SchemasToInclude = ["public"],
              TablesToIgnore = ["__EFMigrationsHistory"],
          });
      }

      public static async Task ResetDatabaseAsync()
      {
          if (_respawner is null)
          {
              throw new InvalidOperationException("The test environment is not started.");
          }

          await using var connection = new NpgsqlConnection(ConnectionString);
          await connection.OpenAsync();
          await _respawner.ResetAsync(connection);
      }

      public static async Task StopAsync()
      {
          if (_factory is not null)
          {
              await _factory.DisposeAsync();
          }

          if (_postgres is not null)
          {
              await _postgres.DisposeAsync();
          }
      }
  }
  ```

  `backend/tests/AcceptanceTests/Support/ApiDriver.cs`:

  ```csharp
  namespace AcceptanceTests.Support;

  // One instance per scenario (Reqnroll context injection), disposed at the end of the scenario.
  public sealed class ApiDriver : IDisposable
  {
      private readonly HttpClient _client = TestEnvironment.Factory.CreateClient();
      private HttpResponseMessage? _response;

      public HttpResponseMessage Response =>
          _response ?? throw new InvalidOperationException("No request has been sent yet.");

      public async Task GetAsync(string path)
      {
          _response?.Dispose();
          _response = await _client.GetAsync(new Uri(path, UriKind.Relative));
      }

      public void Dispose()
      {
          _response?.Dispose();
          _client.Dispose();
      }
  }
  ```

  `backend/tests/AcceptanceTests/Hooks/TestRunHooks.cs`:

  ```csharp
  using AcceptanceTests.Support;
  using Reqnroll;

  namespace AcceptanceTests.Hooks;

  [Binding]
  public static class TestRunHooks
  {
      [BeforeTestRun]
      public static Task StartTestEnvironmentAsync()
      {
          return TestEnvironment.StartAsync();
      }

      [BeforeScenario]
      public static Task ResetDatabaseAsync()
      {
          return TestEnvironment.ResetDatabaseAsync();
      }

      [AfterTestRun]
      public static Task StopTestEnvironmentAsync()
      {
          return TestEnvironment.StopAsync();
      }
  }
  ```

- [ ] **Step 6: Write the step definitions**

  `backend/tests/AcceptanceTests/StepDefinitions/HttpSteps.cs`:

  ```csharp
  using AcceptanceTests.Support;
  using Reqnroll;

  namespace AcceptanceTests.StepDefinitions;

  [Binding]
  public sealed class HttpSteps(ApiDriver api)
  {
      [Then("the response status is {int}")]
      public void ThenTheResponseStatusIs(int status)
      {
          ((int)api.Response.StatusCode).ShouldBe(status);
      }
  }
  ```

  `backend/tests/AcceptanceTests/StepDefinitions/UserSteps.cs`:

  ```csharp
  using System.Net.Http.Json;
  using AcceptanceTests.Support;
  using Application.Users.Queries;
  using Domain.Entities;
  using Infrastructure.Persistence;
  using Microsoft.Extensions.DependencyInjection;
  using Reqnroll;

  namespace AcceptanceTests.StepDefinitions;

  [Binding]
  public sealed class UserSteps(ApiDriver api)
  {
      private const string UsersPath = "/users";

      [Given("the following users exist")]
      public async Task GivenTheFollowingUsersExistAsync(DataTable table)
      {
          using var scope = TestEnvironment.Factory.Services.CreateScope();
          var dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();
          dbContext.AddRange(table.Rows.Select(row => new User
          {
              FirstName = row["First name"],
              LastName = row["Last name"],
          }));
          await dbContext.SaveChangesAsync();
      }

      [When("I list the users")]
      public Task WhenIListTheUsersAsync()
      {
          return api.GetAsync(UsersPath);
      }

      [Then("the users listed are")]
      public async Task ThenTheUsersListedAreAsync(DataTable table)
      {
          var users = await ReadUsersAsync();
          users.Select(user => user.Name).ShouldBe(table.Rows.Select(row => row["Name"]));
      }

      [Then("no user is listed")]
      public async Task ThenNoUserIsListedAsync()
      {
          var users = await ReadUsersAsync();
          users.ShouldBeEmpty();
      }

      private async Task<List<UserSummary>> ReadUsersAsync()
      {
          return (await api.Response.Content.ReadFromJsonAsync<List<UserSummary>>()).ShouldNotBeNull();
      }
  }
  ```

- [ ] **Step 7: Run the feature to verify the ordering scenario fails**

  Run: `dotnet test --project backend/tests/AcceptanceTests` (Docker must be running)
  Expected: FAIL on "Listing returns every user sorted by last name then first name". The names come back in
  insertion order, not sorted. "Listing returns no user when none exists" passes.

- [ ] **Step 8: Sort the listing**

  In `backend/src/Infrastructure/Users/UserQueries.cs`, insert the ordering between `AsNoTracking()` and `Select`:

  ```csharp
          return await dbContext.Set<User>()
              .AsNoTracking()
              .OrderBy(user => user.LastName)
              .ThenBy(user => user.FirstName)
              .Select(user => new UserSummary(user.Id, $"{user.FirstName} {user.LastName}"))
              .ToListAsync(cancellationToken)
              .ConfigureAwait(false);
  ```

- [ ] **Step 9: Run the feature to verify it passes**

  Run: `dotnet test --project backend/tests/AcceptanceTests`
  Expected: PASS, 2 tests.

- [ ] **Step 10: Format and commit**

  ```bash
  dotnet jb cleanupcode N2JSoft.slnx --profile="ReformatAndReorder" --verbosity=ERROR
  git add -A backend/tests/AcceptanceTests backend/src/Infrastructure Directory.Packages.props N2JSoft.slnx
  git commit -m "test(backend): add Reqnroll acceptance tier and sort the users listing"
  ```

---

### Task 3: Error contract (ProblemDetails, exception handler, validation)

**Files:**

- Modify: `.editorconfig`, `Directory.Packages.props`, `backend/src/WebApi/WebApi.csproj`, `backend/src/WebApi/Program.cs`,
  `backend/tests/UnitTests/UnitTests.csproj`, `backend/tests/AcceptanceTests/StepDefinitions/HttpSteps.cs`
- Create: `backend/src/Domain/Exceptions/BusinessRuleViolationException.cs`,
  `backend/src/Application/Exceptions/NotFoundException.cs`, `backend/src/WebApi/Errors/ApiExceptionHandler.cs`,
  `backend/src/WebApi/Errors/ErrorHandlingExtensions.cs`
- Test: `backend/tests/UnitTests/Errors/ApiExceptionHandlerTests.cs`,
  `backend/tests/UnitTests/Errors/ErrorHandlingExtensionsTests.cs`,
  `backend/tests/UnitTests/Errors/ProblemDetailsTestContext.cs`,
  `backend/tests/UnitTests/Errors/TestRuleViolationException.cs`,
  `backend/tests/AcceptanceTests/Features/ErrorResponses.feature`

**Interfaces:**

- Consumes: `ApiDriver`, `HttpSteps` (Task 2).
- Produces (later slices throw these):
  - `Domain.Exceptions.BusinessRuleViolationException`:
    `public abstract class BusinessRuleViolationException(string code, string message) : Exception(message)`,
    with `string Code { get; }`.
  - `Application.Exceptions.NotFoundException`:
    `public sealed class NotFoundException(string resource, object key) : Exception`. The message is
    `"{resource} '{key}' was not found."`.
  - `WebApi.Errors.ErrorHandlingExtensions.AddErrorHandling(this IServiceCollection services, IHostEnvironment environment)`.
  - Acceptance steps `When I request {string}` and `Then the response is a problem with status {int}`.

- [ ] **Step 1: Write the failing handler tests**

  In `.editorconfig`, `# Dotnet Diagnostic Rules` list, add:

  ```ini
  dotnet_diagnostic.CA1032.severity = none # Implement standard exception constructors — our exceptions require their context (resource, rule code); parameterless constructors would allow meaningless instances
  ```

  and in the `# Roslynator` block add:

  ```ini
  dotnet_diagnostic.RCS1194.severity = none # Implement exception constructors — same reason as CA1032
  ```

  In `Directory.Packages.props`, `<!-- Testing -->` group, add (alphabetical order):

  ```xml
      <PackageVersion Include="NSubstitute" Version="6.2.0" />
      <PackageVersion Include="NSubstitute.Analyzers.CSharp" Version="1.0.17" />
  ```

  In `backend/tests/UnitTests/UnitTests.csproj`, add before the project references:

  ```xml
      <ItemGroup>
          <PackageReference Include="NSubstitute" />
          <PackageReference Include="NSubstitute.Analyzers.CSharp">
              <PrivateAssets>all</PrivateAssets>
              <IncludeAssets>runtime; build; native; contentfiles; analyzers; buildtransitive</IncludeAssets>
          </PackageReference>
      </ItemGroup>
  ```

  In `backend/src/WebApi/WebApi.csproj`, add:

  ```xml
      <ItemGroup>
          <InternalsVisibleTo Include="UnitTests" />
      </ItemGroup>
  ```

  `backend/tests/UnitTests/Errors/ProblemDetailsTestContext.cs`:

  ```csharp
  using System.Text.Json;
  using Microsoft.AspNetCore.Http;

  namespace UnitTests.Errors;

  internal static class ProblemDetailsTestContext
  {
      public static DefaultHttpContext Create(IServiceProvider services)
      {
          return new DefaultHttpContext
          {
              RequestServices = services,
              Response = { Body = new MemoryStream() },
          };
      }

      public static async Task<JsonElement> ReadBodyAsync(HttpContext context)
      {
          context.Response.Body.Position = 0;
          using var document = await JsonDocument.ParseAsync(
              context.Response.Body,
              cancellationToken: TestContext.Current.CancellationToken);
          return document.RootElement.Clone();
      }
  }
  ```

  `backend/tests/UnitTests/Errors/TestRuleViolationException.cs`:

  ```csharp
  using Domain.Exceptions;

  namespace UnitTests.Errors;

  // Nothing throws a business rule violation before slice 2: this stands in for the concrete rules.
  public sealed class TestRuleViolationException()
      : BusinessRuleViolationException("test.rule", "The test rule was violated.");
  ```

  `backend/tests/UnitTests/Errors/ApiExceptionHandlerTests.cs`:

  ```csharp
  using Application.Exceptions;
  using Microsoft.AspNetCore.Http;
  using Microsoft.Extensions.DependencyInjection;
  using WebApi.Errors;

  namespace UnitTests.Errors;

  public sealed class ApiExceptionHandlerTests : IDisposable
  {
      // AddOptions() is required: AddProblemDetails() alone does not register IOptions<>.
      private readonly ServiceProvider _services = new ServiceCollection()
          .AddOptions()
          .AddProblemDetails()
          .BuildServiceProvider();

      public void Dispose()
      {
          _services.Dispose();
      }

      [Fact]
      public async Task NotFoundException_is_written_as_a_404_problem_naming_the_resource()
      {
          var context = ProblemDetailsTestContext.Create(_services);

          var handled = await CreateHandler().TryHandleAsync(
              context,
              new NotFoundException("User", 42),
              TestContext.Current.CancellationToken);

          handled.ShouldBeTrue();
          context.Response.StatusCode.ShouldBe(StatusCodes.Status404NotFound);
          context.Response.ContentType.ShouldBe("application/problem+json");
          var body = await ProblemDetailsTestContext.ReadBodyAsync(context);
          body.GetProperty("status").GetInt32().ShouldBe(404);
          body.GetProperty("detail").GetString().ShouldBe("User '42' was not found.");
      }

      [Fact]
      public async Task BusinessRuleViolationException_is_written_as_a_422_problem_with_its_code()
      {
          var context = ProblemDetailsTestContext.Create(_services);

          var handled = await CreateHandler().TryHandleAsync(
              context,
              new TestRuleViolationException(),
              TestContext.Current.CancellationToken);

          handled.ShouldBeTrue();
          context.Response.StatusCode.ShouldBe(StatusCodes.Status422UnprocessableEntity);
          var body = await ProblemDetailsTestContext.ReadBodyAsync(context);
          body.GetProperty("status").GetInt32().ShouldBe(422);
          body.GetProperty("code").GetString().ShouldBe("test.rule");
          body.GetProperty("detail").GetString().ShouldBe("The test rule was violated.");
      }

      [Fact]
      public async Task BadHttpRequestException_is_written_with_its_own_status_code()
      {
          var context = ProblemDetailsTestContext.Create(_services);

          var handled = await CreateHandler().TryHandleAsync(
              context,
              new BadHttpRequestException("Failed to read parameter.", StatusCodes.Status400BadRequest),
              TestContext.Current.CancellationToken);

          handled.ShouldBeTrue();
          context.Response.StatusCode.ShouldBe(StatusCodes.Status400BadRequest);
          var body = await ProblemDetailsTestContext.ReadBodyAsync(context);
          body.GetProperty("status").GetInt32().ShouldBe(400);
      }

      [Fact]
      public async Task Any_other_exception_is_left_to_the_default_500_handling()
      {
          var context = ProblemDetailsTestContext.Create(_services);

          var handled = await CreateHandler().TryHandleAsync(
              context,
              new InvalidOperationException("boom"),
              TestContext.Current.CancellationToken);

          handled.ShouldBeFalse();
          context.Response.Body.Length.ShouldBe(0);
      }

      private ApiExceptionHandler CreateHandler()
      {
          return new ApiExceptionHandler(_services.GetRequiredService<IProblemDetailsService>());
      }
  }
  ```

  `backend/tests/UnitTests/Errors/ErrorHandlingExtensionsTests.cs`:

  ```csharp
  using Microsoft.AspNetCore.Http;
  using Microsoft.Extensions.DependencyInjection;
  using Microsoft.Extensions.Hosting;
  using NSubstitute;
  using WebApi.Errors;

  namespace UnitTests.Errors;

  public sealed class ErrorHandlingExtensionsTests
  {
      [Fact]
      public async Task Unhandled_exception_detail_is_exposed_in_Development()
      {
          var body = await WriteUnhandledExceptionProblemAsync(Environments.Development);

          body.GetProperty("detail").GetString().ShouldNotBeNull().ShouldContain("Database unreachable");
      }

      [Fact]
      public async Task Unhandled_exception_detail_is_hidden_outside_Development()
      {
          var body = await WriteUnhandledExceptionProblemAsync(Environments.Production);

          body.TryGetProperty("detail", out _).ShouldBeFalse();
          body.ToString().ShouldNotContain("Database unreachable");
      }

      // Mirrors what the exception handler middleware writes when no IExceptionHandler handles the exception.
      private static async Task<System.Text.Json.JsonElement> WriteUnhandledExceptionProblemAsync(string environmentName)
      {
          var environment = Substitute.For<IHostEnvironment>();
          environment.EnvironmentName.Returns(environmentName);
          await using var services = new ServiceCollection()
              .AddOptions()
              .AddErrorHandling(environment)
              .BuildServiceProvider();
          var context = ProblemDetailsTestContext.Create(services);
          context.Response.StatusCode = StatusCodes.Status500InternalServerError;

          var written = await services.GetRequiredService<IProblemDetailsService>().TryWriteAsync(new ProblemDetailsContext
          {
              HttpContext = context,
              ProblemDetails = { Status = StatusCodes.Status500InternalServerError },
              Exception = new InvalidOperationException("Database unreachable"),
          });

          written.ShouldBeTrue();
          return await ProblemDetailsTestContext.ReadBodyAsync(context);
      }
  }
  ```

- [ ] **Step 2: Run the tests to verify they fail**

  Run: `dotnet test --project backend/tests/UnitTests`
  Expected: FAIL. `CS0246`/`CS0234` errors for `Application.Exceptions`, `Domain.Exceptions` and
  `WebApi.Errors`.

- [ ] **Step 3: Implement the exceptions, the handler and the registration**

  `backend/src/Domain/Exceptions/BusinessRuleViolationException.cs`:

  ```csharp
  namespace Domain.Exceptions;

  // Base for every business rule broken by a domain operation; mapped to 422 with Code as a stable identifier
  // the frontend translates (e.g. "expense.quota_exceeded").
  public abstract class BusinessRuleViolationException(string code, string message) : Exception(message)
  {
      public string Code { get; } = code;
  }
  ```

  `backend/src/Application/Exceptions/NotFoundException.cs`:

  ```csharp
  namespace Application.Exceptions;

  public sealed class NotFoundException(string resource, object key)
      : Exception($"{resource} '{key}' was not found.");
  ```

  `backend/src/WebApi/Errors/ApiExceptionHandler.cs`:

  ```csharp
  using Application.Exceptions;
  using Domain.Exceptions;
  using Microsoft.AspNetCore.Diagnostics;
  using Microsoft.AspNetCore.Mvc;

  namespace WebApi.Errors;

  internal sealed class ApiExceptionHandler(IProblemDetailsService problemDetailsService) : IExceptionHandler
  {
      public async ValueTask<bool> TryHandleAsync(
          HttpContext httpContext,
          Exception exception,
          CancellationToken cancellationToken)
      {
          var problemDetails = exception switch
          {
              NotFoundException notFound => new ProblemDetails
              {
                  Status = StatusCodes.Status404NotFound,
                  Title = "Resource not found",
                  Detail = notFound.Message,
              },
              BusinessRuleViolationException violation => new ProblemDetails
              {
                  Status = StatusCodes.Status422UnprocessableEntity,
                  Title = "Business rule violation",
                  Detail = violation.Message,
                  Extensions = { ["code"] = violation.Code },
              },
              // Malformed requests; Development throws them (ThrowOnBadRequest) instead of answering 400.
              BadHttpRequestException badRequest => new ProblemDetails
              {
                  Status = badRequest.StatusCode,
              },
              _ => null,
          };

          if (problemDetails is null)
          {
              return false;
          }

          httpContext.Response.StatusCode = problemDetails.Status!.Value;

          return await problemDetailsService.TryWriteAsync(new ProblemDetailsContext
          {
              HttpContext = httpContext,
              ProblemDetails = problemDetails,
              Exception = exception,
          }).ConfigureAwait(false);
      }
  }
  ```

  `backend/src/WebApi/Errors/ErrorHandlingExtensions.cs`:

  ```csharp
  namespace WebApi.Errors;

  internal static class ErrorHandlingExtensions
  {
      public static IServiceCollection AddErrorHandling(this IServiceCollection services, IHostEnvironment environment)
      {
          services.AddProblemDetails(options =>
          {
              if (environment.IsDevelopment())
              {
                  // Unhandled exceptions become a bare 500: show what happened, but only to developers.
                  options.CustomizeProblemDetails = static context =>
                      context.ProblemDetails.Detail ??= context.Exception?.ToString();
              }
          });
          services.AddExceptionHandler<ApiExceptionHandler>();

          return services;
      }
  }
  ```

- [ ] **Step 4: Run the unit tests to verify they pass**

  Run: `dotnet test --project backend/tests/UnitTests`
  Expected: PASS, 15 tests (9 architecture + 4 handler + 2 registration).

- [ ] **Step 5: Write the failing acceptance scenario for the HTTP pipeline**

  `backend/tests/AcceptanceTests/Features/ErrorResponses.feature`:

  ```gherkin
  Feature: Error responses

    Scenario: An unknown route answers with a not found problem
      When I request "/does-not-exist"
      Then the response is a problem with status 404
  ```

  In `backend/tests/AcceptanceTests/StepDefinitions/HttpSteps.cs`, add `using System.Net.Http.Json;` and
  `using System.Text.Json;` at the top, and these two steps to the class:

  ```csharp
      [When("I request {string}")]
      public Task WhenIRequestAsync(string path)
      {
          return api.GetAsync(path);
      }

      [Then("the response is a problem with status {int}")]
      public async Task ThenTheResponseIsAProblemWithStatusAsync(int status)
      {
          ((int)api.Response.StatusCode).ShouldBe(status);
          api.Response.Content.Headers.ContentType?.MediaType.ShouldBe("application/problem+json");
          var problem = await api.Response.Content.ReadFromJsonAsync<JsonElement>();
          problem.GetProperty("status").GetInt32().ShouldBe(status);
      }
  ```

  Run: `dotnet test --project backend/tests/AcceptanceTests`
  Expected: FAIL on "An unknown route answers with a not found problem". The 404 has no content type and an
  empty body.

- [ ] **Step 6: Wire the error contract and validation into `Program`**

  In `backend/src/WebApi/Program.cs`:
  - add `using WebApi.Errors;`;
  - after `builder.AddInfrastructure();` add:

    ```csharp
    builder.Services.AddErrorHandling(builder.Environment);
    builder.Services.AddValidation();
    ```

  - before `app.UseHttpsRedirection();` add:

    ```csharp
    app.UseExceptionHandler();
    app.UseStatusCodePages();
    ```

  `AddValidation()` turns on .NET 10's built-in minimal-API validation (DataAnnotations → 400
  `ValidationProblemDetails`). No endpoint takes a body yet; slice 2's create-user request is the first one.
  The source generator **silently skips request types that are not declared `public`**. Task 8 records this in
  the roadmap conventions.

- [ ] **Step 7: Run both tiers to verify they pass**

  Run: `dotnet test --project backend/tests/AcceptanceTests`
  Expected: PASS, 3 tests.

  Run: `dotnet test --project backend/tests/UnitTests`
  Expected: PASS, 15 tests.

- [ ] **Step 8: Format and commit**

  ```bash
  dotnet jb cleanupcode N2JSoft.slnx --profile="ReformatAndReorder" --verbosity=ERROR
  git add -A backend .editorconfig Directory.Packages.props
  git commit -m "feat(backend): add the ProblemDetails error contract"
  ```

---

### Task 4: URL-segment API versioning, OpenAPI per version and Scalar

**Files:**

- Modify: `Directory.Packages.props`, `backend/src/WebApi/WebApi.csproj`, `backend/src/WebApi/Program.cs`,
  `backend/src/WebApi/Endpoints/UserEndpoints.cs`, `backend/src/WebApi/WebApi.http`,
  `backend/tests/AcceptanceTests/Support/TestEnvironment.cs`, `backend/tests/AcceptanceTests/Support/ApiDriver.cs`,
  `backend/tests/AcceptanceTests/StepDefinitions/UserSteps.cs`, `backend/tests/AcceptanceTests/StepDefinitions/HttpSteps.cs`
- Create: `backend/tests/AcceptanceTests/Features/ApiVersioning.feature`,
  `backend/tests/AcceptanceTests/Features/ApiDocumentation.feature`,
  `backend/tests/AcceptanceTests/StepDefinitions/ApiDocumentationSteps.cs`

**Interfaces:**

- Consumes: `TestEnvironment`, `ApiDriver`, `HttpSteps` (Tasks 2–3); `UseStatusCodePages` (Task 3).
- Produces:
  - Convention: each feature maps
    `endpoints.NewVersionedApi("<Feature>").MapGroup("/v{version:apiVersion}/<feature>").HasApiVersion(1.0)`.
  - `GET /v1/users`.
  - `/openapi/v1.json` and `/scalar/` in Development only.
  - `TestEnvironment.DevelopmentFactory` and `ApiDriver.UseDevelopmentEnvironment()`.

- [ ] **Step 1: Write the failing scenarios**

  In `backend/tests/AcceptanceTests/StepDefinitions/UserSteps.cs`, change the constant to:

  ```csharp
      private const string UsersPath = "/v1/users";
  ```

  `backend/tests/AcceptanceTests/Features/ApiVersioning.feature`:

  ```gherkin
  Feature: API versioning

    Scenario: Responses report the supported API versions
      When I request "/v1/users"
      Then the response status is 200
      And the response header "api-supported-versions" is "1.0"

    Scenario: An unsupported API version is not found
      When I request "/v2/users"
      Then the response is a problem with status 404

    Scenario: Unversioned API routes are not found
      When I request "/users"
      Then the response is a problem with status 404

    Scenario: The health endpoint stays unversioned
      When I request "/health"
      Then the response status is 200
  ```

  `backend/tests/AcceptanceTests/Features/ApiDocumentation.feature`:

  ```gherkin
  Feature: API documentation

    Scenario: The v1 OpenAPI document describes the users endpoint
      Given the API runs in the Development environment
      When I request "/openapi/v1.json"
      Then the response status is 200
      And the OpenAPI document lists the path "/v1/users"

    Scenario: The Scalar reference is served in Development
      Given the API runs in the Development environment
      When I request "/scalar/"
      Then the response status is 200

    Scenario: API documentation is not exposed outside Development
      When I request "/openapi/v1.json"
      Then the response is a problem with status 404
  ```

  In `HttpSteps.cs`, add:

  ```csharp
      [Then("the response header {string} is {string}")]
      public void ThenTheResponseHeaderIs(string name, string value)
      {
          api.Response.Headers.GetValues(name).ShouldHaveSingleItem().ShouldBe(value);
      }
  ```

  In `backend/tests/AcceptanceTests/Support/TestEnvironment.cs`, add the field and property, and dispose the
  factory first in `StopAsync`:

  ```csharp
      private static WebApplicationFactory<Program>? _developmentFactory;

      // Built on first use: only the API documentation scenarios need the Development pipeline. Its startup
      // migration is a no-op on the already migrated container; the seed it inserts is wiped before the next scenario.
      public static WebApplicationFactory<Program> DevelopmentFactory =>
          _developmentFactory ??= Factory.WithWebHostBuilder(builder => builder.UseEnvironment("Development"));
  ```

  ```csharp
      public static async Task StopAsync()
      {
          if (_developmentFactory is not null)
          {
              await _developmentFactory.DisposeAsync();
          }

          if (_factory is not null)
          {
              await _factory.DisposeAsync();
          }

          if (_postgres is not null)
          {
              await _postgres.DisposeAsync();
          }
      }
  ```

  Add `using Microsoft.AspNetCore.Hosting;` to `TestEnvironment.cs` (for `UseEnvironment`).

  In `backend/tests/AcceptanceTests/Support/ApiDriver.cs`, make the client replaceable. Replace the
  `private readonly HttpClient _client` field with:

  ```csharp
      private HttpClient _client = TestEnvironment.Factory.CreateClient();
  ```

  and add:

  ```csharp
      public void UseDevelopmentEnvironment()
      {
          _client.Dispose();
          _client = TestEnvironment.DevelopmentFactory.CreateClient();
      }
  ```

  `backend/tests/AcceptanceTests/StepDefinitions/ApiDocumentationSteps.cs`:

  ```csharp
  using System.Net.Http.Json;
  using System.Text.Json;
  using AcceptanceTests.Support;
  using Reqnroll;

  namespace AcceptanceTests.StepDefinitions;

  [Binding]
  public sealed class ApiDocumentationSteps(ApiDriver api)
  {
      [Given("the API runs in the Development environment")]
      public void GivenTheApiRunsInTheDevelopmentEnvironment()
      {
          api.UseDevelopmentEnvironment();
      }

      [Then("the OpenAPI document lists the path {string}")]
      public async Task ThenTheOpenApiDocumentListsThePathAsync(string path)
      {
          var document = await api.Response.Content.ReadFromJsonAsync<JsonElement>();
          document.GetProperty("paths").TryGetProperty(path, out _).ShouldBeTrue($"the document has no path {path}");
      }
  }
  ```

- [ ] **Step 2: Run the scenarios to verify they fail**

  Run: `dotnet test --project backend/tests/AcceptanceTests`
  Expected: FAIL on:
  - both "List users" scenarios (`/v1/users` is a 404);
  - "Responses report the supported API versions";
  - "Unversioned API routes are not found" (`/users` still answers 200);
  - "The v1 OpenAPI document describes the users endpoint" (no `/v1/users` path);
  - "The Scalar reference is served in Development".

  "The health endpoint stays unversioned", "An unsupported API version is not found" and "API documentation is
  not exposed outside Development" already pass.

- [ ] **Step 3: Add the versioning and documentation packages**

  In `Directory.Packages.props`, add after the "ASP.NET Core & EF Core" group:

  ```xml
    <ItemGroup>
      <!-- API versioning & documentation -->
      <PackageVersion Include="Asp.Versioning.Http" Version="10.2.3" />
      <PackageVersion Include="Asp.Versioning.Mvc.ApiExplorer" Version="10.2.1" />
      <PackageVersion Include="Asp.Versioning.OpenApi" Version="10.2.3" />
      <PackageVersion Include="Scalar.AspNetCore" Version="2.17.14" />
    </ItemGroup>
  ```

  In `backend/src/WebApi/WebApi.csproj`, replace the package `ItemGroup` with the following. The `Description`
  becomes the OpenAPI document's description (analyzer AV0025).

  ```xml
      <PropertyGroup>
          <Description>N2JSoft expense management API</Description>
      </PropertyGroup>

      <ItemGroup>
          <PackageReference Include="Asp.Versioning.Http" />
          <PackageReference Include="Asp.Versioning.Mvc.ApiExplorer" />
          <PackageReference Include="Asp.Versioning.OpenApi" />
          <PackageReference Include="Microsoft.AspNetCore.OpenApi" />
          <PackageReference Include="Microsoft.EntityFrameworkCore.Design">
              <PrivateAssets>all</PrivateAssets>
              <IncludeAssets>runtime; build; native; contentfiles; analyzers; buildtransitive</IncludeAssets>
          </PackageReference>
          <PackageReference Include="Scalar.AspNetCore" />
      </ItemGroup>
  ```

- [ ] **Step 4: Version the users endpoint**

  `backend/src/WebApi/Endpoints/UserEndpoints.cs`:

  ```csharp
  using Application.Users.Queries;
  using Asp.Versioning;
  using Microsoft.AspNetCore.Http.HttpResults;

  namespace WebApi.Endpoints;

  internal static class UserEndpoints
  {
      public static IEndpointRouteBuilder MapUserEndpoints(this IEndpointRouteBuilder endpoints)
      {
          var users = endpoints.NewVersionedApi("Users")
              .MapGroup("/v{version:apiVersion}/users")
              .HasApiVersion(1.0);

          users.MapGet("/", ListUsersAsync);

          return endpoints;
      }

      private static async Task<Ok<IReadOnlyList<UserSummary>>> ListUsersAsync(
          IUserQueries userQueries,
          CancellationToken cancellationToken)
      {
          var users = await userQueries.ListAsync(cancellationToken).ConfigureAwait(false);
          return TypedResults.Ok(users);
      }
  }
  ```

- [ ] **Step 5: Configure versioning, OpenAPI and Scalar in `Program`**

  `backend/src/WebApi/Program.cs` becomes:

  ```csharp
  using Asp.Versioning;
  using Infrastructure;
  using Scalar.AspNetCore;
  using WebApi.Endpoints;
  using WebApi.Errors;

  var builder = WebApplication.CreateBuilder(args);

  builder.AddInfrastructure();
  builder.Services.AddErrorHandling(builder.Environment);
  builder.Services.AddValidation();
  builder.Services.AddHealthChecks();
  // 1.0 is the default API version. This AddOpenApi() replaces services.AddOpenApi(): one document per version.
  builder.Services
      .AddApiVersioning(options =>
      {
          options.ReportApiVersions = true;
          options.ApiVersionReader = new UrlSegmentApiVersionReader();
      })
      .AddApiExplorer(options =>
      {
          options.GroupNameFormat = "'v'VVV";
          options.SubstituteApiVersionInUrl = true;
      })
      .AddOpenApi();

  var app = builder.Build();

  if (app.Environment.IsDevelopment())
  {
      try
      {
          await app.Services.MigrateDatabaseAsync().ConfigureAwait(false);
      }
      catch (Exception e)
      {
          app.Logger.LogError(e, "An error occurred while migrating the database.");
          return;
      }

      app.MapOpenApi().WithDocumentPerVersion();
      app.MapScalarApiReference(options =>
      {
          foreach (var description in app.DescribeApiVersions())
          {
              options.AddDocument(description.GroupName);
          }
      });
  }

  app.UseExceptionHandler();
  app.UseStatusCodePages();
  app.UseHttpsRedirection();

  app.MapUserEndpoints();
  app.MapHealthChecks("/health");

  await app.RunAsync().ConfigureAwait(false);
  ```

  Replace `backend/src/WebApi/WebApi.http` with:

  ```http
  @WebApi_HostAddress = http://localhost:5195

  ### List users
  GET {{WebApi_HostAddress}}/v1/users
  Accept: application/json
  ```

- [ ] **Step 6: Run both tiers to verify they pass**

  Run: `dotnet test --project backend/tests/AcceptanceTests`
  Expected: PASS, 10 tests (2 users + 1 error + 4 versioning + 3 documentation).

  Run: `dotnet test --project backend/tests/UnitTests`
  Expected: PASS, 15 tests.

- [ ] **Step 7: Format and commit**

  ```bash
  dotnet jb cleanupcode N2JSoft.slnx --profile="ReformatAndReorder" --verbosity=ERROR
  git add -A backend Directory.Packages.props
  git commit -m "feat(backend): version the API by URL segment and document it with OpenAPI and Scalar"
  ```

---

### Task 5: French frontend shell with the users page

All the code below was run through `nx run-many -t lint test build` during planning: 6 specs pass, lint is
clean, and the production build succeeds.

**Files:**

- Delete: `frontend/src/app/nx-welcome.ts`
- Modify: `frontend/src/app/app.ts`, `app.html`, `app.routes.ts`, `app.config.ts`, `app.spec.ts`,
  `frontend/src/index.html`
- Create: `frontend/src/app/core/.gitkeep`, `frontend/src/app/features/users/user-summary.ts`, `users.api.ts`,
  `user-list.page.ts`
- Test: `frontend/src/app/app.config.spec.ts`, `frontend/src/app/features/users/users.api.spec.ts`,
  `frontend/src/app/features/users/user-list.page.spec.ts`

**Interfaces:**

- Consumes: `GET /api/v1/users` → `[{ id: string, name: string }]` (Task 4 through the dev proxy, which strips
  `/api`).
- Produces (Task 6's journey relies on these):
  - Route `''` redirects to `users`.
  - The page has the heading "Utilisateurs" and one `<li>` per user name.
  - The nav has a link "Utilisateurs" to `/users`.
  - `LOCALE_ID` is `fr-FR` with French locale data registered.

- [ ] **Step 1: Write the failing specs**

  `frontend/src/app/app.config.spec.ts`:

  ```ts
  import { DatePipe } from '@angular/common';
  import { TestBed } from '@angular/core/testing';
  import { appConfig } from './app.config';

  describe('appConfig', () => {
    it('formats dates in French', () => {
      TestBed.configureTestingModule({
        providers: [...appConfig.providers, DatePipe],
      });

      const date = TestBed.inject(DatePipe).transform('2025-10-15', 'fullDate');

      expect(date).toBe('mercredi 15 octobre 2025');
    });
  });
  ```

  `frontend/src/app/features/users/users.api.spec.ts`:

  ```ts
  import { provideHttpClient } from '@angular/common/http';
  import {
    HttpTestingController,
    provideHttpClientTesting,
  } from '@angular/common/http/testing';
  import { TestBed } from '@angular/core/testing';
  import { UserSummary } from './user-summary';
  import { UsersApi } from './users.api';

  describe('UsersApi', () => {
    let api: UsersApi;
    let http: HttpTestingController;

    beforeEach(() => {
      TestBed.configureTestingModule({
        providers: [provideHttpClient(), provideHttpClientTesting()],
      });
      api = TestBed.inject(UsersApi);
      http = TestBed.inject(HttpTestingController);
    });

    afterEach(() => http.verify());

    it('lists users with GET /api/v1/users', () => {
      const users: UserSummary[] = [
        { id: '7d9f5f3c-2f1e-4a43-9a43-4d1f0c6a1b2e', name: 'Juste Leblanc' },
      ];
      let result: UserSummary[] | undefined;

      api.list().subscribe((value) => (result = value));

      const request = http.expectOne('/api/v1/users');
      expect(request.request.method).toBe('GET');
      request.flush(users);
      expect(result).toEqual(users);
    });
  });
  ```

  `frontend/src/app/features/users/user-list.page.spec.ts`:

  ```ts
  import { TestBed } from '@angular/core/testing';
  import { Observable, of } from 'rxjs';
  import { UserSummary } from './user-summary';
  import UserListPage from './user-list.page';
  import { UsersApi } from './users.api';

  function render(users: Observable<UserSummary[]>): HTMLElement {
    TestBed.configureTestingModule({
      imports: [UserListPage],
      providers: [{ provide: UsersApi, useValue: { list: () => users } }],
    });
    const fixture = TestBed.createComponent(UserListPage);
    fixture.detectChanges();
    return fixture.nativeElement as HTMLElement;
  }

  describe('UserListPage', () => {
    it('renders the name of every user returned by the API', () => {
      const page = render(
        of([
          { id: '1', name: 'Juste Leblanc' },
          { id: '2', name: 'Marc Assin' },
        ])
      );

      const names = Array.from(page.querySelectorAll('li')).map((item) =>
        item.textContent?.trim()
      );
      expect(names).toEqual(['Juste Leblanc', 'Marc Assin']);
    });

    it('says so when there is no user', () => {
      const page = render(of([]));

      expect(page.querySelector('ul')).toBeNull();
      expect(page.textContent).toContain('Aucun utilisateur.');
    });

    it('shows a loading message until the API answers', () => {
      const page = render(new Observable<UserSummary[]>());

      expect(page.textContent).toContain('Chargement…');
    });
  });
  ```

  Replace `frontend/src/app/app.spec.ts` with:

  ```ts
  import { TestBed } from '@angular/core/testing';
  import { provideRouter } from '@angular/router';
  import { App } from './app';

  describe('App', () => {
    beforeEach(async () => {
      await TestBed.configureTestingModule({
        imports: [App],
        providers: [provideRouter([])],
      }).compileComponents();
    });

    it('links to the users page from the navigation', () => {
      const fixture = TestBed.createComponent(App);
      fixture.detectChanges();
      const link = (fixture.nativeElement as HTMLElement).querySelector('nav a');

      expect(link?.textContent?.trim()).toBe('Utilisateurs');
      expect(link?.getAttribute('href')).toBe('/users');
    });
  });
  ```

- [ ] **Step 2: Run the specs to verify they fail**

  Run: `cd frontend && npx nx test n2f --skip-nx-cache; cd ..`
  Expected: FAIL.
  - `users.api.spec.ts` and `user-list.page.spec.ts` cannot resolve `./users.api` / `./user-list.page`.
  - `app.config.spec.ts` fails with a missing `fr-FR` locale data error.
  - `app.spec.ts` finds no `nav a`.

- [ ] **Step 3: Implement the users feature**

  `frontend/src/app/features/users/user-summary.ts`:

  ```ts
  export interface UserSummary {
    id: string;
    name: string;
  }
  ```

  `frontend/src/app/features/users/users.api.ts`:

  ```ts
  import { HttpClient } from '@angular/common/http';
  import { inject, Injectable } from '@angular/core';
  import { Observable } from 'rxjs';
  import { UserSummary } from './user-summary';

  @Injectable({ providedIn: 'root' })
  export class UsersApi {
    private readonly http = inject(HttpClient);

    list(): Observable<UserSummary[]> {
      return this.http.get<UserSummary[]>('/api/v1/users');
    }
  }
  ```

  `frontend/src/app/features/users/user-list.page.ts`:

  ```ts
  import { Component, inject } from '@angular/core';
  import { toSignal } from '@angular/core/rxjs-interop';
  import { UsersApi } from './users.api';

  @Component({
    selector: 'app-user-list-page',
    template: `
      <h1 class="mb-4 text-2xl font-semibold">Utilisateurs</h1>
      @if (users(); as users) {
        @if (users.length > 0) {
          <ul>
            @for (user of users; track user.id) {
              <li>{{ user.name }}</li>
            }
          </ul>
        } @else {
          <p>Aucun utilisateur.</p>
        }
      } @else {
        <p>Chargement…</p>
      }
    `,
  })
  export default class UserListPage {
    protected readonly users = toSignal(inject(UsersApi).list());
  }
  ```

  Create the empty file `frontend/src/app/core/.gitkeep`. The folder holds shared code from later slices.

- [ ] **Step 4: Replace the shell, routes and locale**

  Delete `frontend/src/app/nx-welcome.ts`.

  `frontend/src/app/app.ts`:

  ```ts
  import { Component } from '@angular/core';
  import { RouterLink, RouterLinkActive, RouterOutlet } from '@angular/router';

  @Component({
    imports: [RouterLink, RouterLinkActive, RouterOutlet],
    selector: 'app-root',
    templateUrl: './app.html',
    styleUrl: './app.css',
  })
  export class App {}
  ```

  `frontend/src/app/app.html`:

  ```html
  <header class="border-b border-gray-200 px-6 py-4">
    <nav class="flex items-center gap-6">
      <span class="font-semibold">N2F</span>
      <a routerLink="/users" routerLinkActive="underline">Utilisateurs</a>
    </nav>
  </header>
  <main class="p-6">
    <router-outlet />
  </main>
  ```

  `frontend/src/app/app.routes.ts`:

  ```ts
  import { Route } from '@angular/router';

  export const appRoutes: Route[] = [
    { path: '', pathMatch: 'full', redirectTo: 'users' },
    {
      path: 'users',
      title: 'Utilisateurs',
      loadComponent: () => import('./features/users/user-list.page'),
    },
  ];
  ```

  `frontend/src/app/app.config.ts`:

  ```ts
  import { registerLocaleData } from '@angular/common';
  import {
    provideHttpClient,
    withInterceptorsFromDi,
    withXhr,
  } from '@angular/common/http';
  import localeFr from '@angular/common/locales/fr';
  import {
    ApplicationConfig,
    LOCALE_ID,
    provideBrowserGlobalErrorListeners,
    provideZoneChangeDetection,
  } from '@angular/core';
  import { provideRouter } from '@angular/router';
  import { appRoutes } from './app.routes';

  registerLocaleData(localeFr);

  export const appConfig: ApplicationConfig = {
    providers: [
      provideBrowserGlobalErrorListeners(),
      provideZoneChangeDetection({ eventCoalescing: true }),
      provideRouter(appRoutes),
      provideHttpClient(withXhr(), withInterceptorsFromDi()),
      { provide: LOCALE_ID, useValue: 'fr-FR' },
    ],
  };
  ```

  In `frontend/src/index.html`, change `<html lang="en">` to `<html lang="fr">`.

- [ ] **Step 5: Run lint, tests and build to verify they pass**

  Run: `cd frontend && npx nx run-many -t lint test build --skip-nx-cache; cd ..`
  Expected:
  - `All files pass linting`;
  - `Test Files 4 passed (4)`, `Tests 6 passed (6)`;
  - the production build succeeds with a lazy `user-list-page` chunk.

- [ ] **Step 6: Commit**

  ```bash
  git add -A frontend/src
  git commit -m "feat(frontend): replace the Nx welcome page with a French users shell"
  ```

---

### Task 6: End-to-end tier (Aspire.Hosting.Testing + Playwright) and the dashboard Scalar link

**Files:**

- Modify: `Directory.Packages.props`, `N2JSoft.slnx`, `tools/N2JSoft.Aspire/AppHost.cs`
- Create (under `backend/tests/EndToEndTests/`): `EndToEndTests.csproj`, `AssemblyInfo.cs`,
  `Support/AppFixture.cs`, `Support/PageTest.cs`, `UsersPageTests.cs`, `AspireDashboardTests.cs`

**Interfaces:**

- Consumes:
  - the AppHost (`Projects.N2JSoft_Aspire`), with resources `webapi` (endpoints `http`, `https`) and `angular`
    (endpoint `http`);
  - the users page from Task 5 and its dev seed (Juste Leblanc, Marc Assin);
  - `/scalar` from Task 4.
- Produces:
  - `EndToEndTests.Support.AppFixture` (assembly fixture): `DistributedApplication App`, `IBrowser Browser`,
    `Uri AngularUrl`.
  - `EndToEndTests.Support.PageTest` (base class): `AppFixture Fixture`, `IPage Page`. It gives each test a fresh
    browser context and writes a trace and screenshot to `<bin>/playwright-artifacts/` when the test fails.

- [ ] **Step 1: Write the failing users journey**

  In `Directory.Packages.props`:
  - add `<PackageVersion Include="Aspire.Hosting.Testing" Version="13.6.1" />` to the `<!-- Aspire -->` group;
  - add `<PackageVersion Include="Microsoft.Playwright" Version="1.63.0" />` to the `<!-- Testing -->` group.

  `backend/tests/EndToEndTests/EndToEndTests.csproj`:

  ```xml
  <Project Sdk="Microsoft.NET.Sdk">

      <ItemGroup>
          <PackageReference Include="Aspire.Hosting.Testing" />
          <PackageReference Include="Microsoft.Playwright" />
      </ItemGroup>

      <ItemGroup>
          <ProjectReference Include="..\..\..\tools\N2JSoft.Aspire\N2JSoft.Aspire.csproj" />
      </ItemGroup>

  </Project>
  ```

  Add `<Project Path="backend/tests/EndToEndTests/EndToEndTests.csproj" />` to the `/backend/tests/` folder of
  `N2JSoft.slnx`.

  `backend/tests/EndToEndTests/UsersPageTests.cs`:

  ```csharp
  using EndToEndTests.Support;
  using Microsoft.Playwright;

  namespace EndToEndTests;

  public sealed class UsersPageTests(AppFixture fixture) : PageTest(fixture)
  {
      [Fact]
      public async Task Opening_the_app_shows_the_seeded_users()
      {
          await Page.GotoAsync("/");

          await Assertions.Expect(Page).ToHaveURLAsync(new Uri(Fixture.AngularUrl, "users").ToString());
          await Assertions.Expect(Page.GetByRole(AriaRole.Heading, new() { Name = "Utilisateurs" })).ToBeVisibleAsync();
          var users = Page.GetByRole(AriaRole.Listitem);
          await Assertions.Expect(users.Filter(new() { HasText = "Juste Leblanc" })).ToBeVisibleAsync();
          await Assertions.Expect(users.Filter(new() { HasText = "Marc Assin" })).ToBeVisibleAsync();
      }
  }
  ```

- [ ] **Step 2: Run it to verify it fails**

  Run: `dotnet test --project backend/tests/EndToEndTests`
  Expected: FAIL. `CS0246`: `PageTest` / `AppFixture` / namespace `EndToEndTests.Support` not found.

- [ ] **Step 3: Write the fixture and the page base class**

  `backend/tests/EndToEndTests/AssemblyInfo.cs`:

  ```csharp
  using EndToEndTests.Support;

  // One AppHost and one browser for the whole run; each test opens its own browser context.
  [assembly: AssemblyFixture(typeof(AppFixture))]
  ```

  `backend/tests/EndToEndTests/Support/AppFixture.cs`:

  ```csharp
  using Aspire.Hosting;
  using Aspire.Hosting.ApplicationModel;
  using Aspire.Hosting.Testing;
  using Microsoft.Playwright;

  namespace EndToEndTests.Support;

  public sealed class AppFixture : IAsyncLifetime
  {
      // The first run installs the frontend's npm packages and Chromium: allow plenty of time.
      private static readonly TimeSpan StartupTimeout = TimeSpan.FromMinutes(5);

      private DistributedApplication? _app;
      private IPlaywright? _playwright;
      private IBrowser? _browser;
      private Uri? _angularUrl;

      public DistributedApplication App => _app ?? throw NotStarted();

      public IBrowser Browser => _browser ?? throw NotStarted();

      public Uri AngularUrl => _angularUrl ?? throw NotStarted();

      public async ValueTask InitializeAsync()
      {
          using var timeout = new CancellationTokenSource(StartupTimeout);
          try
          {
              await StartAsync(timeout.Token);
          }
          catch
          {
              // xUnit does not dispose a fixture whose initialization failed: stop what already started.
              await DisposeAsync();
              throw;
          }
      }

      public async ValueTask DisposeAsync()
      {
          if (_browser is not null)
          {
              await _browser.DisposeAsync();
          }

          _playwright?.Dispose();

          if (_app is not null)
          {
              await _app.StopAsync();
              await _app.DisposeAsync();
          }
      }

      private async Task StartAsync(CancellationToken cancellationToken)
      {
          InstallChromium();

          var builder = await DistributedApplicationTestingBuilder.CreateAsync<Projects.N2JSoft_Aspire>(cancellationToken);
          _app = await builder.BuildAsync(cancellationToken);
          await _app.StartAsync(cancellationToken);
          await _app.ResourceNotifications.WaitForResourceHealthyAsync("webapi", cancellationToken);
          await _app.ResourceNotifications.WaitForResourceAsync("angular", KnownResourceStates.Running, cancellationToken);

          _angularUrl = _app.GetEndpoint("angular", "http");
          await WaitForAngularAsync(_angularUrl, cancellationToken);

          _playwright = await Playwright.CreateAsync();
          _browser = await _playwright.Chromium.LaunchAsync(new BrowserTypeLaunchOptions { Headless = true });
      }

      // Idempotent and fast once Chromium is cached in ~/.cache/ms-playwright, so no manual install step is needed.
      private static void InstallChromium()
      {
          var exitCode = Microsoft.Playwright.Program.Main(["install", "chromium"]);
          if (exitCode != 0)
          {
              throw new InvalidOperationException($"Installing Chromium for Playwright failed with exit code {exitCode}.");
          }
      }

      // "Running" only means the process started: the Angular dev server answers once its first build is done.
      private static async Task WaitForAngularAsync(Uri angularUrl, CancellationToken cancellationToken)
      {
          using var http = new HttpClient();
          while (true)
          {
              try
              {
                  using var response = await http.GetAsync(angularUrl, cancellationToken);
                  if (response.IsSuccessStatusCode)
                  {
                      return;
                  }
              }
              catch (HttpRequestException)
              {
                  // The dev server is not listening yet.
              }

              await Task.Delay(TimeSpan.FromSeconds(2), cancellationToken);
          }
      }

      private static InvalidOperationException NotStarted()
      {
          return new InvalidOperationException("The application is not started.");
      }
  }
  ```

  `backend/tests/EndToEndTests/Support/PageTest.cs`:

  ```csharp
  using Microsoft.Playwright;

  namespace EndToEndTests.Support;

  public abstract class PageTest(AppFixture fixture) : IAsyncLifetime
  {
      private static readonly string ArtifactsDirectory =
          Path.Combine(AppContext.BaseDirectory, "playwright-artifacts");

      private IBrowserContext? _context;
      private IPage? _page;

      protected AppFixture Fixture { get; } = fixture;

      protected IPage Page => _page ?? throw new InvalidOperationException("The page is not open.");

      public async ValueTask InitializeAsync()
      {
          _context = await Fixture.Browser.NewContextAsync(new BrowserNewContextOptions
          {
              BaseURL = Fixture.AngularUrl.ToString(),
          });
          await _context.Tracing.StartAsync(new TracingStartOptions { Screenshots = true, Snapshots = true, Sources = true });
          _page = await _context.NewPageAsync();
      }

      public async ValueTask DisposeAsync()
      {
          if (_context is not null)
          {
              // TestState is already set here; TestStatus still says "Running" and cannot be used.
              if (TestContext.Current.TestState?.Result == TestResult.Failed && _page is not null)
              {
                  await SaveFailureArtifactsAsync(_context, _page);
              }
              else
              {
                  await _context.Tracing.StopAsync();
              }

              await _context.DisposeAsync();
          }

          GC.SuppressFinalize(this);
      }

      private static async Task SaveFailureArtifactsAsync(IBrowserContext context, IPage page)
      {
          Directory.CreateDirectory(ArtifactsDirectory);
          var name = ArtifactName(TestContext.Current.Test?.TestDisplayName);
          await page.ScreenshotAsync(new PageScreenshotOptions
          {
              Path = Path.Combine(ArtifactsDirectory, $"{name}.png"),
              FullPage = true,
          });
          await context.Tracing.StopAsync(new TracingStopOptions { Path = Path.Combine(ArtifactsDirectory, $"{name}.zip") });
      }

      private static string ArtifactName(string? testName)
      {
          return string.Concat((testName ?? Guid.NewGuid().ToString("N"))
              .Select(c => char.IsLetterOrDigit(c) || c is '.' or '-' or '_' ? c : '_'));
      }
  }
  ```

- [ ] **Step 4: Run the journey to verify it passes**

  Run: `dotnet test --project backend/tests/EndToEndTests` (Docker must be running)
  Expected: PASS, 1 test. A warm run takes about 30 s; the first run downloads Chromium (~650 MB). On Fedora the
  Playwright driver prints `BEWARE: your OS is not officially supported by Playwright`; it is harmless.

  `WaitForResourceHealthyAsync("webapi")` currently returns as soon as the process runs, because `webapi` has no
  health check yet. Step 6 adds one.

- [ ] **Step 5: Write the failing dashboard link test**

  `backend/tests/EndToEndTests/AspireDashboardTests.cs`:

  ```csharp
  using EndToEndTests.Support;

  namespace EndToEndTests;

  public sealed class AspireDashboardTests(AppFixture fixture)
  {
      [Fact]
      public void The_webapi_resource_links_to_Scalar()
      {
          fixture.App.ResourceNotifications.TryGetCurrentState("webapi", out var resourceEvent).ShouldBeTrue();

          resourceEvent.ShouldNotBeNull().Snapshot.Urls
              .Where(url => url.DisplayProperties.DisplayName == "Scalar")
              .ShouldHaveSingleItem()
              .Url.ShouldEndWith("/scalar");
      }
  }
  ```

  Run: `dotnet test --project backend/tests/EndToEndTests`
  Expected: FAIL on `The_webapi_resource_links_to_Scalar`, because the sequence has no item.

- [ ] **Step 6: Add the health check and the Scalar link to the AppHost**

  In `tools/N2JSoft.Aspire/AppHost.cs`, the `webapi` resource becomes:

  ```csharp
  var webapi = builder.AddProject<Projects.WebApi>("webapi")
      .WithReference(mainDatabase)
      .WaitFor(postgres)
      .WaitFor(mainDatabase)
      .WithHttpHealthCheck("/health")
      .WithUrlForEndpoint("https", _ => new() { Url = "/scalar", DisplayText = "Scalar" });
  ```

  `WithHttpHealthCheck` makes "healthy" real for the dashboard, for `WaitFor(webapi)` on the `angular` resource,
  and for the fixture. This `WithUrlForEndpoint` overload *adds* a URL; the `Action<ResourceUrlAnnotation>`
  overload would rewrite the endpoint's own URL instead.

- [ ] **Step 7: Run the E2E tier to verify it passes**

  Run: `dotnet test --project backend/tests/EndToEndTests`
  Expected: PASS, 2 tests.

  Manual check of the Definition of Done: run `dotnet run --project tools/N2JSoft.Aspire`, open the dashboard,
  and confirm:
  - `webapi` shows a "Scalar" link that opens Scalar with the `v1` document and `GET /v1/users`;
  - the `angular` URL shows the users page listing Juste Leblanc and Marc Assin.

- [ ] **Step 8: Format and commit**

  ```bash
  dotnet jb cleanupcode N2JSoft.slnx --profile="ReformatAndReorder" --verbosity=ERROR
  git add -A backend/tests/EndToEndTests tools/N2JSoft.Aspire Directory.Packages.props N2JSoft.slnx
  git commit -m "test: add Aspire and Playwright end-to-end tier and the dashboard Scalar link"
  ```

---

### Task 7: Split CI by tier

**Files:**

- Modify: `.github/workflows/CI.yml`, `.github/dependabot.yml`

**Interfaces:**

- Consumes: the test project paths from Tasks 1, 2 and 6, and the coverage flags verified during planning.
- Produces: the jobs `backend` (3 OSes), `backend-acceptance`, `e2e` and `frontend`.

- [ ] **Step 1: Rewrite the workflow**

  Replace `.github/workflows/CI.yml` with:

  ```yaml
  name: CI

  permissions:
    contents: read

  on:
    push:
      branches:
        - develop
    pull_request:

  jobs:
    backend:
      runs-on: ${{ matrix.os }}
      strategy:
        matrix:
          os: [ ubuntu-latest, windows-latest, macos-latest ]
        fail-fast: false
      steps:
        - uses: actions/checkout@v7

        - name: Setup .NET
          uses: actions/setup-dotnet@v6
          with:
            dotnet-version: '10.0.x'
            cache: true
            cache-dependency-path: Directory.Packages.props

        - name: Restore dependencies
          run: dotnet restore

        - name: Build
          run: dotnet build --no-restore --configuration Release

        - name: Restore tools
          if: matrix.os == 'ubuntu-latest'
          run: dotnet tool restore

        - name: Check formatting (ReSharper)
          if: matrix.os == 'ubuntu-latest'
          run: |
            dotnet jb cleanupcode N2JSoft.slnx --profile="ReformatAndReorder" --no-build --verbosity=ERROR
            git diff --exit-code

        - name: Unit tests
          run: >-
            dotnet test --project backend/tests/UnitTests --no-build --configuration Release
            --coverage --coverage-output-format cobertura --coverage-output unit-tests.cobertura.xml
            --results-directory artifacts/coverage

        - name: Upload coverage reports
          if: matrix.os == 'ubuntu-latest'
          uses: actions/upload-artifact@v7
          with:
            name: coverage-reports
            path: artifacts/coverage/*.cobertura.xml
            retention-days: 14

    backend-acceptance:
      runs-on: ubuntu-latest
      steps:
        - uses: actions/checkout@v7

        - name: Setup .NET
          uses: actions/setup-dotnet@v6
          with:
            dotnet-version: '10.0.x'
            cache: true
            cache-dependency-path: Directory.Packages.props

        - name: Restore dependencies
          run: dotnet restore

        - name: Build
          run: dotnet build --no-restore --configuration Release

        - name: Acceptance tests (Testcontainers uses the runner's Docker)
          run: dotnet test --project backend/tests/AcceptanceTests --no-build --configuration Release

    e2e:
      runs-on: ubuntu-latest
      steps:
        - uses: actions/checkout@v7

        - name: Setup .NET
          uses: actions/setup-dotnet@v6
          with:
            dotnet-version: '10.0.x'
            cache: true
            cache-dependency-path: Directory.Packages.props

        - name: Setup Node.js
          uses: actions/setup-node@v7
          with:
            node-version: 24
            cache: npm
            cache-dependency-path: frontend/package-lock.json

        - name: Install frontend dependencies
          working-directory: frontend
          run: npm ci

        - name: Restore dependencies
          run: dotnet restore

        - name: Build
          run: dotnet build --no-restore --configuration Release

        - name: Create the ASP.NET Core development certificate (webapi's https endpoint)
          run: dotnet dev-certs https

        - name: Install Playwright Chromium and its system dependencies
          run: pwsh backend/tests/EndToEndTests/bin/Release/net10.0/playwright.ps1 install --with-deps chromium

        - name: End-to-end tests
          run: dotnet test --project backend/tests/EndToEndTests --no-build --configuration Release

        - name: Upload Playwright traces and screenshots
          if: failure()
          uses: actions/upload-artifact@v7
          with:
            name: playwright-artifacts
            path: backend/tests/EndToEndTests/bin/Release/net10.0/playwright-artifacts/
            retention-days: 14
            if-no-files-found: ignore

    frontend:
      runs-on: ubuntu-latest
      defaults:
        run:
          working-directory: frontend
      steps:
        - uses: actions/checkout@v7

        - name: Setup Node.js
          uses: actions/setup-node@v7
          with:
            node-version: 24
            cache: npm
            cache-dependency-path: frontend/package-lock.json

        - name: Install dependencies
          run: npm ci

        - name: Lint, test & build
          run: npx nx run-many -t lint test build
  ```

  Tiers are selected by project path, not by traits.

- [ ] **Step 2: Group the test packages in Dependabot**

  In `.github/dependabot.yml`, NuGet `groups`, insert before `nuget-other`:

  ```yaml
        testing:
          patterns:
            - "xunit*"
            - "Shouldly"
            - "NSubstitute*"
            - "Reqnroll*"
            - "Testcontainers*"
            - "Respawn"
            - "Microsoft.Playwright"
            - "Microsoft.Testing.*"
  ```

  `Microsoft.AspNetCore.Mvc.Testing` and `Aspire.Hosting.Testing` keep following their framework groups (first
  match wins).

- [ ] **Step 3: Verify the workflow locally**

  Run: `python3 -c "import yaml, sys; [yaml.safe_load(open(f)) for f in sys.argv[1:]]; print('valid')" .github/workflows/CI.yml .github/dependabot.yml`
  Expected: `valid`

  Run each job's commands as CI would:

  ```bash
  dotnet build --configuration Release
  dotnet test --project backend/tests/UnitTests --no-build --configuration Release --coverage --coverage-output-format cobertura --coverage-output unit-tests.cobertura.xml --results-directory artifacts/coverage
  ls artifacts/coverage/unit-tests.cobertura.xml
  dotnet test --project backend/tests/AcceptanceTests --no-build --configuration Release
  dotnet test --project backend/tests/EndToEndTests --no-build --configuration Release
  dotnet jb cleanupcode N2JSoft.slnx --profile="ReformatAndReorder" --no-build --verbosity=ERROR && git diff --exit-code
  ```

  Expected:
  - every test command reports `Passed!`;
  - the coverage file exists (`artifacts/` is gitignored);
  - `git diff --exit-code` exits 0.

- [ ] **Step 4: Commit**

  ```bash
  git add .github/workflows/CI.yml .github/dependabot.yml
  git commit -m "ci: run Docker-dependent test tiers in dedicated ubuntu jobs"
  ```

---

### Task 8: Documentation, roadmap, pull request

**Files:**

- Modify: `README.md` (section "Mes Notes"), `docs/superpowers/roadmap.md`

**Interfaces:**

- Consumes: everything above.
- Produces: a merged-ready `feat/foundation` branch and its PR into `develop`.

- [ ] **Step 1: Write "Mes Notes"**

  In `README.md`, replace the line `À compléter par vos soins.` under `## Mes Notes` with:

  ````markdown
  Le travail est découpé en tranches verticales décrites dans la [feuille de route](docs/superpowers/roadmap.md).
  Chaque tranche a sa spécification (`docs/superpowers/specs/`) et son plan d'implémentation
  (`docs/superpowers/plans/`).

  ### Tranche 1 — Fondations

  Réalisé :

  - **Clean Architecture** : le backend est découpé en `Domain`, `Application`, `Infrastructure` et `WebApi` (seul
    point de composition). La règle de dépendance (`Domain ← Application ← Infrastructure`) est vérifiée par des
    tests d'architecture. Les règles métier restent isolées et testables unitairement.
  - **CQRS léger, sans médiateur** : un handler par cas d'usage en écriture (à partir de la tranche 2) ; en
    lecture, des interfaces de requêtes déclarées dans `Application` et implémentées par des projections EF Core
    `AsNoTracking`. MediatR est devenu payant et n'apporte rien à cette taille de projet.
  - **Versionnement d'API** par segment d'URL (`/v1/users`) avec `Asp.Versioning` : visible dans les URL et dans
    la documentation. Une version inconnue répond `404`.
  - **OpenAPI + Scalar** en développement uniquement : un document par version, et un lien « Scalar » sur la
    ressource `webapi` du dashboard Aspire.
  - **Contrat d'erreur unique** (RFC 9457 ProblemDetails) :
    - `400` pour la validation ;
    - `404` pour une ressource introuvable ;
    - `422` pour une règle métier violée, avec un `code` stable que le front traduit en français ;
    - `500` sans détail technique hors développement.
  - **Redis supprimé** : il n'était pas utilisé et aurait imposé un conteneur de plus à chaque niveau de tests.
  - **Front** : la page d'accueil Nx est remplacée par une coquille en français (locale `fr-FR`), avec la liste
    des utilisateurs.
  - **CI** : build et tests unitaires sur Ubuntu, Windows et macOS ; les tests qui ont besoin de Docker
    (acceptation, bout en bout) tournent sur Ubuntu uniquement.

  ### Lancer les tests

  | Niveau                                      | Commande                                                   | Prérequis      |
  |---------------------------------------------|------------------------------------------------------------|----------------|
  | Unitaires (xUnit v3, Shouldly, NSubstitute) | `dotnet test --project backend/tests/UnitTests`            | —              |
  | Acceptation (Reqnroll, Testcontainers)      | `dotnet test --project backend/tests/AcceptanceTests`      | Docker         |
  | Bout en bout (Aspire, Playwright)           | `dotnet test --project backend/tests/EndToEndTests`        | Docker, Node.js |
  | Front (Vitest)                              | `cd frontend && npx nx run-many -t lint test build`        | Node.js        |

  - Les tests de bout en bout installent Chromium automatiquement au premier lancement (dans
    `~/.cache/ms-playwright`). Sous Linux, s'il manque des bibliothèques système :
    `pwsh backend/tests/EndToEndTests/bin/Debug/net10.0/playwright.ps1 install-deps chromium`.
  - En cas d'échec, une trace et une capture d'écran sont écrites dans
    `backend/tests/EndToEndTests/bin/<Configuration>/net10.0/playwright-artifacts/`.
  - Couverture de code : ajouter `--coverage --coverage-output-format cobertura --results-directory artifacts/coverage`.

  ### Pistes d'amélioration

  - Générer le client TypeScript à partir du document OpenAPI, au lieu de maintenir les interfaces à la main.
  - Passer le front en détection de changements sans zone.js (zoneless).
  - Supprimer le script `prestart: npm install` du front : Aspire 13.6 installe déjà les dépendances avant de
    lancer l'application.
  ````

- [ ] **Step 2: Update the roadmap**

  In `docs/superpowers/roadmap.md`:
  - In the slices table, Foundation row: `Spec ready` → `Done`. Append the plan link to the Spec column:
    `[foundation](specs/2026-10-10-foundation-design.md), [plan](plans/2026-10-10-foundation.md)`.
  - In "Conventions every slice inherits", replace the `**Errors:**` bullet with:

    ```markdown
    - **Errors:** request shape validated by .NET 10 built-in minimal-API validation (DataAnnotations → `400`).
      Request records must be declared `public`: the validation source generator silently skips other types.
      `NotFoundException` → `404`; `BusinessRuleViolationException` → `422` with a stable `code` extension
      (e.g. `expense.quota_exceeded`). Unknown routes and unsupported versions answer a `404` ProblemDetails.
      The UI maps `code` to its French message.
    ```

  - In the same section, under the `**Tests:**` bullet, append to the *Acceptance* sub-bullet:
    `Reuse ApiDriver and HttpSteps ("When I request …", "Then the response is a problem with status …").` Append
    to the *End-to-end* sub-bullet: `Derive page tests from PageTest; the dev seed (Juste Leblanc, Marc Assin) is
    present.`

- [ ] **Step 3: Verify and commit**

  Run: `npx --yes markdownlint-cli2 README.md docs/superpowers/roadmap.md docs/superpowers/plans/2026-10-10-foundation.md`
  Expected: `0 error(s)` (rules configured by `.markdownlint.json`).

  ```bash
  git add README.md docs/superpowers/roadmap.md
  git commit -m "docs: record the foundation slice in Mes Notes and the roadmap"
  ```

- [ ] **Step 4: Rebase on `develop` and run everything once more**

  `develop` has moved since this branch was cut (e.g. #20).

  ```bash
  git fetch origin
  git rebase origin/develop
  dotnet build N2JSoft.slnx --configuration Release
  dotnet test --project backend/tests/UnitTests --no-build --configuration Release
  dotnet test --project backend/tests/AcceptanceTests --no-build --configuration Release
  dotnet test --project backend/tests/EndToEndTests --no-build --configuration Release
  (cd frontend && npx nx run-many -t lint test build)
  ```

  Expected: every suite passes (15 unit, 10 acceptance, 2 E2E, 6 frontend).

- [ ] **Step 5: Push and open the pull request (ask the user first: this publishes the branch)**

  ```bash
  git push -u origin feat/foundation
  gh pr create --base develop --head feat/foundation \
    --title "feat: foundation — Clean Architecture, API conventions, test tiers, French shell" \
    --body-file - <<'EOF'
  ## Summary

  Slice 1 of the roadmap (spec: `docs/superpowers/specs/2026-10-10-foundation-design.md`).

  - Backend split into Domain / Application / Infrastructure / WebApi, dependency rule enforced by architecture tests
  - `/v1/...` URL-segment versioning, OpenAPI per version + Scalar (Development), dashboard "Scalar" link
  - One ProblemDetails error contract: 400 validation, 404 not found, 422 business rule + `code`, 500 without detail outside Development
  - Redis removed
  - Test tiers: Unit (xUnit v3 / MTP), Acceptance (Reqnroll + Testcontainers + Respawn), End-to-end (Aspire + Playwright)
  - French Angular shell listing users
  - CI: Docker-dependent tiers on ubuntu only

  ## Test plan

  - [ ] CI green (backend ×3 OS, backend-acceptance, e2e, frontend)
  - [ ] `dotnet run --project tools/N2JSoft.Aspire`: users page lists the seeded users; "Scalar" link shows `GET /v1/users`
  EOF
  ```

  Expected: the PR URL is printed. Then wait for CI and fix anything red before calling the slice done.
