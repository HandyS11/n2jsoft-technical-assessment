# Expense Reports Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Add expense reports: created for an active user and a month with a stored French title, one per user and
month, listed and read under `/v1/expense-reports`, deleted physically, with a French reports page (create form and
paged list) and a report page (header and delete).

**Architecture:** `ExpenseReport` entity and `ReportMonth` value object in `Domain`; the active-user rule lives in
`ExpenseReport.Create`, the one-per-month rule in `CreateExpenseReportHandler` (asks the store first) backed by a
unique index whose violation the store maps to the same `422`. One store per feature (`IExpenseReportStore` over the
shared `AppDbContext`), one handler per write. Report reads never join `users`, so the reports of a soft-deleted user
stay visible. The user picker reads `GET /v1/users?isActive=true` page by page.

**Tech Stack:** .NET 10 (SDK 10.0.401) minimal APIs, EF Core 10 + Npgsql (Aspire 13.6.1), xUnit v3 on
Microsoft.Testing.Platform, Shouldly, NSubstitute, Reqnroll + Testcontainers + Respawn, Aspire.Hosting.Testing +
Playwright, Angular 22 + Nx 23 + Vitest.

**Spec:** [docs/superpowers/specs/2026-10-11-expense-reports-design.md](../specs/2026-10-11-expense-reports-design.md)

## Verified during planning

The spec's two risks were spiked in a throwaway worktree of this repository, with the repo's analyzer set (warnings
as errors) and a real Postgres 18.3. No spike task remains.

1. **No index over a complex property.** EF Core 10 refuses `HasIndex(r => new { r.UserId, r.Month.Year, … })`
   ("not a valid member access expression") and `HasIndex("UserId", "Month_Year", …)` (no such CLR property). The
   spec's fallback applies: `Year` and `Month` are plain `int` properties; `Period` is an unmapped getter
   (`new ReportMonth(Year, Month)`), which EF ignores without configuration. The generated migration creates
   `expense_reports` with the unique index `IX_expense_reports_UserId_Year_Month`.
2. **Duplicate detection.** Saving a second report for the same user and month throws a `DbUpdateException` whose
   `InnerException` is a `PostgresException` with `SqlState == PostgresErrorCodes.UniqueViolation` (`23505`) and
   `ConstraintName == "IX_expense_reports_UserId_Year_Month"`. `AnyAsync` on `UserId`/`Year`/`Month` and the sort on them
   translate; a report of a soft-deleted user still loads (no join); `Remove` + `SaveChangesAsync` deletes it.

The production backend code of Tasks 1–5 (Domain, Application, Infrastructure, WebApi) was compiled in that worktree
and passed through `jb cleanupcode`; the code below is the formatted result. Not verified during planning: the
test code, the Angular code and the Playwright journeys (the build or the test run tells you if an analyzer or an
API name needs a small fix).

## Spec deviations (decided while planning)

- **Columns `Year` / `Month` instead of a complex property**, table `expense_reports` (the `users` naming), see
  above. The spec has been updated accordingly.
- **The `ReportMonth` parameter is named `period`** everywhere (`ExpenseReport.Create(user, period)`,
  `CreateExpenseReportCommand(UserId, Period)`, `ExistsAsync(userId, period, …)`): `Month` is already the int.
- **A concurrent deletion answers `404`**: two requests that both read a report then delete it would make the
  second `SaveChangesAsync` throw `DbUpdateConcurrencyException` (a `500`). The store maps it to
  `NotFoundException`. Reports are never updated, so a concurrency failure can only mean "already deleted".
  `NotFoundException` and `BusinessRuleViolationException` gain an optional `innerException` for this.
- **`DuplicateExpenseReportException` carries no user or month**: the same exception is thrown by the handler and by
  the store, which only has the database error.
- **No active user**: the create form is replaced by "Aucun utilisateur actif." (the spec said the select is
  replaced and *Créer* disabled; hiding the whole form says the same with less markup).
- **Month field validation**: `<input type="month">` falls back to a text field in some browsers, so the form also
  checks the value is `AAAA-MM` between `2000-01` and `2100-12`, with the message "Indiquez un mois au format
  AAAA-MM, entre 2000-01 et 2100-12." A user must be chosen: "Choisissez un utilisateur."
- **Report page delete**: a `404` (deleted meanwhile) goes back to the list like a success; any other failure shows
  "La suppression a échoué. Veuillez réessayer."
- **Confirmation text** (list and report page): `Supprimer la note de frais « <titre> » ? Ses dépenses seront
  supprimées avec elle.`, built by one shared function.
- **`pageCountOf`** moves from `user-list.page.ts` to `core/paged-result.ts`, shared by both lists.
- **Acceptance races** are exercised at the store level (two DI scopes, both read, then both save), because two HTTP
  requests cannot be interleaved deterministically.

## Global Constraints

- SDK `10.0.401`; central package management, versions only in `Directory.Packages.props`. **No new package** in
  this slice.
- Do not add `Microsoft.NET.Test.Sdk` or `xunit.runner.visualstudio` (they bring VSTest back).
- `TreatWarningsAsErrors` stays on. No `#pragma warning disable`, no `<NoWarn>`, no new `.editorconfig` relaxation.
- Dependency rule unchanged: `Application` references `Domain` only (plus DI abstractions) — no EF Core, no ASP.NET
  Core, no Npgsql. The architecture tests enforce it.
- Types consumed across projects are `public`; everything else `internal`. Request records are `public` and live in
  `backend/src/WebApi/Endpoints/Requests/` (validation skips other types).
- Every method returning `Task`/`ValueTask` ends with `Async`, Reqnroll steps included (VSTHRD200). xUnit test
  methods use underscore sentences. Library code awaits with `.ConfigureAwait(false)`; test code does not.
- Limits, copied from the spec: months from January 2000 (`ReportMonth.MinYear = 2000`) to December 2100
  (`ReportMonth.MaxYear = 2100`), month 1–12; title at most `ExpenseReport.TitleMaxLength = 250` characters; title
  format `"{FirstName} {LastName} - {Mois} {Année}"`, e.g. `Juste Leblanc - Octobre 2025`.
- Business rule codes, verbatim: `expense_report.inactive_user`, `expense_report.already_exists`.
- Error contract: `400` validation; `404` unknown report, unknown or deleted user in a create request, malformed id;
  `422` with `code` for the two rules.
- English for code, Gherkin, commit messages and ProblemDetails. French for UI text and README "Mes Notes".
- Run commands from the repository root. `dtk dotnet …` is a token-saving wrapper; plain `dotnet …` works too.
- Before each backend commit: `dotnet tool restore` (once), then
  `dotnet jb cleanupcode N2JSoft.slnx --profile="ReformatAndReorder" --verbosity=ERROR`. CI fails on any diff it
  produces. If it moves code, rerun the task's tests before committing.
- Commits: conventional, small, on branch `feat/expense-reports` (already created; the spec is committed on it).
  **No `Co-Authored-By` trailer in commits and no "Generated with Claude Code" line in the PR.** This repository
  rule overrides any default attribution.

## Review Focus

Inputs the spec implies but does not spell out, most likely to bite first. Each one has a test in the owning task.

1. **Two requests creating the same report at once** (double click, two tabs) must give one `201` and one
   `422 expense_report.already_exists`, not a `500` from the unique index. Pinned in Task 3 (store-level race
   scenario).
2. **Two requests deleting the same report at once** must give one `204` and one `404`, not a `500` from
   `DbUpdateConcurrencyException`. Pinned in Task 4 (store-level race scenario).
3. **A malformed or empty month** typed where the browser shows `<input type="month">` as plain text (`""`,
   `1999-12`) must be refused by the form with a French message, not sent as `NaN`. Pinned in Task 7.
4. **Renaming a user after their report exists** must leave the report's title unchanged, in the API and the list.
   Pinned in Task 1 (unit) and Task 2 (acceptance).
5. **Deleting the only report of the last page** must show the previous page, not "Page 3 sur 2". Pinned in Task 7.

---

## File map

```text
backend/src/Domain/
  ValueObjects/ReportMonth.cs                          year + month, French label                          (T1)
  Entities/ExpenseReport.cs                            Create(user, period), stored title                  (T1)
  Exceptions/InactiveUserException.cs                  expense_report.inactive_user                        (T1)
  Exceptions/DuplicateExpenseReportException.cs        expense_report.already_exists                       (T1)
  Exceptions/BusinessRuleViolationException.cs         + optional innerException                           (T1)
backend/src/Application/
  Exceptions/NotFoundException.cs                      + optional innerException                           (T4)
  ExpenseReports/IExpenseReportStore.cs                reads (T2), create (T3), delete (T4)
  ExpenseReports/ExpenseReportSummary.cs, ExpenseReportDetails.cs                                          (T2, T3)
  ExpenseReports/Commands/CreateExpenseReport.cs                                                           (T3)
  ExpenseReports/Commands/DeleteExpenseReport.cs                                                           (T4)
  DependencyInjectionExtensions.cs                     registers the two handlers                          (T3, T4)
  Users/IUserStore.cs                                  ListAsync gains bool? isActive                      (T5)
backend/src/Infrastructure/
  Persistence/ExpenseReportEntityTypeConfiguration.cs  table, FK, unique index                             (T2)
  Persistence/AppDbContext.cs                          applies the configuration                           (T2)
  Persistence/Migrations/<timestamp>_ExpenseReports.cs generated                                           (T2)
  ExpenseReports/ExpenseReportStore.cs                 reads (T2), create (T3), delete (T4)
  DependencyInjectionExtensions.cs                     registers IExpenseReportStore                       (T2)
  Users/UserStore.cs                                   isActive filter                                     (T5)
backend/src/WebApi/
  Program.cs                                           MapExpenseReportEndpoints()                         (T2)
  Endpoints/ExpenseReportEndpoints.cs                  GET list, GET id (T2), POST (T3), DELETE (T4)
  Endpoints/Requests/CreateExpenseReportRequest.cs                                                         (T3)
  Endpoints/UserEndpoints.cs                           isActive query parameter                            (T5)
backend/tests/UnitTests/
  ValueObjects/ReportMonthTests.cs, Entities/ExpenseReportTests.cs                                         (T1)
  ExpenseReports/Commands/CreateExpenseReportHandlerTests.cs                                               (T3)
  ExpenseReports/Commands/DeleteExpenseReportHandlerTests.cs                                               (T4)
backend/tests/AcceptanceTests/
  Support/KnownExpenseReports.cs, Support/Months.cs                                                        (T2)
  Support/JsonRequests.cs                              With() moved out of UserCommandSteps                (T3)
  StepDefinitions/ExpenseReportSteps.cs                arrange + reads                                     (T2, T4)
  StepDefinitions/ExpenseReportCommandSteps.cs         create, delete, races                               (T3, T4)
  StepDefinitions/HttpSteps.cs                         business rule violation step                        (T3)
  StepDefinitions/ApiDocumentationSteps.cs             query parameter step                                (T5)
  StepDefinitions/UserCommandSteps.cs                  uses JsonRequests.With                              (T3)
  Features/ListExpenseReports.feature, GetExpenseReport.feature                                            (T2)
  Features/CreateExpenseReport.feature                                                                     (T3)
  Features/DeleteExpenseReport.feature                                                                     (T4)
  Features/ListUsers.feature                           isActive scenarios                                  (T5)
  Features/ApiDocumentation.feature                    expense report paths and errors (T2–T4), isActive (T5)
backend/tests/EndToEndTests/
  UsersPageTests.cs                                    opens /users; / opens the reports                   (T9)
  ExpenseReportManagementTests.cs                      3 journeys                                          (T9)
frontend/src/app/
  core/paged-result.ts                                 + pageCountOf                                       (T6)
  core/problem-details.ts (+ spec)                     businessRuleCode                                    (T6)
  features/users/users.api.ts (+ spec)                 isActive, listAllActive                             (T6)
  features/users/user-list.page.ts                     imports pageCountOf                                 (T6)
  features/expense-reports/expense-report-summary.ts, expense-report-details.ts                            (T6)
  features/expense-reports/expense-reports.api.ts (+ spec)                                                 (T6)
  features/expense-reports/confirm-deletion.ts                                                             (T7)
  features/expense-reports/expense-report-list.page.ts (+ spec)                                            (T7)
  features/expense-reports/expense-report.page.ts (+ spec)                                                 (T8)
  app.routes.ts, app.routes.spec.ts                    /expense-reports (T7), /expense-reports/:id (T8)
  app.html, app.spec.ts                                Notes de frais link                                 (T7)
README.md, docs/superpowers/roadmap.md                                                                     (T10)
```

---

### Task 1: Expense report domain

**Files:**

- Create: `backend/src/Domain/ValueObjects/ReportMonth.cs`, `backend/src/Domain/Entities/ExpenseReport.cs`,
  `backend/src/Domain/Exceptions/InactiveUserException.cs`,
  `backend/src/Domain/Exceptions/DuplicateExpenseReportException.cs`
- Modify: `backend/src/Domain/Exceptions/BusinessRuleViolationException.cs`
- Test: `backend/tests/UnitTests/ValueObjects/ReportMonthTests.cs`,
  `backend/tests/UnitTests/Entities/ExpenseReportTests.cs`

**Interfaces:**

- Consumes: `Domain.Entities.User` (`Id`, `FirstName`, `LastName`, `IsActive`, `User.Create`, `Deactivate`,
  `UpdateDetails`, `User.NameMaxLength`), `Domain.Exceptions.InvalidValueException(string parameterName, string
  message)` with `ParameterName`, `BusinessRuleViolationException.Code`.
- Produces:
  - `public sealed record ReportMonth(int year, int month)` with `int Year`, `int Month`, `const int MinYear = 2000`,
    `const int MaxYear = 2100`, `ToString()` → `"Octobre 2025"`.
  - `public sealed class ExpenseReport` with `Guid Id`, `Guid UserId`, `int Year`, `int Month`,
    `ReportMonth Period`, `string Title`, `const int TitleMaxLength = 250`,
    `static ExpenseReport Create(User user, ReportMonth period)`.
  - `public sealed class InactiveUserException(Guid userId)` — code `expense_report.inactive_user`.
  - `public sealed class DuplicateExpenseReportException(Exception? innerException = null)` — code
    `expense_report.already_exists`.
  - `BusinessRuleViolationException(string code, string message, Exception? innerException = null)`.

- [ ] **Step 1: Write the failing `ReportMonth` tests**

  `backend/tests/UnitTests/ValueObjects/ReportMonthTests.cs`:

  ```csharp
  using Domain.Exceptions;
  using Domain.ValueObjects;

  namespace UnitTests.ValueObjects;

  public sealed class ReportMonthTests
  {
      [Fact]
      public void Keeps_the_year_and_the_month()
      {
          var period = new ReportMonth(2025, 10);

          period.Year.ShouldBe(2025);
          period.Month.ShouldBe(10);
      }

      [Fact]
      public void Two_months_with_the_same_year_and_month_are_equal()
      {
          new ReportMonth(2025, 10).ShouldBe(new ReportMonth(2025, 10));
      }

      [Theory]
      [InlineData(2000, 1)]
      [InlineData(2100, 12)]
      public void Accepts_the_first_and_the_last_month_allowed(int year, int month)
      {
          Should.NotThrow(() => new ReportMonth(year, month));
      }

      [Theory]
      [InlineData(1999)]
      [InlineData(2101)]
      public void Rejects_a_year_out_of_range(int year)
      {
          Should.Throw<InvalidValueException>(() => new ReportMonth(year, 1)).ParameterName.ShouldBe("year");
      }

      [Theory]
      [InlineData(0)]
      [InlineData(13)]
      public void Rejects_a_month_out_of_range(int month)
      {
          Should.Throw<InvalidValueException>(() => new ReportMonth(2025, month)).ParameterName.ShouldBe("month");
      }

      [Theory]
      [InlineData(1, "Janvier 2025")]
      [InlineData(2, "Février 2025")]
      [InlineData(3, "Mars 2025")]
      [InlineData(4, "Avril 2025")]
      [InlineData(5, "Mai 2025")]
      [InlineData(6, "Juin 2025")]
      [InlineData(7, "Juillet 2025")]
      [InlineData(8, "Août 2025")]
      [InlineData(9, "Septembre 2025")]
      [InlineData(10, "Octobre 2025")]
      [InlineData(11, "Novembre 2025")]
      [InlineData(12, "Décembre 2025")]
      public void Reads_as_the_capitalised_french_month_then_the_year(int month, string expected)
      {
          new ReportMonth(2025, month).ToString().ShouldBe(expected);
      }
  }
  ```

- [ ] **Step 2: Write the failing `ExpenseReport` tests**

  `backend/tests/UnitTests/Entities/ExpenseReportTests.cs`:

  ```csharp
  using Domain.Entities;
  using Domain.Exceptions;
  using Domain.ValueObjects;

  namespace UnitTests.Entities;

  public sealed class ExpenseReportTests
  {
      private static readonly Address Paris = new("12 rue de Rivoli", "75001", "Paris");
      private static readonly ReportMonth October2025 = new(2025, 10);

      [Fact]
      public void Create_assigns_the_user_and_the_month()
      {
          var user = User.Create("Juste", "Leblanc", Paris, 10);

          var report = ExpenseReport.Create(user, October2025);

          report.Id.ShouldNotBe(Guid.Empty);
          report.UserId.ShouldBe(user.Id);
          report.Year.ShouldBe(2025);
          report.Month.ShouldBe(10);
          report.Period.ShouldBe(October2025);
      }

      [Fact]
      public void Create_titles_the_report_with_the_user_name_then_the_month()
      {
          var report = ExpenseReport.Create(User.Create("Juste", "Leblanc", Paris, 10), October2025);

          report.Title.ShouldBe("Juste Leblanc - Octobre 2025");
      }

      [Fact]
      public void Create_gives_every_report_its_own_id()
      {
          var user = User.Create("Juste", "Leblanc", Paris, 10);

          ExpenseReport.Create(user, October2025).Id.ShouldNotBe(ExpenseReport.Create(user, October2025).Id);
      }

      [Fact]
      public void The_title_fits_its_column_for_the_longest_names_and_month()
      {
          var name = new string('a', User.NameMaxLength);

          var report = ExpenseReport.Create(User.Create(name, name, Paris, 10), new ReportMonth(2025, 9));

          report.Title.Length.ShouldBeLessThanOrEqualTo(ExpenseReport.TitleMaxLength);
      }

      [Fact]
      public void The_title_does_not_follow_a_later_rename_of_the_user()
      {
          var user = User.Create("Juste", "Leblanc", Paris, 10);
          var report = ExpenseReport.Create(user, October2025);

          user.UpdateDetails("Justine", "Leblanc", Paris, 10);

          report.Title.ShouldBe("Juste Leblanc - Octobre 2025");
      }

      [Fact]
      public void Create_refuses_an_inactive_user()
      {
          var user = User.Create("Juste", "Leblanc", Paris, 10);
          user.Deactivate();

          Should.Throw<InactiveUserException>(() => ExpenseReport.Create(user, October2025))
              .Code.ShouldBe("expense_report.inactive_user");
      }
  }
  ```

- [ ] **Step 3: Run the tests to verify they fail**

  Run: `dtk dotnet build backend/tests/UnitTests`
  Expected: FAIL — `ReportMonth`, `ExpenseReport` and `InactiveUserException` do not exist (CS0246).

- [ ] **Step 4: Add `innerException` to the business rule base**

  Replace `backend/src/Domain/Exceptions/BusinessRuleViolationException.cs` with:

  ```csharp
  namespace Domain.Exceptions;

  /// <summary>
  /// Base for every business rule broken by a domain operation; mapped to 422 with <see cref="Code" /> as a stable
  /// identifier the frontend translates (e.g. "expense.quota_exceeded").
  /// </summary>
  public abstract class BusinessRuleViolationException(string code, string message, Exception? innerException = null)
      : Exception(message, innerException)
  {
      public string Code { get; } = code;
  }
  ```

- [ ] **Step 5: Write `ReportMonth`**

  `backend/src/Domain/ValueObjects/ReportMonth.cs`:

  ```csharp
  using Domain.Exceptions;

  namespace Domain.ValueObjects;

  /// <summary>The calendar month an expense report covers, from January 2000 to December 2100.</summary>
  public sealed record ReportMonth
  {
      public const int MinYear = 2000;
      public const int MaxYear = 2100;

      /// <summary>
      /// Hard-coded rather than read from CultureInfo("fr-FR"): a title must depend neither on ICU being installed nor on
      /// a culture's capitalisation.
      /// </summary>
      private static readonly string[] FrenchNames =
      [
          "Janvier", "Février", "Mars", "Avril", "Mai", "Juin",
          "Juillet", "Août", "Septembre", "Octobre", "Novembre", "Décembre",
      ];

      public ReportMonth(int year, int month)
      {
          if (year is < MinYear or > MaxYear)
          {
              throw new InvalidValueException(nameof(year), $"The year must be between {MinYear} and {MaxYear}.");
          }

          if (month is < 1 or > 12)
          {
              throw new InvalidValueException(nameof(month), "The month must be between 1 and 12.");
          }

          Year = year;
          Month = month;
      }

      public int Year { get; }

      /// <summary>1 for January to 12 for December.</summary>
      public int Month { get; }

      /// <summary>The French label used in report titles: "Octobre 2025".</summary>
      public override string ToString()
      {
          return $"{FrenchNames[Month - 1]} {Year}";
      }
  }
  ```

- [ ] **Step 6: Write the two exceptions**

  `backend/src/Domain/Exceptions/InactiveUserException.cs`:

  ```csharp
  namespace Domain.Exceptions;

  public sealed class InactiveUserException(Guid userId)
      : BusinessRuleViolationException(
          "expense_report.inactive_user",
          $"User '{userId}' is inactive: only active users can be assigned to an expense report.");
  ```

  `backend/src/Domain/Exceptions/DuplicateExpenseReportException.cs`:

  ```csharp
  namespace Domain.Exceptions;

  /// <summary>A user has at most one expense report per month.</summary>
  public sealed class DuplicateExpenseReportException(Exception? innerException = null)
      : BusinessRuleViolationException(
          "expense_report.already_exists",
          "The user already has an expense report for this month.",
          innerException);
  ```

- [ ] **Step 7: Write `ExpenseReport`**

  `backend/src/Domain/Entities/ExpenseReport.cs`:

  ```csharp
  using Domain.Exceptions;
  using Domain.ValueObjects;

  namespace Domain.Entities;

  /// <summary>A user's expenses for one calendar month. Nothing changes after creation; a deletion is physical.</summary>
  public sealed class ExpenseReport
  {
      /// <summary>Room for two names of <see cref="User.NameMaxLength" />, the separators and any month label.</summary>
      public const int TitleMaxLength = 250;

      /// <summary>Only <see cref="Create" /> and EF Core, which then sets the properties it loads, build reports.</summary>
      private ExpenseReport()
      {
      }

      public Guid Id { get; } = Guid.CreateVersion7();
      public Guid UserId { get; private set; }

      /// <summary>
      /// <see cref="Year" /> and <see cref="Month" /> are stored as two columns rather than as a complex property: EF Core
      /// cannot index a complex property's columns, and (user, year, month) is unique.
      /// </summary>
      public int Year { get; private set; }

      public int Month { get; private set; }

      public ReportMonth Period => new(Year, Month);

      /// <summary>"First Last - Month Year", fixed at creation: renaming or deleting the user leaves it unchanged.</summary>
      public string Title { get; private set; } = string.Empty;

      /// <summary>Throws <see cref="InactiveUserException" /> (422) when the user is inactive.</summary>
      public static ExpenseReport Create(User user, ReportMonth period)
      {
          ArgumentNullException.ThrowIfNull(user);
          ArgumentNullException.ThrowIfNull(period);
          if (!user.IsActive)
          {
              throw new InactiveUserException(user.Id);
          }

          return new ExpenseReport
          {
              UserId = user.Id,
              Year = period.Year,
              Month = period.Month,
              Title = $"{user.FirstName} {user.LastName} - {period}",
          };
      }
  }
  ```

- [ ] **Step 8: Run the unit tests**

  Run: `dtk dotnet test --project backend/tests/UnitTests`
  Expected: PASS — the new `ReportMonthTests` (21 cases) and `ExpenseReportTests` (6) included, nothing else broken.

- [ ] **Step 9: Format and commit**

  ```bash
  dotnet jb cleanupcode N2JSoft.slnx --profile="ReformatAndReorder" --verbosity=ERROR
  git add -A backend
  git commit -m "feat(expense-reports): add the expense report entity and its month"
  ```

---

### Task 2: Expense report persistence and read endpoints

**Files:**

- Create: `backend/src/Infrastructure/Persistence/ExpenseReportEntityTypeConfiguration.cs`,
  `backend/src/Infrastructure/Persistence/Migrations/<timestamp>_ExpenseReports.cs` (generated, with its
  `.Designer.cs`), `backend/src/Application/ExpenseReports/IExpenseReportStore.cs`,
  `backend/src/Application/ExpenseReports/ExpenseReportSummary.cs`,
  `backend/src/Application/ExpenseReports/ExpenseReportDetails.cs`,
  `backend/src/Infrastructure/ExpenseReports/ExpenseReportStore.cs`,
  `backend/src/WebApi/Endpoints/ExpenseReportEndpoints.cs`
- Modify: `backend/src/Infrastructure/Persistence/AppDbContext.cs`,
  `backend/src/Infrastructure/Persistence/Migrations/AppDbContextModelSnapshot.cs` (generated),
  `backend/src/Infrastructure/DependencyInjectionExtensions.cs`, `backend/src/WebApi/Program.cs`,
  `backend/tests/AcceptanceTests/Features/ApiDocumentation.feature`
- Test: `backend/tests/AcceptanceTests/Support/KnownExpenseReports.cs`,
  `backend/tests/AcceptanceTests/Support/Months.cs`,
  `backend/tests/AcceptanceTests/StepDefinitions/ExpenseReportSteps.cs`,
  `backend/tests/AcceptanceTests/Features/ListExpenseReports.feature`,
  `backend/tests/AcceptanceTests/Features/GetExpenseReport.feature`

**Interfaces:**

- Consumes: Task 1's `ExpenseReport`, `ReportMonth`. Existing `PagedResult<T>(Items, Page, PageSize, TotalCount)`,
  `PageRequest(Page, PageSize)` with `Skip`, `NotFoundException(string resource, object key)`, `KnownUsers.IdOf`,
  `ApiDriver.GetAsync` / `ReadJsonAsync<T>`, the user steps "the following users exist", "I update the user {string}
  with", "I delete the user {string}".
- Produces:
  - `public sealed record ExpenseReportSummary(Guid Id, string Title, int Year, int Month)`.
  - `public sealed record ExpenseReportDetails(Guid Id, string Title, Guid UserId, int Year, int Month)` with
    `static ExpenseReportDetails From(ExpenseReport report)`.
  - `IExpenseReportStore` with `ListAsync(PageRequest, CancellationToken)` and `GetDetailsAsync(Guid,
    CancellationToken)` (Tasks 3 and 4 add the write members).
  - `ExpenseReportEntityTypeConfiguration.UserYearAndMonthIndexName = "IX_expense_reports_UserId_Year_Month"` (internal).
  - `GET /v1/expense-reports` and `GET /v1/expense-reports/{id}`; `ExpenseReportEndpoints.MapExpenseReportEndpoints`.
  - Acceptance: `KnownExpenseReports` (`Add(title, id)`, `IdOf(title)`), `Months.Parse("2025-10")` →
    `ReportMonth`, `Months.Format(year, month)` → `"2025-10"`; steps "the following expense reports exist", "I list
    the expense reports", "I list page {int} of the expense reports with {int} reports per page", "I get the
    expense report {string}", "I get an unknown expense report", "the expense reports listed are", "no expense
    report is listed", "the listing reports {int} expense reports in total", "the expense report returned is".

- [ ] **Step 1: Write the acceptance support classes**

  `backend/tests/AcceptanceTests/Support/KnownExpenseReports.cs`:

  ```csharp
  namespace AcceptanceTests.Support;

  /// <summary>
  /// The ids of the expense reports a scenario arranged or created, by title ("Juste Leblanc - Octobre 2025"). One
  /// instance per scenario (Reqnroll context injection), shared by the step classes.
  /// </summary>
  public sealed class KnownExpenseReports
  {
      private readonly Dictionary<string, Guid> _ids = [];

      public void Add(string title, Guid id)
      {
          _ids[title] = id;
      }

      public Guid IdOf(string title)
      {
          return _ids.TryGetValue(title, out var id)
              ? id
              : throw new InvalidOperationException(
                  $"No expense report titled {title} was arranged or created in this scenario.");
      }
  }
  ```

  `backend/tests/AcceptanceTests/Support/Months.cs`:

  ```csharp
  using System.Globalization;
  using Domain.ValueObjects;

  namespace AcceptanceTests.Support;

  /// <summary>Months are written "2025-10" in the feature files.</summary>
  public static class Months
  {
      private const string Pattern = "yyyy-MM";

      public static ReportMonth Parse(string value)
      {
          var date = DateOnly.ParseExact(value, Pattern, CultureInfo.InvariantCulture);
          return new ReportMonth(date.Year, date.Month);
      }

      public static string Format(int year, int month)
      {
          return new DateOnly(year, month, 1).ToString(Pattern, CultureInfo.InvariantCulture);
      }
  }
  ```

- [ ] **Step 2: Write the read steps**

  `backend/tests/AcceptanceTests/StepDefinitions/ExpenseReportSteps.cs`:

  ```csharp
  using AcceptanceTests.Support;
  using Application.ExpenseReports;
  using Application.Paging;
  using Domain.Entities;
  using Infrastructure.Persistence;
  using Microsoft.EntityFrameworkCore;
  using Microsoft.Extensions.DependencyInjection;
  using Reqnroll;

  namespace AcceptanceTests.StepDefinitions;

  [Binding]
  public sealed class ExpenseReportSteps(ApiDriver api, KnownUsers knownUsers, KnownExpenseReports knownReports)
  {
      private const string ExpenseReportsPath = "/v1/expense-reports";

      /// <summary>
      /// "User" names a user arranged earlier in the scenario, still active at this point (deactivate or delete them
      /// afterwards); "Month" is written "2025-10".
      /// </summary>
      [Given("the following expense reports exist")]
      public async Task GivenTheFollowingExpenseReportsExistAsync(DataTable table)
      {
          var reports = new List<ExpenseReport>();
          using (var scope = TestEnvironment.Factory.Services.CreateScope())
          {
              var dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();
              foreach (var row in table.Rows)
              {
                  var userId = knownUsers.IdOf(row["User"]);
                  var user = await dbContext.Set<User>().SingleAsync(stored => stored.Id == userId);
                  reports.Add(ExpenseReport.Create(user, Months.Parse(row["Month"])));
              }

              dbContext.AddRange(reports);
              await dbContext.SaveChangesAsync();
          }

          foreach (var report in reports)
          {
              knownReports.Add(report.Title, report.Id);
          }
      }

      [When("I list the expense reports")]
      public Task WhenIListTheExpenseReportsAsync()
      {
          return api.GetAsync(ExpenseReportsPath);
      }

      [When("I list page {int} of the expense reports with {int} reports per page")]
      public Task WhenIListAPageOfTheExpenseReportsAsync(int page, int pageSize)
      {
          return api.GetAsync($"{ExpenseReportsPath}?page={page}&pageSize={pageSize}");
      }

      [When("I get the expense report {string}")]
      public Task WhenIGetTheExpenseReportAsync(string title)
      {
          return api.GetAsync($"{ExpenseReportsPath}/{knownReports.IdOf(title)}");
      }

      [When("I get an unknown expense report")]
      public Task WhenIGetAnUnknownExpenseReportAsync()
      {
          return api.GetAsync($"{ExpenseReportsPath}/{Guid.NewGuid()}");
      }

      [Then("the expense reports listed are")]
      public async Task ThenTheExpenseReportsListedAreAsync(DataTable table)
      {
          var reports = await api.ReadJsonAsync<PagedResult<ExpenseReportSummary>>();
          reports.Items
              .Select(report => (report.Title, Months.Format(report.Year, report.Month)))
              .ShouldBe(table.Rows.Select(row => (row["Title"], row["Month"])));
      }

      [Then("no expense report is listed")]
      public async Task ThenNoExpenseReportIsListedAsync()
      {
          var reports = await api.ReadJsonAsync<PagedResult<ExpenseReportSummary>>();
          reports.Items.ShouldBeEmpty();
          reports.TotalCount.ShouldBe(0);
      }

      [Then("the listing reports {int} expense reports in total")]
      public async Task ThenTheListingReportsExpenseReportsInTotalAsync(int totalCount)
      {
          var reports = await api.ReadJsonAsync<PagedResult<ExpenseReportSummary>>();
          reports.TotalCount.ShouldBe(totalCount);
      }

      [Then("the expense report returned is")]
      public async Task ThenTheExpenseReportReturnedIsAsync(DataTable table)
      {
          var report = await api.ReadJsonAsync<ExpenseReportDetails>();
          var expected = table.Rows.ShouldHaveSingleItem();
          report.ShouldSatisfyAllConditions(
              () => report.Title.ShouldBe(expected["Title"]),
              () => report.UserId.ShouldBe(knownUsers.IdOf(expected["User"])),
              () => Months.Format(report.Year, report.Month).ShouldBe(expected["Month"]));
      }
  }
  ```

- [ ] **Step 3: Write the read features**

  `backend/tests/AcceptanceTests/Features/ListExpenseReports.feature`:

  ```gherkin
  Feature: List expense reports

    Scenario: Listing returns the most recent months first, then sorts by title
      Given the following users exist
        | First name | Last name |
        | Juste      | Leblanc   |
        | Marc       | Assin     |
      And the following expense reports exist
        | User          | Month   |
        | Marc Assin    | 2024-12 |
        | Juste Leblanc | 2025-09 |
        | Marc Assin    | 2025-10 |
        | Juste Leblanc | 2025-10 |
      When I list the expense reports
      Then the response status is 200
      And the expense reports listed are
        | Title                          | Month   |
        | Juste Leblanc - Octobre 2025   | 2025-10 |
        | Marc Assin - Octobre 2025      | 2025-10 |
        | Juste Leblanc - Septembre 2025 | 2025-09 |
        | Marc Assin - Décembre 2024     | 2024-12 |

    Scenario: Listing returns no expense report when none exists
      When I list the expense reports
      Then the response status is 200
      And no expense report is listed

    Scenario: Listing returns the requested page and the total number of expense reports
      Given the following users exist
        | First name | Last name |
        | Juste      | Leblanc   |
      And the following expense reports exist
        | User          | Month   |
        | Juste Leblanc | 2025-08 |
        | Juste Leblanc | 2025-09 |
        | Juste Leblanc | 2025-10 |
      When I list page 2 of the expense reports with 2 reports per page
      Then the response status is 200
      And the expense reports listed are
        | Title                       | Month   |
        | Juste Leblanc - Août 2025   | 2025-08 |
      And the listing reports 3 expense reports in total

    Scenario Outline: Listing rejects an invalid page
      When I request "/v1/expense-reports?<query>"
      Then the response is a problem with status 400

      Examples:
        | query        |
        | page=0       |
        | pageSize=0   |
        | pageSize=101 |

    Scenario: The report of a deleted user stays listed with its title
      Given the following users exist
        | First name | Last name |
        | Juste      | Leblanc   |
      And the following expense reports exist
        | User          | Month   |
        | Juste Leblanc | 2025-10 |
      When I delete the user "Juste Leblanc"
      And I list the expense reports
      Then the expense reports listed are
        | Title                        | Month   |
        | Juste Leblanc - Octobre 2025 | 2025-10 |

    Scenario: Renaming the user does not change the title of their reports
      Given the following users exist
        | First name | Last name |
        | Juste      | Leblanc   |
      And the following expense reports exist
        | User          | Month   |
        | Juste Leblanc | 2025-10 |
      When I update the user "Juste Leblanc" with
        | First name | Last name | Street           | Postal code | City  | Monthly quota |
        | Justine    | Leblanc   | 12 rue de Rivoli | 75001       | Paris | 10            |
      And I list the expense reports
      Then the expense reports listed are
        | Title                        | Month   |
        | Juste Leblanc - Octobre 2025 | 2025-10 |
  ```

  `backend/tests/AcceptanceTests/Features/GetExpenseReport.feature`:

  ```gherkin
  Feature: Get an expense report

    Scenario: Getting an expense report returns its title, user and month
      Given the following users exist
        | First name | Last name |
        | Juste      | Leblanc   |
      And the following expense reports exist
        | User          | Month   |
        | Juste Leblanc | 2025-10 |
      When I get the expense report "Juste Leblanc - Octobre 2025"
      Then the response status is 200
      And the expense report returned is
        | Title                        | User          | Month   |
        | Juste Leblanc - Octobre 2025 | Juste Leblanc | 2025-10 |

    Scenario: The report of a deleted user can still be read
      Given the following users exist
        | First name | Last name |
        | Juste      | Leblanc   |
      And the following expense reports exist
        | User          | Month   |
        | Juste Leblanc | 2025-10 |
      When I delete the user "Juste Leblanc"
      And I get the expense report "Juste Leblanc - Octobre 2025"
      Then the response status is 200

    Scenario: Getting an unknown expense report answers not found
      When I get an unknown expense report
      Then the response is a problem with status 404

    Scenario: A malformed id answers not found
      When I request "/v1/expense-reports/not-a-guid"
      Then the response is a problem with status 404
  ```

  In `backend/tests/AcceptanceTests/Features/ApiDocumentation.feature`, add to the first outline's examples:

  ```gherkin
        | /v1/expense-reports       |
        | /v1/expense-reports/{id}  |
  ```

  and to the second outline's examples:

  ```gherkin
        | get    | /v1/expense-reports       | 400    |
        | get    | /v1/expense-reports/{id}  | 404    |
  ```

  Align the table pipes (the cleanup tool does not format Gherkin; keep the columns readable).

- [ ] **Step 4: Run the acceptance tests to verify they fail**

  Run: `dtk dotnet build backend/tests/AcceptanceTests`
  Expected: FAIL — `Application.ExpenseReports` does not exist (CS0234).

- [ ] **Step 5: Write the read models and the store interface**

  `backend/src/Application/ExpenseReports/ExpenseReportSummary.cs`:

  ```csharp
  namespace Application.ExpenseReports;

  public sealed record ExpenseReportSummary(Guid Id, string Title, int Year, int Month);
  ```

  `backend/src/Application/ExpenseReports/ExpenseReportDetails.cs`:

  ```csharp
  using Domain.Entities;

  namespace Application.ExpenseReports;

  public sealed record ExpenseReportDetails(Guid Id, string Title, Guid UserId, int Year, int Month)
  {
      public static ExpenseReportDetails From(ExpenseReport report)
      {
          return new ExpenseReportDetails(report.Id, report.Title, report.UserId, report.Year, report.Month);
      }
  }
  ```

  `backend/src/Application/ExpenseReports/IExpenseReportStore.cs`:

  ```csharp
  using Application.Paging;

  namespace Application.ExpenseReports;

  /// <summary>
  /// The expense reports feature's data access, implemented over the DbContext (which already is the unit of work).
  /// Reads never join the users, so the report of a deleted user stays visible.
  /// </summary>
  public interface IExpenseReportStore
  {
      /// <summary>The most recent months first, then by title.</summary>
      Task<PagedResult<ExpenseReportSummary>> ListAsync(PageRequest request, CancellationToken cancellationToken);

      Task<ExpenseReportDetails?> GetDetailsAsync(Guid id, CancellationToken cancellationToken);
  }
  ```

- [ ] **Step 6: Map the entity**

  `backend/src/Infrastructure/Persistence/ExpenseReportEntityTypeConfiguration.cs`:

  ```csharp
  using Domain.Entities;
  using Microsoft.EntityFrameworkCore;
  using Microsoft.EntityFrameworkCore.Metadata.Builders;

  namespace Infrastructure.Persistence;

  internal sealed class ExpenseReportEntityTypeConfiguration : IEntityTypeConfiguration<ExpenseReport>
  {
      /// <summary>The store recognises a duplicate report by the name of the index it violates.</summary>
      public const string UserYearAndMonthIndexName = "IX_expense_reports_UserId_Year_Month";

      public void Configure(EntityTypeBuilder<ExpenseReport> builder)
      {
          builder.ToTable("expense_reports");
          // Id is get-only, so EF Core does not discover it as the key by convention.
          builder.HasKey(report => report.Id);
          builder.Property(report => report.Title).HasMaxLength(ExpenseReport.TitleMaxLength);
          // No navigation: report reads never join the users, so the users' query filter cannot hide the report of a
          // deleted user. Restrict: users are only ever deleted logically.
          builder.HasOne<User>().WithMany().HasForeignKey(report => report.UserId).OnDelete(DeleteBehavior.Restrict);
          builder.HasIndex(report => new
              {
                  report.UserId, report.Year, report.Month
              })
              .IsUnique()
              .HasDatabaseName(UserYearAndMonthIndexName);
      }
  }
  ```

  In `backend/src/Infrastructure/Persistence/AppDbContext.cs`, replace the body of `OnModelCreating` after
  `base.OnModelCreating(modelBuilder);` with (RCS1201 asks for the chain):

  ```csharp
          modelBuilder
              .ApplyConfiguration(new UserEntityTypeConfiguration())
              .ApplyConfiguration(new ExpenseReportEntityTypeConfiguration());
  ```

  The seed is unchanged: no demo report.

- [ ] **Step 7: Write the store's reads**

  `backend/src/Infrastructure/ExpenseReports/ExpenseReportStore.cs`:

  ```csharp
  using Application.ExpenseReports;
  using Application.Paging;
  using Domain.Entities;
  using Infrastructure.Persistence;
  using Microsoft.EntityFrameworkCore;

  namespace Infrastructure.ExpenseReports;

  internal sealed class ExpenseReportStore(AppDbContext dbContext) : IExpenseReportStore
  {
      public async Task<PagedResult<ExpenseReportSummary>> ListAsync(
          PageRequest request,
          CancellationToken cancellationToken)
      {
          var reports = dbContext.Set<ExpenseReport>().AsNoTracking();
          var totalCount = await reports.CountAsync(cancellationToken).ConfigureAwait(false);
          var items = await reports
              .OrderByDescending(report => report.Year)
              .ThenByDescending(report => report.Month)
              .ThenBy(report => report.Title)
              // Reports sharing a title (two users with the same name) keep the same order from one page to the next.
              .ThenBy(report => report.Id)
              .Skip(request.Skip)
              .Take(request.PageSize)
              .Select(report => new ExpenseReportSummary(report.Id, report.Title, report.Year, report.Month))
              .ToListAsync(cancellationToken)
              .ConfigureAwait(false);

          return new PagedResult<ExpenseReportSummary>(items, request.Page, request.PageSize, totalCount);
      }

      public Task<ExpenseReportDetails?> GetDetailsAsync(Guid id, CancellationToken cancellationToken)
      {
          return dbContext.Set<ExpenseReport>()
              .AsNoTracking()
              .Where(report => report.Id == id)
              .Select(report => new ExpenseReportDetails(report.Id, report.Title, report.UserId, report.Year,
                  report.Month))
              .SingleOrDefaultAsync(cancellationToken);
      }
  }
  ```

  In `backend/src/Infrastructure/DependencyInjectionExtensions.cs`, add `using Application.ExpenseReports;` and
  `using Infrastructure.ExpenseReports;`, and register the store after `IUserStore`:

  ```csharp
          builder.Services.AddScoped<IExpenseReportStore, ExpenseReportStore>();
  ```

- [ ] **Step 8: Write the read endpoints**

  `backend/src/WebApi/Endpoints/ExpenseReportEndpoints.cs`:

  ```csharp
  using System.ComponentModel.DataAnnotations;
  using Application.Exceptions;
  using Application.ExpenseReports;
  using Application.Paging;
  using Microsoft.AspNetCore.Http.HttpResults;

  namespace WebApi.Endpoints;

  internal static class ExpenseReportEndpoints
  {
      public static IEndpointRouteBuilder MapExpenseReportEndpoints(this IEndpointRouteBuilder endpoints)
      {
          var reports = endpoints.NewVersionedApi("ExpenseReports")
              .MapGroup("/v{version:apiVersion}/expense-reports")
              .HasApiVersion(1.0);

          reports.MapGet("/", ListExpenseReportsAsync)
              .ProducesValidationProblem();
          reports.MapGet("/{id:guid}", GetExpenseReportAsync)
              .ProducesProblem(StatusCodes.Status404NotFound);

          return endpoints;
      }

      private static async Task<Ok<PagedResult<ExpenseReportSummary>>> ListExpenseReportsAsync(
          IExpenseReportStore expenseReportStore,
          CancellationToken cancellationToken,
          [Range(1, PageRequest.MaxPage)] int page = 1,
          [Range(1, PageRequest.MaxPageSize)] int pageSize = PageRequest.DefaultPageSize)
      {
          var reports = await expenseReportStore.ListAsync(new PageRequest(page, pageSize), cancellationToken)
              .ConfigureAwait(false);
          return TypedResults.Ok(reports);
      }

      private static async Task<Ok<ExpenseReportDetails>> GetExpenseReportAsync(
          Guid id,
          IExpenseReportStore expenseReportStore,
          CancellationToken cancellationToken)
      {
          var report = await expenseReportStore.GetDetailsAsync(id, cancellationToken).ConfigureAwait(false)
                       ?? throw new NotFoundException("ExpenseReport", id);
          return TypedResults.Ok(report);
      }
  }
  ```

  In `backend/src/WebApi/Program.cs`, after `app.MapUserEndpoints();` add:

  ```csharp
  app.MapExpenseReportEndpoints();
  ```

- [ ] **Step 9: Generate the migration**

  `dotnet ef` runs `Program` up to `builder.Build()`; the dummy connection string only satisfies the Aspire Npgsql
  component, no database is contacted.

  ```bash
  dotnet tool restore
  dotnet ef migrations add ExpenseReports \
    --project backend/src/Infrastructure \
    --startup-project backend/src/WebApi \
    --output-dir Persistence/Migrations \
    -- --ConnectionStrings:database "Host=localhost;Database=design;Username=design;Password=design"
  ```

  Expected: `Done.` The migration creates `expense_reports` (`Id` uuid, `UserId` uuid, `Year` integer, `Month`
  integer, `Title` character varying(250)), the foreign key `FK_expense_reports_users_UserId` with
  `ReferentialAction.Restrict`, and the unique index `IX_expense_reports_UserId_Year_Month` on
  `UserId, Year, Month`. Keep it as generated.

- [ ] **Step 10: Run the backend tiers**

  Run: `dtk dotnet test --project backend/tests/UnitTests`
  Expected: PASS.

  Run: `dtk dotnet test --project backend/tests/AcceptanceTests` (Docker required)
  Expected: PASS — the new List and Get scenarios and the two new API documentation rows included. Respawn now
  also clears `expense_reports` (it orders deletions by foreign key).

- [ ] **Step 11: Format and commit**

  ```bash
  dotnet jb cleanupcode N2JSoft.slnx --profile="ReformatAndReorder" --verbosity=ERROR
  git add -A backend
  git commit -m "feat(expense-reports): store expense reports and expose the list and the detail"
  ```

---

### Task 3: Create an expense report

**Files:**

- Create: `backend/src/Application/ExpenseReports/Commands/CreateExpenseReport.cs`,
  `backend/src/WebApi/Endpoints/Requests/CreateExpenseReportRequest.cs`
- Modify: `backend/src/Application/ExpenseReports/IExpenseReportStore.cs`,
  `backend/src/Infrastructure/ExpenseReports/ExpenseReportStore.cs`,
  `backend/src/Application/DependencyInjectionExtensions.cs`,
  `backend/src/WebApi/Endpoints/ExpenseReportEndpoints.cs`,
  `backend/tests/AcceptanceTests/StepDefinitions/HttpSteps.cs`,
  `backend/tests/AcceptanceTests/StepDefinitions/UserCommandSteps.cs`,
  `backend/tests/AcceptanceTests/Features/ApiDocumentation.feature`
- Test: `backend/tests/UnitTests/ExpenseReports/Commands/CreateExpenseReportHandlerTests.cs`,
  `backend/tests/AcceptanceTests/Support/JsonRequests.cs`,
  `backend/tests/AcceptanceTests/StepDefinitions/ExpenseReportCommandSteps.cs`,
  `backend/tests/AcceptanceTests/Features/CreateExpenseReport.feature`

**Interfaces:**

- Consumes: Task 1's `ExpenseReport.Create`, `ReportMonth`, `DuplicateExpenseReportException`,
  `InactiveUserException`; Task 2's `IExpenseReportStore`, `ExpenseReportDetails.From`,
  `ExpenseReportEntityTypeConfiguration.UserYearAndMonthIndexName`, `KnownExpenseReports`, `Months`. Existing
  `IUserStore.FindAsync` and the internal `UserStoreExtensions.GetAsync(this IUserStore, Guid, CancellationToken)`
  (throws `NotFoundException`).
- Produces:
  - `IExpenseReportStore.ExistsAsync(Guid userId, ReportMonth period, CancellationToken)`, `Add(ExpenseReport)`,
    `SaveChangesAsync(CancellationToken)` (throws `DuplicateExpenseReportException` on the unique index).
  - `public sealed record CreateExpenseReportCommand(Guid UserId, ReportMonth Period)` and
    `CreateExpenseReportHandler(IUserStore, IExpenseReportStore).HandleAsync(command, ct)` →
    `Task<ExpenseReportDetails>`.
  - `POST /v1/expense-reports` with body `{ userId, year, month }`.
  - Acceptance: `JsonRequests.With(JsonObject request, string path, string json)`; steps "I create an expense report
    for {string} for {string}", "I create an expense report for an unknown user", "I create an expense report whose
    <field> is <json>", "the response locates the created expense report", "two requests create an expense report
    for {string} for {string} at the same time", "the second request is refused as a duplicate", and in `HttpSteps`
    "the response is a business rule violation with the code {string}". Task 4 adds to `ExpenseReportCommandSteps`.

- [ ] **Step 1: Write the failing handler tests**

  `backend/tests/UnitTests/ExpenseReports/Commands/CreateExpenseReportHandlerTests.cs`:

  ```csharp
  using Application.Exceptions;
  using Application.ExpenseReports;
  using Application.ExpenseReports.Commands;
  using Application.Users;
  using Domain.Entities;
  using Domain.Exceptions;
  using Domain.ValueObjects;
  using NSubstitute;

  namespace UnitTests.ExpenseReports.Commands;

  public sealed class CreateExpenseReportHandlerTests
  {
      private static readonly ReportMonth October2025 = new(2025, 10);
      private readonly IExpenseReportStore _expenseReportStore = Substitute.For<IExpenseReportStore>();

      private readonly User _juste =
          User.Create("Juste", "Leblanc", new Address("12 rue de Rivoli", "75001", "Paris"), 10);

      private readonly IUserStore _userStore = Substitute.For<IUserStore>();

      public CreateExpenseReportHandlerTests()
      {
          _userStore.FindAsync(_juste.Id, Arg.Any<CancellationToken>()).Returns(_juste);
      }

      [Fact]
      public async Task Adds_a_report_for_the_user_and_the_month_then_saves()
      {
          ExpenseReport? added = null;
          _expenseReportStore.Add(Arg.Do<ExpenseReport>(report => added = report));

          await CreateHandler().HandleAsync(
              new CreateExpenseReportCommand(_juste.Id, October2025),
              TestContext.Current.CancellationToken);

          added.ShouldNotBeNull();
          added.UserId.ShouldBe(_juste.Id);
          added.Period.ShouldBe(October2025);
          Received.InOrder(() =>
          {
              _expenseReportStore.Add(added);
              _ = _expenseReportStore.SaveChangesAsync(Arg.Any<CancellationToken>());
          });
      }

      [Fact]
      public async Task Returns_the_details_of_the_new_report()
      {
          ExpenseReport? added = null;
          _expenseReportStore.Add(Arg.Do<ExpenseReport>(report => added = report));

          var details = await CreateHandler().HandleAsync(
              new CreateExpenseReportCommand(_juste.Id, October2025),
              TestContext.Current.CancellationToken);

          details.ShouldBe(new ExpenseReportDetails(
              added.ShouldNotBeNull().Id,
              "Juste Leblanc - Octobre 2025",
              _juste.Id,
              2025,
              10));
      }

      [Fact]
      public async Task Throws_not_found_for_an_unknown_user_without_saving()
      {
          await Should.ThrowAsync<NotFoundException>(() => CreateHandler().HandleAsync(
              new CreateExpenseReportCommand(Guid.NewGuid(), October2025),
              TestContext.Current.CancellationToken));

          await ShouldNotHaveSavedAsync();
      }

      [Fact]
      public async Task Refuses_a_second_report_for_the_same_user_and_month_without_saving()
      {
          _expenseReportStore.ExistsAsync(_juste.Id, October2025, Arg.Any<CancellationToken>()).Returns(true);

          var exception = await Should.ThrowAsync<DuplicateExpenseReportException>(() => CreateHandler().HandleAsync(
              new CreateExpenseReportCommand(_juste.Id, October2025),
              TestContext.Current.CancellationToken));

          exception.Code.ShouldBe("expense_report.already_exists");
          await ShouldNotHaveSavedAsync();
      }

      [Fact]
      public async Task Refuses_an_inactive_user_without_saving()
      {
          _juste.Deactivate();

          await Should.ThrowAsync<InactiveUserException>(() => CreateHandler().HandleAsync(
              new CreateExpenseReportCommand(_juste.Id, October2025),
              TestContext.Current.CancellationToken));

          await ShouldNotHaveSavedAsync();
      }

      private CreateExpenseReportHandler CreateHandler()
      {
          return new CreateExpenseReportHandler(_userStore, _expenseReportStore);
      }

      private async Task ShouldNotHaveSavedAsync()
      {
          _expenseReportStore.DidNotReceive().Add(Arg.Any<ExpenseReport>());
          await _expenseReportStore.DidNotReceive().SaveChangesAsync(Arg.Any<CancellationToken>());
      }
  }
  ```

- [ ] **Step 2: Run them to verify they fail**

  Run: `dtk dotnet build backend/tests/UnitTests`
  Expected: FAIL — `Application.ExpenseReports.Commands` does not exist, and `IExpenseReportStore` has no `Add`,
  `ExistsAsync` or `SaveChangesAsync`.

- [ ] **Step 3: Add the create members to the store interface**

  Replace `backend/src/Application/ExpenseReports/IExpenseReportStore.cs` with:

  ```csharp
  using Application.Paging;
  using Domain.Entities;
  using Domain.ValueObjects;

  namespace Application.ExpenseReports;

  /// <summary>
  /// The expense reports feature's data access, implemented over the DbContext (which already is the unit of work).
  /// Reads never join the users, so the report of a deleted user stays visible.
  /// </summary>
  public interface IExpenseReportStore
  {
      /// <summary>The most recent months first, then by title.</summary>
      Task<PagedResult<ExpenseReportSummary>> ListAsync(PageRequest request, CancellationToken cancellationToken);

      Task<ExpenseReportDetails?> GetDetailsAsync(Guid id, CancellationToken cancellationToken);

      Task<bool> ExistsAsync(Guid userId, ReportMonth period, CancellationToken cancellationToken);

      void Add(ExpenseReport report);

      /// <summary>
      /// Saves every change of the request, whatever store made it (they share one DbContext). Throws
      /// <see cref="Domain.Exceptions.DuplicateExpenseReportException" /> when a concurrent request created the same
      /// user's report for the same month first.
      /// </summary>
      Task SaveChangesAsync(CancellationToken cancellationToken);
  }
  ```

- [ ] **Step 4: Write the handler**

  `backend/src/Application/ExpenseReports/Commands/CreateExpenseReport.cs`:

  ```csharp
  using Application.Users;
  using Domain.Entities;
  using Domain.Exceptions;
  using Domain.ValueObjects;

  namespace Application.ExpenseReports.Commands;

  public sealed record CreateExpenseReportCommand(Guid UserId, ReportMonth Period);

  public sealed class CreateExpenseReportHandler(IUserStore userStore, IExpenseReportStore expenseReportStore)
  {
      public async Task<ExpenseReportDetails> HandleAsync(
          CreateExpenseReportCommand command,
          CancellationToken cancellationToken)
      {
          var user = await userStore.GetAsync(command.UserId, cancellationToken).ConfigureAwait(false);
          if (await expenseReportStore.ExistsAsync(user.Id, command.Period, cancellationToken).ConfigureAwait(false))
          {
              throw new DuplicateExpenseReportException();
          }

          var report = ExpenseReport.Create(user, command.Period);
          expenseReportStore.Add(report);
          await expenseReportStore.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
          return ExpenseReportDetails.From(report);
      }
  }
  ```

  In `backend/src/Application/DependencyInjectionExtensions.cs`, add `using Application.ExpenseReports.Commands;` and,
  after `services.AddScoped<DeleteUserHandler>();`:

  ```csharp
          services.AddScoped<CreateExpenseReportHandler>();
  ```

- [ ] **Step 5: Implement the store's create members**

  In `backend/src/Infrastructure/ExpenseReports/ExpenseReportStore.cs`, add the usings `Domain.Exceptions`,
  `Domain.ValueObjects` and `Npgsql`, then add after `GetDetailsAsync`:

  ```csharp
      public Task<bool> ExistsAsync(Guid userId, ReportMonth period, CancellationToken cancellationToken)
      {
          return dbContext.Set<ExpenseReport>().AnyAsync(
              report => report.UserId == userId && report.Year == period.Year && report.Month == period.Month,
              cancellationToken);
      }

      public void Add(ExpenseReport report)
      {
          dbContext.Set<ExpenseReport>().Add(report);
      }

      public async Task SaveChangesAsync(CancellationToken cancellationToken)
      {
          try
          {
              await dbContext.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
          }
          catch (DbUpdateException exception) when (IsDuplicateReport(exception))
          {
              // Another request created the same report between ExistsAsync and this save.
              throw new DuplicateExpenseReportException(exception);
          }
      }

      private static bool IsDuplicateReport(DbUpdateException exception)
      {
          return exception.InnerException is PostgresException
          {
              SqlState: PostgresErrorCodes.UniqueViolation,
              ConstraintName: ExpenseReportEntityTypeConfiguration.UserYearAndMonthIndexName,
          };
      }
  ```

  `Npgsql` comes transitively with `Aspire.Npgsql.EntityFrameworkCore.PostgreSQL`; no package to add.

- [ ] **Step 6: Write the request and the endpoint**

  `backend/src/WebApi/Endpoints/Requests/CreateExpenseReportRequest.cs`:

  ```csharp
  using System.ComponentModel.DataAnnotations;
  using Application.ExpenseReports.Commands;
  using Domain.ValueObjects;

  namespace WebApi.Endpoints.Requests;

  /// <summary>Nullable, so that a missing field answers 400 instead of binding to Guid.Empty or 0.</summary>
  public sealed record CreateExpenseReportRequest(
      [property: Required] Guid? UserId,
      [property: Required]
      [property: Range(ReportMonth.MinYear, ReportMonth.MaxYear)]
      int? Year,
      [property: Required]
      [property: Range(1, 12)]
      int? Month)
  {
      /// <summary>Called once validation has run, so every value is present.</summary>
      public CreateExpenseReportCommand ToCommand()
      {
          return new CreateExpenseReportCommand(UserId!.Value, new ReportMonth(Year!.Value, Month!.Value));
      }
  }
  ```

  In `backend/src/WebApi/Endpoints/ExpenseReportEndpoints.cs`, add the usings `Application.ExpenseReports.Commands`
  and `WebApi.Endpoints.Requests`, map the route after the `GET /{id:guid}` one:

  ```csharp
          // 404: the user is unknown or deleted. 422: the user is inactive, or already has a report for that month.
          reports.MapPost("/", CreateExpenseReportAsync)
              .ProducesValidationProblem()
              .ProducesProblem(StatusCodes.Status404NotFound)
              .ProducesProblem(StatusCodes.Status422UnprocessableEntity);
  ```

  and add the handler method after `GetExpenseReportAsync`:

  ```csharp
      private static async Task<Created<ExpenseReportDetails>> CreateExpenseReportAsync(
          CreateExpenseReportRequest request,
          CreateExpenseReportHandler handler,
          CancellationToken cancellationToken)
      {
          var report = await handler.HandleAsync(request.ToCommand(), cancellationToken).ConfigureAwait(false);
          return TypedResults.Created(new Uri($"/v1/expense-reports/{report.Id}", UriKind.Relative), report);
      }
  ```

- [ ] **Step 7: Run the unit tests**

  Run: `dtk dotnet test --project backend/tests/UnitTests`
  Expected: PASS — the five `CreateExpenseReportHandlerTests` included.

- [ ] **Step 8: Share the JSON field replacement**

  Create `backend/tests/AcceptanceTests/Support/JsonRequests.cs` with the `With` method moved out of
  `UserCommandSteps`:

  ```csharp
  using System.Text.Json.Nodes;

  namespace AcceptanceTests.Support;

  public static class JsonRequests
  {
      /// <summary>Replaces the field at <paramref name="path" /> ("address.city") with the parsed JSON value.</summary>
      public static JsonObject With(JsonObject request, string path, string json)
      {
          var segments = path.Split('.');
          var parent = segments[..^1].Aggregate(request, (node, segment) => node[segment]!.AsObject());
          parent[segments[^1]] = JsonNode.Parse(json);
          return request;
      }
  }
  ```

  In `backend/tests/AcceptanceTests/StepDefinitions/UserCommandSteps.cs`, delete the private static `With` method and
  replace its two calls `With(ValidRequest(), field, json)` with `JsonRequests.With(ValidRequest(), field, json)`.

- [ ] **Step 9: Write the business rule step**

  In `backend/tests/AcceptanceTests/StepDefinitions/HttpSteps.cs`, add after
  `ThenTheResponseIsAProblemWithStatusAsync`:

  ```csharp
      [Then("the response is a business rule violation with the code {string}")]
      public async Task ThenTheResponseIsABusinessRuleViolationWithTheCodeAsync(string code)
      {
          ((int)api.Response.StatusCode).ShouldBe(422);
          api.Response.Content.Headers.ContentType.ShouldNotBeNull().MediaType.ShouldBe("application/problem+json");
          // The buffered string can be read again by a later step; the stream cannot.
          using var problem = JsonDocument.Parse(await api.Response.Content.ReadAsStringAsync());
          problem.RootElement.GetProperty("code").GetString().ShouldBe(code);
      }
  ```

- [ ] **Step 10: Write the create steps**

  `backend/tests/AcceptanceTests/StepDefinitions/ExpenseReportCommandSteps.cs`:

  ```csharp
  using System.Text.Json.Nodes;
  using AcceptanceTests.Support;
  using Application.ExpenseReports;
  using Application.Users;
  using Domain.Entities;
  using Domain.Exceptions;
  using Domain.ValueObjects;
  using Microsoft.Extensions.DependencyInjection;
  using Reqnroll;

  namespace AcceptanceTests.StepDefinitions;

  [Binding]
  public sealed class ExpenseReportCommandSteps(ApiDriver api, KnownUsers knownUsers, KnownExpenseReports knownReports)
  {
      private const string ExpenseReportsPath = "/v1/expense-reports";

      /// <summary>What the second of two concurrent requests threw when it saved; null if it did not throw.</summary>
      private Exception? _secondSaveFailure;

      [When("I create an expense report for {string} for {string}")]
      public Task WhenICreateAnExpenseReportForAsync(string name, string month)
      {
          var period = Months.Parse(month);
          return CreateAsync(Request(knownUsers.IdOf(name), period.Year, period.Month));
      }

      [When("I create an expense report for an unknown user")]
      public Task WhenICreateAnExpenseReportForAnUnknownUserAsync()
      {
          return CreateAsync(Request(Guid.NewGuid(), 2025, 10));
      }

      /// <summary>
      /// Sends a valid request in which one field is replaced by a JSON value (null, 1999). The user id is random:
      /// validation answers before any user is looked up.
      /// </summary>
      [When(@"^I create an expense report whose (\S+) is (.+)$")]
      public Task WhenICreateAnExpenseReportWhoseFieldIsAsync(string field, string json)
      {
          return CreateAsync(JsonRequests.With(Request(Guid.NewGuid(), 2025, 10), field, json));
      }

      /// <summary>
      /// Both requests read (the user, then whether the report exists) before either saves, as when they run at the
      /// same time; the HTTP pipeline cannot interleave them deterministically, so this drives the stores directly.
      /// </summary>
      [When("two requests create an expense report for {string} for {string} at the same time")]
      public async Task WhenTwoRequestsCreateAnExpenseReportAtTheSameTimeAsync(string name, string month)
      {
          var userId = knownUsers.IdOf(name);
          var period = Months.Parse(month);
          using var first = TestEnvironment.Factory.Services.CreateScope();
          using var second = TestEnvironment.Factory.Services.CreateScope();
          var firstStore = await StageCreationAsync(first, userId, period);
          var secondStore = await StageCreationAsync(second, userId, period);

          await firstStore.SaveChangesAsync(TestContext.Current.CancellationToken);
          _secondSaveFailure = await Record.ExceptionAsync(() =>
              secondStore.SaveChangesAsync(TestContext.Current.CancellationToken));
      }

      [Then("the response locates the created expense report")]
      public async Task ThenTheResponseLocatesTheCreatedExpenseReportAsync()
      {
          var report = await api.ReadJsonAsync<ExpenseReportDetails>();
          api.Response.Headers.Location.ShouldNotBeNull().OriginalString.ShouldBe($"{ExpenseReportsPath}/{report.Id}");
      }

      [Then("the second request is refused as a duplicate")]
      public void ThenTheSecondRequestIsRefusedAsADuplicate()
      {
          _secondSaveFailure.ShouldBeOfType<DuplicateExpenseReportException>();
      }

      private static JsonObject Request(Guid userId, int year, int month)
      {
          return new JsonObject
          {
              ["userId"] = userId.ToString(), ["year"] = year, ["month"] = month,
          };
      }

      private static async Task<IExpenseReportStore> StageCreationAsync(
          IServiceScope scope,
          Guid userId,
          ReportMonth period)
      {
          var cancellationToken = TestContext.Current.CancellationToken;
          var user = (await scope.ServiceProvider.GetRequiredService<IUserStore>().FindAsync(userId, cancellationToken))
              .ShouldNotBeNull();
          var expenseReportStore = scope.ServiceProvider.GetRequiredService<IExpenseReportStore>();
          (await expenseReportStore.ExistsAsync(userId, period, cancellationToken)).ShouldBeFalse();
          expenseReportStore.Add(ExpenseReport.Create(user, period));
          return expenseReportStore;
      }

      private async Task CreateAsync(JsonObject request)
      {
          await api.SendAsync(HttpMethod.Post, ExpenseReportsPath, request);
          if (api.Response.IsSuccessStatusCode)
          {
              var report = await api.ReadJsonAsync<ExpenseReportDetails>();
              knownReports.Add(report.Title, report.Id);
          }
      }
  }
  ```

- [ ] **Step 11: Write the create feature**

  `backend/tests/AcceptanceTests/Features/CreateExpenseReport.feature`:

  ```gherkin
  Feature: Create an expense report

    Scenario: Creating an expense report answers with the titled report and where to find it
      Given the following users exist
        | First name | Last name |
        | Juste      | Leblanc   |
      When I create an expense report for "Juste Leblanc" for "2025-10"
      Then the response status is 201
      And the response locates the created expense report
      And the expense report returned is
        | Title                        | User          | Month   |
        | Juste Leblanc - Octobre 2025 | Juste Leblanc | 2025-10 |

    Scenario: The created expense report appears in the list
      Given the following users exist
        | First name | Last name |
        | Juste      | Leblanc   |
      When I create an expense report for "Juste Leblanc" for "2025-10"
      And I list the expense reports
      Then the expense reports listed are
        | Title                        | Month   |
        | Juste Leblanc - Octobre 2025 | 2025-10 |

    Scenario Outline: The first and the last month allowed are accepted
      Given the following users exist
        | First name | Last name |
        | Juste      | Leblanc   |
      When I create an expense report for "Juste Leblanc" for "<month>"
      Then the response status is 201

      Examples:
        | month   |
        | 2000-01 |
        | 2100-12 |

    Scenario: A user can have reports for several months, and several users for the same month
      Given the following users exist
        | First name | Last name |
        | Juste      | Leblanc   |
        | Marc       | Assin     |
      And the following expense reports exist
        | User          | Month   |
        | Juste Leblanc | 2025-10 |
      When I create an expense report for "Juste Leblanc" for "2025-11"
      Then the response status is 201
      When I create an expense report for "Marc Assin" for "2025-10"
      Then the response status is 201

    Scenario: A second report for the same user and month is refused
      Given the following users exist
        | First name | Last name |
        | Juste      | Leblanc   |
      And the following expense reports exist
        | User          | Month   |
        | Juste Leblanc | 2025-10 |
      When I create an expense report for "Juste Leblanc" for "2025-10"
      Then the response is a business rule violation with the code "expense_report.already_exists"

    Scenario: Two requests creating the same report at the same time create it once
      Given the following users exist
        | First name | Last name |
        | Juste      | Leblanc   |
      When two requests create an expense report for "Juste Leblanc" for "2025-10" at the same time
      Then the second request is refused as a duplicate
      When I list the expense reports
      Then the listing reports 1 expense reports in total

    Scenario: An inactive user cannot be assigned a report
      Given the following users exist
        | First name | Last name | Status   |
        | Juste      | Leblanc   | Inactive |
      When I create an expense report for "Juste Leblanc" for "2025-10"
      Then the response is a business rule violation with the code "expense_report.inactive_user"

    Scenario: A deleted user cannot be assigned a report
      Given the following users exist
        | First name | Last name | Status  |
        | Juste      | Leblanc   | Deleted |
      When I create an expense report for "Juste Leblanc" for "2025-10"
      Then the response is a problem with status 404

    Scenario: An unknown user cannot be assigned a report
      When I create an expense report for an unknown user
      Then the response is a problem with status 404

    Scenario Outline: Creating an expense report rejects an invalid <field>
      When I create an expense report whose <field> is <value>
      Then the response is a problem with status 400

      Examples:
        | field  | value          |
        | userId | null           |
        | userId | "not-a-guid"   |
        | year   | null           |
        | year   | 1999           |
        | year   | 2101           |
        | month  | null           |
        | month  | 0              |
        | month  | 13             |
  ```

  In `backend/tests/AcceptanceTests/Features/ApiDocumentation.feature`, add to the error outline's examples:

  ```gherkin
        | post   | /v1/expense-reports       | 400    |
        | post   | /v1/expense-reports       | 404    |
        | post   | /v1/expense-reports       | 422    |
  ```

  `userId: "not-a-guid"` is a JSON binding failure rather than a validation error; it answers a `400` ProblemDetails
  like the other rows (the framework sets the status, the status code pages write the body). If it answers anything
  else, read `ErrorHandlingExtensions` before changing the expectation.

- [ ] **Step 12: Run the acceptance tests**

  Run: `dtk dotnet test --project backend/tests/AcceptanceTests`
  Expected: PASS — every `CreateExpenseReport` scenario, the three new documentation rows, and the user scenarios
  that now use `JsonRequests.With`.

- [ ] **Step 13: Format and commit**

  ```bash
  dotnet jb cleanupcode N2JSoft.slnx --profile="ReformatAndReorder" --verbosity=ERROR
  git add -A backend
  git commit -m "feat(expense-reports): create a report for an active user, once per month"
  ```

---

### Task 4: Delete an expense report

**Files:**

- Create: `backend/src/Application/ExpenseReports/Commands/DeleteExpenseReport.cs`
- Modify: `backend/src/Application/Exceptions/NotFoundException.cs`,
  `backend/src/Application/ExpenseReports/IExpenseReportStore.cs`,
  `backend/src/Infrastructure/ExpenseReports/ExpenseReportStore.cs`,
  `backend/src/Application/DependencyInjectionExtensions.cs`,
  `backend/src/WebApi/Endpoints/ExpenseReportEndpoints.cs`,
  `backend/tests/AcceptanceTests/StepDefinitions/ExpenseReportSteps.cs`,
  `backend/tests/AcceptanceTests/StepDefinitions/ExpenseReportCommandSteps.cs`,
  `backend/tests/AcceptanceTests/Features/ApiDocumentation.feature`
- Test: `backend/tests/UnitTests/ExpenseReports/Commands/DeleteExpenseReportHandlerTests.cs`,
  `backend/tests/AcceptanceTests/Features/DeleteExpenseReport.feature`

**Interfaces:**

- Consumes: Tasks 2–3's `IExpenseReportStore`, `ExpenseReportStore`, `ExpenseReportCommandSteps` (its
  `_secondSaveFailure` field and `ExpenseReportsPath`), `KnownExpenseReports`.
- Produces:
  - `NotFoundException(string resource, object key, Exception? innerException = null)`.
  - `IExpenseReportStore.FindAsync(Guid, CancellationToken)` → `Task<ExpenseReport?>`, `Remove(ExpenseReport)`;
    `SaveChangesAsync` also throws `NotFoundException` for a report deleted concurrently.
  - `public sealed record DeleteExpenseReportCommand(Guid Id)`, `DeleteExpenseReportHandler(IExpenseReportStore)`.
  - `DELETE /v1/expense-reports/{id}`.
  - Acceptance steps "I delete the expense report {string}", "I delete an unknown expense report", "the expense
    report {string} is no longer stored", "two requests delete the expense report {string} at the same time", "the
    second request answers not found".

- [ ] **Step 1: Write the failing handler tests**

  `backend/tests/UnitTests/ExpenseReports/Commands/DeleteExpenseReportHandlerTests.cs`:

  ```csharp
  using Application.Exceptions;
  using Application.ExpenseReports;
  using Application.ExpenseReports.Commands;
  using Domain.Entities;
  using Domain.ValueObjects;
  using NSubstitute;

  namespace UnitTests.ExpenseReports.Commands;

  public sealed class DeleteExpenseReportHandlerTests
  {
      private readonly IExpenseReportStore _expenseReportStore = Substitute.For<IExpenseReportStore>();

      [Fact]
      public async Task Removes_the_report_then_saves()
      {
          var user = User.Create("Juste", "Leblanc", new Address("12 rue de Rivoli", "75001", "Paris"), 10);
          var report = ExpenseReport.Create(user, new ReportMonth(2025, 10));
          _expenseReportStore.FindAsync(report.Id, Arg.Any<CancellationToken>()).Returns(report);

          await CreateHandler().HandleAsync(
              new DeleteExpenseReportCommand(report.Id),
              TestContext.Current.CancellationToken);

          Received.InOrder(() =>
          {
              _expenseReportStore.Remove(report);
              _ = _expenseReportStore.SaveChangesAsync(Arg.Any<CancellationToken>());
          });
      }

      [Fact]
      public async Task Throws_not_found_for_an_unknown_report_without_saving()
      {
          await Should.ThrowAsync<NotFoundException>(() => CreateHandler().HandleAsync(
              new DeleteExpenseReportCommand(Guid.NewGuid()),
              TestContext.Current.CancellationToken));

          _expenseReportStore.DidNotReceive().Remove(Arg.Any<ExpenseReport>());
          await _expenseReportStore.DidNotReceive().SaveChangesAsync(Arg.Any<CancellationToken>());
      }

      private DeleteExpenseReportHandler CreateHandler()
      {
          return new DeleteExpenseReportHandler(_expenseReportStore);
      }
  }
  ```

- [ ] **Step 2: Run them to verify they fail**

  Run: `dtk dotnet build backend/tests/UnitTests`
  Expected: FAIL — `DeleteExpenseReportCommand` does not exist; `IExpenseReportStore` has no `FindAsync` or
  `Remove`.

- [ ] **Step 3: Let `NotFoundException` keep its cause**

  Replace `backend/src/Application/Exceptions/NotFoundException.cs` with:

  ```csharp
  namespace Application.Exceptions;

  public sealed class NotFoundException(string resource, object key, Exception? innerException = null)
      : Exception($"{resource} '{key}' was not found.", innerException);
  ```

- [ ] **Step 4: Add the delete members to the store interface**

  In `backend/src/Application/ExpenseReports/IExpenseReportStore.cs`, add after `ExistsAsync`:

  ```csharp
      /// <summary>The tracked report, to delete; null when unknown.</summary>
      Task<ExpenseReport?> FindAsync(Guid id, CancellationToken cancellationToken);
  ```

  after `Add`:

  ```csharp
      void Remove(ExpenseReport report);
  ```

  and replace the `SaveChangesAsync` summary with:

  ```csharp
      /// <summary>
      /// Saves every change of the request, whatever store made it (they share one DbContext). Throws
      /// <see cref="Domain.Exceptions.DuplicateExpenseReportException" /> when a concurrent request created the same
      /// user's report for the same month first, and <see cref="Exceptions.NotFoundException" /> when a concurrent
      /// request deleted a removed report first.
      /// </summary>
  ```

- [ ] **Step 5: Write the handler**

  `backend/src/Application/ExpenseReports/Commands/DeleteExpenseReport.cs`:

  ```csharp
  using Application.Exceptions;

  namespace Application.ExpenseReports.Commands;

  public sealed record DeleteExpenseReportCommand(Guid Id);

  /// <summary>A physical deletion: the report is gone for good, and from slice 4 its expenses go with it.</summary>
  public sealed class DeleteExpenseReportHandler(IExpenseReportStore expenseReportStore)
  {
      public async Task HandleAsync(DeleteExpenseReportCommand command, CancellationToken cancellationToken)
      {
          var report = await expenseReportStore.FindAsync(command.Id, cancellationToken).ConfigureAwait(false)
                       ?? throw new NotFoundException("ExpenseReport", command.Id);
          expenseReportStore.Remove(report);
          await expenseReportStore.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
      }
  }
  ```

  In `backend/src/Application/DependencyInjectionExtensions.cs`, after
  `services.AddScoped<CreateExpenseReportHandler>();`:

  ```csharp
          services.AddScoped<DeleteExpenseReportHandler>();
  ```

- [ ] **Step 6: Implement the store's delete members**

  In `backend/src/Infrastructure/ExpenseReports/ExpenseReportStore.cs`, add `using Application.Exceptions;`, then add
  after `ExistsAsync`:

  ```csharp
      public Task<ExpenseReport?> FindAsync(Guid id, CancellationToken cancellationToken)
      {
          return dbContext.Set<ExpenseReport>().SingleOrDefaultAsync(report => report.Id == id, cancellationToken);
      }
  ```

  after `Add`:

  ```csharp
      public void Remove(ExpenseReport report)
      {
          dbContext.Set<ExpenseReport>().Remove(report);
      }
  ```

  and, in `SaveChangesAsync`, add a first `catch` before the `DbUpdateException` one
  (`DbUpdateConcurrencyException` derives from it):

  ```csharp
          catch (DbUpdateConcurrencyException exception)
          {
              // Reports are never updated: a row that was not there to delete was deleted by another request.
              var entry = exception.Entries[0];
              throw new NotFoundException(entry.Metadata.ClrType.Name, entry.Property("Id").CurrentValue!, exception);
          }
  ```

- [ ] **Step 7: Write the endpoint**

  In `backend/src/WebApi/Endpoints/ExpenseReportEndpoints.cs`, map the route after the `POST` one:

  ```csharp
          reports.MapDelete("/{id:guid}", DeleteExpenseReportAsync)
              .ProducesProblem(StatusCodes.Status404NotFound);
  ```

  and add the handler method after `CreateExpenseReportAsync`:

  ```csharp
      private static async Task<NoContent> DeleteExpenseReportAsync(
          Guid id,
          DeleteExpenseReportHandler handler,
          CancellationToken cancellationToken)
      {
          await handler.HandleAsync(new DeleteExpenseReportCommand(id), cancellationToken).ConfigureAwait(false);
          return TypedResults.NoContent();
      }
  ```

- [ ] **Step 8: Run the unit tests**

  Run: `dtk dotnet test --project backend/tests/UnitTests`
  Expected: PASS — the two `DeleteExpenseReportHandlerTests` included.

- [ ] **Step 9: Write the delete steps**

  In `backend/tests/AcceptanceTests/StepDefinitions/ExpenseReportCommandSteps.cs`, add `using Application.Exceptions;`
  and these members (the `When` ones after the create `When` steps, the `Then` one after
  `ThenTheSecondRequestIsRefusedAsADuplicate`, the helper after `StageCreationAsync`):

  ```csharp
      [When("I delete the expense report {string}")]
      public Task WhenIDeleteTheExpenseReportAsync(string title)
      {
          return api.SendAsync(HttpMethod.Delete, $"{ExpenseReportsPath}/{knownReports.IdOf(title)}");
      }

      [When("I delete an unknown expense report")]
      public Task WhenIDeleteAnUnknownExpenseReportAsync()
      {
          return api.SendAsync(HttpMethod.Delete, $"{ExpenseReportsPath}/{Guid.NewGuid()}");
      }

      /// <summary>Both requests read the report before either saves, as when they run at the same time.</summary>
      [When("two requests delete the expense report {string} at the same time")]
      public async Task WhenTwoRequestsDeleteTheExpenseReportAtTheSameTimeAsync(string title)
      {
          var id = knownReports.IdOf(title);
          using var first = TestEnvironment.Factory.Services.CreateScope();
          using var second = TestEnvironment.Factory.Services.CreateScope();
          var firstStore = await StageDeletionAsync(first, id);
          var secondStore = await StageDeletionAsync(second, id);

          await firstStore.SaveChangesAsync(TestContext.Current.CancellationToken);
          _secondSaveFailure = await Record.ExceptionAsync(() =>
              secondStore.SaveChangesAsync(TestContext.Current.CancellationToken));
      }

      [Then("the second request answers not found")]
      public void ThenTheSecondRequestAnswersNotFound()
      {
          _secondSaveFailure.ShouldBeOfType<NotFoundException>();
      }

      private static async Task<IExpenseReportStore> StageDeletionAsync(IServiceScope scope, Guid id)
      {
          var expenseReportStore = scope.ServiceProvider.GetRequiredService<IExpenseReportStore>();
          var report = (await expenseReportStore.FindAsync(id, TestContext.Current.CancellationToken))
              .ShouldNotBeNull();
          expenseReportStore.Remove(report);
          return expenseReportStore;
      }
  ```

  In `backend/tests/AcceptanceTests/StepDefinitions/ExpenseReportSteps.cs`, add after
  `ThenTheExpenseReportReturnedIsAsync`:

  ```csharp
      [Then("the expense report {string} is no longer stored")]
      public async Task ThenTheExpenseReportIsNoLongerStoredAsync(string title)
      {
          var id = knownReports.IdOf(title);
          using var scope = TestEnvironment.Factory.Services.CreateScope();
          var dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();
          (await dbContext.Set<ExpenseReport>().AnyAsync(report => report.Id == id)).ShouldBeFalse();
      }
  ```

- [ ] **Step 10: Write the delete feature**

  `backend/tests/AcceptanceTests/Features/DeleteExpenseReport.feature`:

  ```gherkin
  Feature: Delete an expense report

    Scenario: A deleted expense report is gone for good
      Given the following users exist
        | First name | Last name |
        | Juste      | Leblanc   |
      And the following expense reports exist
        | User          | Month   |
        | Juste Leblanc | 2025-09 |
        | Juste Leblanc | 2025-10 |
      When I delete the expense report "Juste Leblanc - Octobre 2025"
      Then the response status is 204
      And the expense report "Juste Leblanc - Octobre 2025" is no longer stored
      When I list the expense reports
      Then the expense reports listed are
        | Title                          | Month   |
        | Juste Leblanc - Septembre 2025 | 2025-09 |

    Scenario Outline: A deleted expense report can no longer be read or deleted
      Given the following users exist
        | First name | Last name |
        | Juste      | Leblanc   |
      And the following expense reports exist
        | User          | Month   |
        | Juste Leblanc | 2025-10 |
      When I delete the expense report "Juste Leblanc - Octobre 2025"
      And I <action> the expense report "Juste Leblanc - Octobre 2025"
      Then the response is a problem with status 404

      Examples:
        | action |
        | get    |
        | delete |

    Scenario: Once deleted, the report can be created again for the same month
      Given the following users exist
        | First name | Last name |
        | Juste      | Leblanc   |
      And the following expense reports exist
        | User          | Month   |
        | Juste Leblanc | 2025-10 |
      When I delete the expense report "Juste Leblanc - Octobre 2025"
      And I create an expense report for "Juste Leblanc" for "2025-10"
      Then the response status is 201

    Scenario: Two requests deleting the same report at the same time delete it once
      Given the following users exist
        | First name | Last name |
        | Juste      | Leblanc   |
      And the following expense reports exist
        | User          | Month   |
        | Juste Leblanc | 2025-10 |
      When two requests delete the expense report "Juste Leblanc - Octobre 2025" at the same time
      Then the second request answers not found
      And the expense report "Juste Leblanc - Octobre 2025" is no longer stored

    Scenario: Deleting an unknown expense report answers not found
      When I delete an unknown expense report
      Then the response is a problem with status 404
  ```

  In `backend/tests/AcceptanceTests/Features/ApiDocumentation.feature`, add to the error outline's examples:

  ```gherkin
        | delete | /v1/expense-reports/{id}  | 404    |
  ```

- [ ] **Step 11: Run the acceptance tests**

  Run: `dtk dotnet test --project backend/tests/AcceptanceTests`
  Expected: PASS — every `DeleteExpenseReport` scenario and the new documentation row included.

- [ ] **Step 12: Format and commit**

  ```bash
  dotnet jb cleanupcode N2JSoft.slnx --profile="ReformatAndReorder" --verbosity=ERROR
  git add -A backend
  git commit -m "feat(expense-reports): delete a report physically"
  ```

---

### Task 5: List only the active users

**Files:**

- Modify: `backend/src/Application/Users/IUserStore.cs`, `backend/src/Infrastructure/Users/UserStore.cs`,
  `backend/src/WebApi/Endpoints/UserEndpoints.cs`,
  `backend/tests/AcceptanceTests/StepDefinitions/ApiDocumentationSteps.cs`,
  `backend/tests/AcceptanceTests/Features/ApiDocumentation.feature`
- Test: `backend/tests/AcceptanceTests/Features/ListUsers.feature`

**Interfaces:**

- Consumes: existing `IUserStore.ListAsync(PageRequest, CancellationToken)`, the generic step "I request {string}",
  the user steps "the following users exist" (with `Status` Active / Inactive / Deleted), "the users listed are",
  "the listing reports {int} users in total".
- Produces: `IUserStore.ListAsync(PageRequest request, bool? isActive, CancellationToken cancellationToken)`;
  `GET /v1/users?isActive=true|false` (omitted: every user). Step "the OpenAPI document declares the query parameter
  {string} for {string} on {string}". Task 6's `UsersApi.list(page, pageSize, isActive)` calls this.

- [ ] **Step 1: Write the failing scenarios**

  In `backend/tests/AcceptanceTests/Features/ListUsers.feature`, add `| isActive=maybe |` to the examples of
  "Listing rejects an invalid page", and append:

  ```gherkin

    Scenario Outline: Listing only the active, or only the inactive, users
      Given the following users exist
        | First name | Last name | Status   |
        | Juste      | Leblanc   | Active   |
        | Marc       | Assin     | Inactive |
        | Albert     | Leblanc   | Deleted  |
      When I request "/v1/users?isActive=<isActive>"
      Then the response status is 200
      And the users listed are
        | Name   |
        | <name> |
      And the listing reports 1 users in total

      Examples:
        | isActive | name          |
        | true     | Juste Leblanc |
        | false    | Marc Assin    |
  ```

  In `backend/tests/AcceptanceTests/StepDefinitions/ApiDocumentationSteps.cs`, add:

  ```csharp
      [Then("the OpenAPI document declares the query parameter {string} for {string} on {string}")]
      public async Task ThenTheOpenApiDocumentDeclaresTheQueryParameterAsync(string name, string method, string path)
      {
          var document = await api.Response.Content.ReadFromJsonAsync<JsonElement>();
          var parameters = document.GetProperty("paths").GetProperty(path).GetProperty(method).GetProperty("parameters");
          parameters.EnumerateArray()
              .ShouldContain(parameter => parameter.GetProperty("name").GetString() == name &&
                                          parameter.GetProperty("in").GetString() == "query",
                  $"{method} {path} declares no query parameter {name}");
      }
  ```

  and in `backend/tests/AcceptanceTests/Features/ApiDocumentation.feature`, after the error outline:

  ```gherkin

    Scenario: The v1 OpenAPI document describes the active filter of the users list
      Given the API runs in the Development environment
      When I request "/openapi/v1.json"
      Then the OpenAPI document declares the query parameter "isActive" for "get" on "/v1/users"
  ```

- [ ] **Step 2: Run them to verify they fail**

  Run: `dtk dotnet test --project backend/tests/AcceptanceTests`
  Expected: FAIL — `isActive=true` lists both Juste Leblanc and Marc Assin (the parameter is ignored),
  `isActive=maybe` answers `200`, and the document has no `isActive` parameter.

- [ ] **Step 3: Filter in the store**

  In `backend/src/Application/Users/IUserStore.cs`, replace the `ListAsync` declaration with:

  ```csharp
      /// <summary>Every user when <paramref name="isActive" /> is null, otherwise only the active or the inactive ones.</summary>
      Task<PagedResult<UserSummary>> ListAsync(PageRequest request, bool? isActive, CancellationToken cancellationToken);
  ```

  In `backend/src/Infrastructure/Users/UserStore.cs`, replace the start of `ListAsync`, up to and including
  `var users = dbContext.Set<User>().AsNoTracking();`, with:

  ```csharp
      public async Task<PagedResult<UserSummary>> ListAsync(
          PageRequest request,
          bool? isActive,
          CancellationToken cancellationToken)
      {
          var users = dbContext.Set<User>().AsNoTracking();
          if (isActive is { } active)
          {
              users = users.Where(user => user.IsActive == active);
          }
  ```

  The count that follows now counts the filtered users, so `totalCount` matches the filter.

- [ ] **Step 4: Accept the query parameter**

  In `backend/src/WebApi/Endpoints/UserEndpoints.cs`, give `ListUsersAsync` a last parameter and pass it on:

  ```csharp
          [Range(1, PageRequest.MaxPageSize)] int pageSize = PageRequest.DefaultPageSize,
          bool? isActive = null)
      {
          var users = await userStore.ListAsync(new PageRequest(page, pageSize), isActive, cancellationToken)
  ```

- [ ] **Step 5: Run the backend tiers**

  Run: `dtk dotnet test --project backend/tests/UnitTests`
  Expected: PASS.

  Run: `dtk dotnet test --project backend/tests/AcceptanceTests`
  Expected: PASS — `isActive=maybe` is a binding failure answered `400` (like `/v1/users/not-a-guid` answers `404`).

- [ ] **Step 6: Format and commit**

  ```bash
  dotnet jb cleanupcode N2JSoft.slnx --profile="ReformatAndReorder" --verbosity=ERROR
  git add -A backend
  git commit -m "feat(users): filter the users list by active status"
  ```

---

### Task 6: Frontend API clients

**Files:**

- Create: `frontend/src/app/core/problem-details.ts`,
  `frontend/src/app/features/expense-reports/expense-report-summary.ts`,
  `frontend/src/app/features/expense-reports/expense-report-details.ts`,
  `frontend/src/app/features/expense-reports/expense-reports.api.ts`
- Modify: `frontend/src/app/core/paged-result.ts`, `frontend/src/app/features/users/user-list.page.ts`,
  `frontend/src/app/features/users/users.api.ts`
- Test: `frontend/src/app/core/problem-details.spec.ts`,
  `frontend/src/app/features/expense-reports/expense-reports.api.spec.ts`,
  `frontend/src/app/features/users/users.api.spec.ts`

**Interfaces:**

- Consumes: Tasks 2–5's API: `GET/POST /api/v1/expense-reports`, `GET/DELETE /api/v1/expense-reports/:id`,
  `GET /api/v1/users?isActive=true`, the `422` body `{ status, code, … }`.
- Produces:
  - `pageCountOf(page: PagedResult<unknown>): number` in `core/paged-result.ts`.
  - `businessRuleCode(error: unknown): string | undefined` in `core/problem-details.ts`.
  - `ExpenseReportSummary { id; title; year; month }`, `ExpenseReportDetails { id; title; userId; year; month }`,
    `CreateExpenseReport { userId; year; month }`.
  - `ExpenseReportsApi`: `list(page, pageSize)`, `get(id)`, `create(report: CreateExpenseReport)` →
    `Observable<ExpenseReportDetails>`, `delete(id)` → `Observable<void>`.
  - `UsersApi.list(page, pageSize, isActive?)`, `UsersApi.listAllActive(): Observable<UserSummary[]>`.

- [ ] **Step 1: Write the failing specs**

  `frontend/src/app/core/problem-details.spec.ts`:

  ```ts
  import { HttpErrorResponse } from '@angular/common/http';
  import { businessRuleCode } from './problem-details';

  describe('businessRuleCode', () => {
    it('reads the code of a 422', () => {
      const error = new HttpErrorResponse({
        status: 422,
        error: { status: 422, code: 'expense_report.already_exists' },
      });

      expect(businessRuleCode(error)).toBe('expense_report.already_exists');
    });

    it('ignores a code sent with another status', () => {
      const error = new HttpErrorResponse({ status: 400, error: { code: 'expense_report.already_exists' } });

      expect(businessRuleCode(error)).toBeUndefined();
    });

    it.each([null, { status: 422 }, { code: 42 }])('is undefined for a 422 whose body is %j', (body) => {
      expect(businessRuleCode(new HttpErrorResponse({ status: 422, error: body }))).toBeUndefined();
    });

    it('is undefined for an error that is not an HTTP response', () => {
      expect(businessRuleCode(new Error('Service unavailable'))).toBeUndefined();
    });
  });
  ```

  `frontend/src/app/features/expense-reports/expense-reports.api.spec.ts`:

  ```ts
  import { provideHttpClient } from '@angular/common/http';
  import {
    HttpTestingController,
    provideHttpClientTesting,
  } from '@angular/common/http/testing';
  import { TestBed } from '@angular/core/testing';
  import { PagedResult } from '../../core/paged-result';
  import { CreateExpenseReport, ExpenseReportDetails } from './expense-report-details';
  import { ExpenseReportSummary } from './expense-report-summary';
  import { ExpenseReportsApi } from './expense-reports.api';

  const id = '0199d5a8-7c1e-7b3a-9f2e-3c4d5e6f7a8b';
  const userId = '7d9f5f3c-2f1e-4a43-9a43-4d1f0c6a1b2e';
  const october: CreateExpenseReport = { userId, year: 2025, month: 10 };
  const details: ExpenseReportDetails = { ...october, id, title: 'Juste Leblanc - Octobre 2025' };

  describe('ExpenseReportsApi', () => {
    let api: ExpenseReportsApi;
    let http: HttpTestingController;

    beforeEach(() => {
      TestBed.configureTestingModule({
        providers: [provideHttpClient(), provideHttpClientTesting()],
      });
      api = TestBed.inject(ExpenseReportsApi);
      http = TestBed.inject(HttpTestingController);
    });

    afterEach(() => http.verify());

    it('lists a page of reports with GET /api/v1/expense-reports', () => {
      const reports: PagedResult<ExpenseReportSummary> = {
        items: [{ id, title: 'Juste Leblanc - Octobre 2025', year: 2025, month: 10 }],
        page: 2,
        pageSize: 20,
        totalCount: 21,
      };
      let result: PagedResult<ExpenseReportSummary> | undefined;

      api.list(2, 20).subscribe((value) => (result = value));

      const request = http.expectOne('/api/v1/expense-reports?page=2&pageSize=20');
      expect(request.request.method).toBe('GET');
      request.flush(reports);
      expect(result).toEqual(reports);
    });

    it('gets a report with GET /api/v1/expense-reports/:id', () => {
      let result: ExpenseReportDetails | undefined;

      api.get(id).subscribe((value) => (result = value));

      const request = http.expectOne(`/api/v1/expense-reports/${id}`);
      expect(request.request.method).toBe('GET');
      request.flush(details);
      expect(result).toEqual(details);
    });

    it('creates a report with POST /api/v1/expense-reports', () => {
      let result: ExpenseReportDetails | undefined;

      api.create(october).subscribe((value) => (result = value));

      const request = http.expectOne('/api/v1/expense-reports');
      expect(request.request.method).toBe('POST');
      expect(request.request.body).toEqual(october);
      request.flush(details, { status: 201, statusText: 'Created' });
      expect(result).toEqual(details);
    });

    it('deletes a report with DELETE /api/v1/expense-reports/:id', () => {
      api.delete(id).subscribe();

      const request = http.expectOne(`/api/v1/expense-reports/${id}`);
      expect(request.request.method).toBe('DELETE');
      request.flush(null, { status: 204, statusText: 'No Content' });
    });
  });
  ```

  In `frontend/src/app/features/users/users.api.spec.ts`, add after the `details` constant:

  ```ts
  const justeSummary: UserSummary = {
    id: '1',
    name: 'Juste Leblanc',
    city: 'Paris',
    monthlyExpenseQuota: 10,
    isActive: true,
  };
  const marcSummary: UserSummary = { ...justeSummary, id: '2', name: 'Marc Assin', city: 'Lyon' };

  function activePage(
    items: UserSummary[],
    page: number,
    totalCount: number
  ): PagedResult<UserSummary> {
    return { items, page, pageSize: 100, totalCount };
  }
  ```

  and these tests at the end of the `describe`:

  ```ts
    it('lists only the active users with ?isActive=true', () => {
      api.list(1, 20, true).subscribe();

      const request = http.expectOne('/api/v1/users?page=1&pageSize=20&isActive=true');
      expect(request.request.method).toBe('GET');
      request.flush(activePage([], 1, 0));
    });

    it('reads every page of active users, 100 at a time', () => {
      let result: UserSummary[] | undefined;

      api.listAllActive().subscribe((value) => (result = value));

      http
        .expectOne('/api/v1/users?page=1&pageSize=100&isActive=true')
        .flush(activePage([justeSummary], 1, 101));
      http
        .expectOne('/api/v1/users?page=2&pageSize=100&isActive=true')
        .flush(activePage([marcSummary], 2, 101));
      expect(result).toEqual([justeSummary, marcSummary]);
    });

    it('asks for one page only when every active user fits in it', () => {
      let result: UserSummary[] | undefined;

      api.listAllActive().subscribe((value) => (result = value));

      http
        .expectOne('/api/v1/users?page=1&pageSize=100&isActive=true')
        .flush(activePage([justeSummary], 1, 1));
      expect(result).toEqual([justeSummary]);
    });

    it('stops at an empty page, even when the total says there are more', () => {
      let result: UserSummary[] | undefined;

      api.listAllActive().subscribe((value) => (result = value));

      http
        .expectOne('/api/v1/users?page=1&pageSize=100&isActive=true')
        .flush(activePage([], 1, 250));
      expect(result).toEqual([]);
    });
  ```

  (`afterEach(() => http.verify())` fails a test that sends a request it did not expect.)

- [ ] **Step 2: Run the specs to verify they fail**

  Run: `(cd frontend && npx nx test n2f --skip-nx-cache)`
  Expected: FAIL — `./problem-details`, `./expense-reports.api` and `./expense-report-details` cannot be resolved;
  `listAllActive` is not a function.

- [ ] **Step 3: Share the page count**

  Replace `frontend/src/app/core/paged-result.ts` with:

  ```ts
  export interface PagedResult<T> {
    items: T[];
    page: number;
    pageSize: number;
    totalCount: number;
  }

  /** At least 1: an empty list still shows as page 1 of 1. */
  export function pageCountOf(page: PagedResult<unknown>): number {
    return Math.max(1, Math.ceil(page.totalCount / page.pageSize));
  }
  ```

  In `frontend/src/app/features/users/user-list.page.ts`, delete the local `pageCountOf` function (and its comment)
  and import it: `import { PagedResult, pageCountOf } from '../../core/paged-result';`.

- [ ] **Step 4: Read the business rule code**

  `frontend/src/app/core/problem-details.ts`:

  ```ts
  import { HttpErrorResponse } from '@angular/common/http';

  /**
   * The stable `code` of a 422, a business rule the API refused (e.g. "expense_report.already_exists"); undefined for
   * any other error.
   */
  export function businessRuleCode(error: unknown): string | undefined {
    if (!(error instanceof HttpErrorResponse) || error.status !== 422) {
      return undefined;
    }
    const code: unknown = (error.error as { code?: unknown } | null)?.code;
    return typeof code === 'string' ? code : undefined;
  }
  ```

- [ ] **Step 5: Write the expense report client**

  `frontend/src/app/features/expense-reports/expense-report-summary.ts`:

  ```ts
  export interface ExpenseReportSummary {
    id: string;
    /** "Juste Leblanc - Octobre 2025", fixed when the report was created. */
    title: string;
    year: number;
    /** 1 for January to 12 for December. */
    month: number;
  }
  ```

  `frontend/src/app/features/expense-reports/expense-report-details.ts`:

  ```ts
  /** The body of a report creation. */
  export interface CreateExpenseReport {
    userId: string;
    year: number;
    /** 1 for January to 12 for December. */
    month: number;
  }

  export interface ExpenseReportDetails extends CreateExpenseReport {
    id: string;
    /** "Juste Leblanc - Octobre 2025", fixed when the report was created. */
    title: string;
  }
  ```

  `frontend/src/app/features/expense-reports/expense-reports.api.ts`:

  ```ts
  import { HttpClient } from '@angular/common/http';
  import { inject, Injectable } from '@angular/core';
  import { Observable } from 'rxjs';
  import { PagedResult } from '../../core/paged-result';
  import { CreateExpenseReport, ExpenseReportDetails } from './expense-report-details';
  import { ExpenseReportSummary } from './expense-report-summary';

  const expenseReportsUrl = '/api/v1/expense-reports';

  @Injectable({ providedIn: 'root' })
  export class ExpenseReportsApi {
    private readonly http = inject(HttpClient);

    list(page: number, pageSize: number): Observable<PagedResult<ExpenseReportSummary>> {
      return this.http.get<PagedResult<ExpenseReportSummary>>(expenseReportsUrl, {
        params: { page, pageSize },
      });
    }

    get(id: string): Observable<ExpenseReportDetails> {
      return this.http.get<ExpenseReportDetails>(`${expenseReportsUrl}/${id}`);
    }

    /** Refused with a 422 when the user is inactive or already has a report for that month. */
    create(report: CreateExpenseReport): Observable<ExpenseReportDetails> {
      return this.http.post<ExpenseReportDetails>(expenseReportsUrl, report);
    }

    delete(id: string): Observable<void> {
      return this.http.delete<void>(`${expenseReportsUrl}/${id}`);
    }
  }
  ```

- [ ] **Step 6: Read the active users**

  In `frontend/src/app/features/users/users.api.ts`, import `EMPTY`, `expand` and `reduce` from `rxjs` (next to
  `Observable`), add after `const usersUrl = '/api/v1/users';`:

  ```ts
  /** The API's PageRequest.MaxPageSize: the fewest requests to read every user. */
  const largestPageSize = 100;
  ```

  and replace `list` with:

  ```ts
    /** `isActive` lists only the active (true) or the inactive (false) users; omitted, every user. */
    list(page: number, pageSize: number, isActive?: boolean): Observable<PagedResult<UserSummary>> {
      const params = isActive === undefined ? { page, pageSize } : { page, pageSize, isActive };
      return this.http.get<PagedResult<UserSummary>>(usersUrl, { params });
    }

    /** Every active user, in the API's order (last name, then first name), read 100 at a time. */
    listAllActive(): Observable<UserSummary[]> {
      return this.list(1, largestPageSize, true).pipe(
        // Stops at an empty page too: users deleted meanwhile must not make it ask for pages forever.
        expand((page) =>
          page.items.length > 0 && page.page * page.pageSize < page.totalCount
            ? this.list(page.page + 1, largestPageSize, true)
            : EMPTY
        ),
        reduce((users: UserSummary[], page) => [...users, ...page.items], [])
      );
    }
  ```

- [ ] **Step 7: Run the frontend checks**

  Run: `(cd frontend && npx nx run-many -t lint test --skip-nx-cache)`
  Expected: PASS — the new specs included, the users list spec unchanged and green.

- [ ] **Step 8: Commit**

  ```bash
  (cd frontend && npx prettier --write src/app)
  git add -A frontend/src
  git commit -m "feat(expense-reports): add the expense reports API client and the active users list"
  ```

---

### Task 7: Expense reports page

**Files:**

- Create: `frontend/src/app/features/expense-reports/confirm-deletion.ts`,
  `frontend/src/app/features/expense-reports/expense-report-list.page.ts`
- Modify: `frontend/src/app/app.routes.ts`, `frontend/src/app/app.html`
- Test: `frontend/src/app/features/expense-reports/expense-report-list.page.spec.ts`,
  `frontend/src/app/app.routes.spec.ts`, `frontend/src/app/app.spec.ts`

**Interfaces:**

- Consumes: Task 6's `ExpenseReportsApi` (`list`, `create`, `delete`), `UsersApi.listAllActive`,
  `businessRuleCode`, `pageCountOf`, `PagedResult`, `ExpenseReportSummary`, `UserSummary` (`id`, `name`).
- Produces: `confirmDeletion(title: string): boolean`; the default-exported standalone `ExpenseReportListPage` at
  `/expense-reports` (also `''` and `**`). After a creation it navigates to `['/expense-reports', id]`, which Task 8
  routes.

- [ ] **Step 1: Write the failing page spec**

  `frontend/src/app/features/expense-reports/expense-report-list.page.spec.ts`:

  ```ts
  import { HttpErrorResponse } from '@angular/common/http';
  import { TestBed } from '@angular/core/testing';
  import { provideRouter, Router } from '@angular/router';
  import { RouterTestingHarness } from '@angular/router/testing';
  import { Observable, of, throwError } from 'rxjs';
  import { PagedResult } from '../../core/paged-result';
  import { UserSummary } from '../users/user-summary';
  import { UsersApi } from '../users/users.api';
  import { ExpenseReportDetails } from './expense-report-details';
  import ExpenseReportListPage from './expense-report-list.page';
  import { ExpenseReportSummary } from './expense-report-summary';
  import { ExpenseReportsApi } from './expense-reports.api';

  const juste: UserSummary = {
    id: 'u1',
    name: 'Juste Leblanc',
    city: 'Paris',
    monthlyExpenseQuota: 10,
    isActive: true,
  };
  const october: ExpenseReportSummary = {
    id: '1',
    title: 'Juste Leblanc - Octobre 2025',
    year: 2025,
    month: 10,
  };
  const september: ExpenseReportSummary = {
    id: '2',
    title: 'Juste Leblanc - Septembre 2025',
    year: 2025,
    month: 9,
  };
  const monthMessage = 'Indiquez un mois au format AAAA-MM, entre 2000-01 et 2100-12.';

  function pageOf(
    items: ExpenseReportSummary[],
    page = 1,
    totalCount = items.length
  ): PagedResult<ExpenseReportSummary> {
    return { items, page, pageSize: 20, totalCount };
  }

  function fakeReportsApi(
    list: (page: number) => Observable<PagedResult<ExpenseReportSummary>> = () =>
      of(pageOf([october, september]))
  ) {
    return {
      list: vi.fn(list),
      create: vi.fn(
        (): Observable<ExpenseReportDetails> =>
          of({ id: '9', title: 'Juste Leblanc - Octobre 2025', userId: 'u1', year: 2025, month: 10 })
      ),
      delete: vi.fn((): Observable<void> => of(undefined)),
    };
  }

  function fakeUsersApi(listAllActive: () => Observable<UserSummary[]> = () => of([juste])) {
    return { listAllActive: vi.fn(listAllActive) };
  }

  async function render(reportsApi = fakeReportsApi(), usersApi = fakeUsersApi()) {
    TestBed.configureTestingModule({
      providers: [
        provideRouter([
          { path: 'expense-reports', component: ExpenseReportListPage },
          { path: 'expense-reports/:id', children: [] },
        ]),
        { provide: ExpenseReportsApi, useValue: reportsApi },
        { provide: UsersApi, useValue: usersApi },
      ],
    });
    const harness = await RouterTestingHarness.create();
    await harness.navigateByUrl('/expense-reports', ExpenseReportListPage);
    harness.detectChanges();
    const element = () => harness.routeNativeElement as HTMLElement;
    const button = (label: string, rowIndex?: number) => {
      const scope =
        rowIndex === undefined ? element() : element().querySelectorAll('tbody tr')[rowIndex];
      const found = Array.from(scope.querySelectorAll('button')).find(
        (candidate) => candidate.textContent?.trim() === label
      );
      if (!found) {
        throw new Error(`No button "${label}"`);
      }
      return found;
    };

    return {
      element,
      button,
      text: () => element().textContent ?? '',
      rows: () =>
        Array.from(element().querySelectorAll('tbody tr')).map((row) =>
          row.querySelector('td')?.textContent?.trim()
        ),
      linkOf: (rowIndex: number) =>
        element().querySelectorAll('tbody tr')[rowIndex].querySelector('a')?.getAttribute('href'),
      alerts: () =>
        Array.from(element().querySelectorAll('[role="alert"]')).map((alert) =>
          alert.textContent?.trim()
        ),
      errors: () =>
        Array.from(element().querySelectorAll('.field-error')).map((error) =>
          error.textContent?.trim()
        ),
      options: () =>
        Array.from(element().querySelectorAll('#userId option')).map((option) =>
          option.textContent?.trim()
        ),
      month: () => element().querySelector('#month') as HTMLInputElement,
      click: (label: string, rowIndex?: number) => {
        button(label, rowIndex).click();
        harness.detectChanges();
      },
      choose: (userId: string) => {
        const select = element().querySelector('#userId') as HTMLSelectElement;
        select.value = userId;
        select.dispatchEvent(new Event('change'));
        harness.detectChanges();
      },
      fillMonth: (value: string) => {
        const input = element().querySelector('#month') as HTMLInputElement;
        input.value = value;
        input.dispatchEvent(new Event('input'));
        harness.detectChanges();
      },
      submit: async () => {
        element().querySelector('form')?.dispatchEvent(new Event('submit'));
        harness.detectChanges();
        await harness.fixture.whenStable();
      },
    };
  }

  describe('ExpenseReportListPage', () => {
    afterEach(() => {
      vi.useRealTimers();
      vi.restoreAllMocks();
    });

    describe('list', () => {
      it('shows each report title, linked to its page', async () => {
        const page = await render();

        expect(page.rows()).toEqual(['Juste Leblanc - Octobre 2025', 'Juste Leblanc - Septembre 2025']);
        expect(page.linkOf(0)).toBe('/expense-reports/1');
      });

      it('asks the API for the first page of 20 reports', async () => {
        const reportsApi = fakeReportsApi();

        await render(reportsApi);

        expect(reportsApi.list).toHaveBeenCalledWith(1, 20);
      });

      it('says so when there is no report', async () => {
        const page = await render(fakeReportsApi(() => of(pageOf([]))));

        expect(page.element().querySelector('table')).toBeNull();
        expect(page.text()).toContain('Aucune note de frais.');
      });

      it('says so when the reports cannot be loaded', async () => {
        const page = await render(
          fakeReportsApi(() => throwError(() => new Error('Service unavailable')))
        );

        expect(page.alerts()).toContain('Impossible de charger les notes de frais.');
      });

      it('pages through the reports', async () => {
        const reportsApi = fakeReportsApi((pageNumber) =>
          of(pageOf(pageNumber === 1 ? [october] : [september], pageNumber, 45))
        );
        const page = await render(reportsApi);
        expect(page.text()).toContain('Page 1 sur 3');

        page.click('Suivant');

        expect(reportsApi.list).toHaveBeenLastCalledWith(2, 20);
        expect(page.rows()).toEqual(['Juste Leblanc - Septembre 2025']);
        expect(page.text()).toContain('Page 2 sur 3');
      });

      it('deletes a report once the deletion is confirmed, then reloads the page', async () => {
        const confirm = vi.spyOn(window, 'confirm').mockReturnValue(true);
        const reportsApi = fakeReportsApi();
        const page = await render(reportsApi);

        page.click('Supprimer', 0);

        expect(confirm).toHaveBeenCalledWith(
          'Supprimer la note de frais « Juste Leblanc - Octobre 2025 » ? Ses dépenses seront supprimées avec elle.'
        );
        expect(reportsApi.delete).toHaveBeenCalledWith('1');
        expect(reportsApi.list).toHaveBeenCalledTimes(2);
      });

      it('keeps the report when the deletion is cancelled', async () => {
        vi.spyOn(window, 'confirm').mockReturnValue(false);
        const reportsApi = fakeReportsApi();
        const page = await render(reportsApi);

        page.click('Supprimer', 0);

        expect(reportsApi.delete).not.toHaveBeenCalled();
      });

      it('goes back one page when the last report of the last page is deleted', async () => {
        vi.spyOn(window, 'confirm').mockReturnValue(true);
        let deleted = false;
        const reportsApi = fakeReportsApi((pageNumber) => {
          if (pageNumber === 1) {
            return of(pageOf([october], 1, deleted ? 20 : 21));
          }
          return of(deleted ? pageOf([], 2, 20) : pageOf([september], 2, 21));
        });
        reportsApi.delete.mockImplementation(() => {
          deleted = true;
          return of(undefined);
        });
        const page = await render(reportsApi);
        page.click('Suivant');

        page.click('Supprimer', 0);

        expect(page.rows()).toEqual(['Juste Leblanc - Octobre 2025']);
        expect(page.text()).toContain('Page 1 sur 1');
      });

      it('says so when a deletion fails, and reloads the page', async () => {
        vi.spyOn(window, 'confirm').mockReturnValue(true);
        const reportsApi = fakeReportsApi();
        reportsApi.delete.mockImplementation(() =>
          throwError(() => new HttpErrorResponse({ status: 500 }))
        );
        const page = await render(reportsApi);

        page.click('Supprimer', 0);

        expect(page.alerts()).toContain("L'opération a échoué. Veuillez réessayer.");
        expect(reportsApi.list).toHaveBeenCalledTimes(2);
      });
    });

    describe('creation form', () => {
      it('starts at the current month', async () => {
        vi.useFakeTimers({ toFake: ['Date'] });
        vi.setSystemTime(new Date(2026, 9, 11));

        const page = await render();

        expect(page.month().value).toBe('2026-10');
      });

      it('offers the active users', async () => {
        const usersApi = fakeUsersApi();

        const page = await render(fakeReportsApi(), usersApi);

        expect(usersApi.listAllActive).toHaveBeenCalled();
        expect(page.options()).toEqual(['Choisissez un utilisateur', 'Juste Leblanc']);
      });

      it('says so when there is no active user', async () => {
        const page = await render(fakeReportsApi(), fakeUsersApi(() => of([])));

        expect(page.text()).toContain('Aucun utilisateur actif.');
        expect(page.element().querySelector('form')).toBeNull();
      });

      it('says so when the users cannot be loaded', async () => {
        const page = await render(
          fakeReportsApi(),
          fakeUsersApi(() => throwError(() => new Error('Service unavailable')))
        );

        expect(page.alerts()).toContain('Impossible de charger les utilisateurs.');
        expect(page.element().querySelector('form')).toBeNull();
      });

      it('asks for a user instead of creating', async () => {
        const reportsApi = fakeReportsApi();
        const page = await render(reportsApi);

        await page.submit();

        expect(reportsApi.create).not.toHaveBeenCalled();
        expect(page.errors()).toEqual(['Choisissez un utilisateur.']);
      });

      it.each(['', '1999-12', '2101-01'])('refuses the month "%s"', async (month) => {
        const reportsApi = fakeReportsApi();
        const page = await render(reportsApi);
        page.choose('u1');
        page.fillMonth(month);

        await page.submit();

        expect(reportsApi.create).not.toHaveBeenCalled();
        expect(page.errors()).toEqual([monthMessage]);
      });

      it('creates the report for the chosen user and month, then opens it', async () => {
        const reportsApi = fakeReportsApi();
        const page = await render(reportsApi);
        page.choose('u1');
        page.fillMonth('2025-10');

        await page.submit();

        expect(reportsApi.create).toHaveBeenCalledWith({ userId: 'u1', year: 2025, month: 10 });
        expect(TestBed.inject(Router).url).toBe('/expense-reports/9');
      });

      it.each([
        {
          status: 422,
          body: { status: 422, code: 'expense_report.inactive_user' },
          message: 'Cet utilisateur est inactif : il ne peut pas être affecté à une note de frais.',
        },
        {
          status: 422,
          body: { status: 422, code: 'expense_report.already_exists' },
          message: 'Une note de frais existe déjà pour cet utilisateur et ce mois.',
        },
        { status: 404, body: null, message: 'Utilisateur introuvable.' },
        { status: 500, body: null, message: "L'opération a échoué. Veuillez réessayer." },
      ])('says "$message" when the API answers $status', async ({ status, body, message }) => {
        const reportsApi = fakeReportsApi();
        reportsApi.create.mockImplementation(() =>
          throwError(() => new HttpErrorResponse({ status, error: body }))
        );
        const page = await render(reportsApi);
        page.choose('u1');

        await page.submit();

        expect(page.alerts()).toContain(message);
        expect(page.button('Créer').disabled).toBe(false);
        expect(TestBed.inject(Router).url).toBe('/expense-reports');
      });
    });
  });
  ```

- [ ] **Step 2: Update the shell specs**

  In `frontend/src/app/app.spec.ts`, replace the test with:

  ```ts
    it('links to the expense reports, then to the users, from the navigation', () => {
      const fixture = TestBed.createComponent(App);
      fixture.detectChanges();
      const links = Array.from((fixture.nativeElement as HTMLElement).querySelectorAll('nav a'));

      expect(
        links.map((link) => [link.textContent?.trim(), link.getAttribute('href')])
      ).toEqual([
        ['Notes de frais', '/expense-reports'],
        ['Utilisateurs', '/users'],
      ]);
    });
  ```

  In `frontend/src/app/app.routes.spec.ts`, import `ExpenseReportsApi` from
  `./features/expense-reports/expense-reports.api`; in the `UsersApi` fake add
  `listAllActive: () => of([]),`; add a provider:

  ```ts
          {
            provide: ExpenseReportsApi,
            useValue: {
              list: () => of({ items: [], page: 1, pageSize: 20, totalCount: 0 }),
            },
          },
  ```

  and replace the first two tests with:

  ```ts
    it('opens the expense reports page at the root', async () => {
      const harness = await RouterTestingHarness.create();

      await harness.navigateByUrl('/');

      expect(TestBed.inject(Router).url).toBe('/expense-reports');
      expect(harness.routeNativeElement?.querySelector('h1')?.textContent?.trim()).toBe(
        'Notes de frais'
      );
    });

    it('redirects an unknown URL to the expense reports page', async () => {
      const harness = await RouterTestingHarness.create();

      await harness.navigateByUrl('/page-inconnue');

      expect(TestBed.inject(Router).url).toBe('/expense-reports');
    });

    it('opens the users page at /users', async () => {
      const harness = await RouterTestingHarness.create();

      await harness.navigateByUrl('/users');

      expect(harness.routeNativeElement?.querySelector('h1')?.textContent?.trim()).toBe(
        'Utilisateurs'
      );
    });
  ```

- [ ] **Step 3: Run the specs to verify they fail**

  Run: `(cd frontend && npx nx test n2f --skip-nx-cache)`
  Expected: FAIL — `./expense-report-list.page` cannot be resolved; the shell has one link; `/` still redirects to
  `/users`.

- [ ] **Step 4: Write the deletion confirmation**

  `frontend/src/app/features/expense-reports/confirm-deletion.ts`:

  ```ts
  /** Asks before a physical deletion, which also removes the report's expenses. */
  export function confirmDeletion(title: string): boolean {
    return window.confirm(
      `Supprimer la note de frais « ${title} » ? Ses dépenses seront supprimées avec elle.`
    );
  }
  ```

- [ ] **Step 5: Write the page**

  `frontend/src/app/features/expense-reports/expense-report-list.page.ts`:

  ```ts
  import { HttpErrorResponse } from '@angular/common/http';
  import { Component, computed, inject, signal } from '@angular/core';
  import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
  import { NonNullableFormBuilder, ReactiveFormsModule, Validators } from '@angular/forms';
  import { Router, RouterLink } from '@angular/router';
  import { catchError, EMPTY, Subject, switchMap } from 'rxjs';
  import { PagedResult, pageCountOf } from '../../core/paged-result';
  import { businessRuleCode } from '../../core/problem-details';
  import { UserSummary } from '../users/user-summary';
  import { UsersApi } from '../users/users.api';
  import { confirmDeletion } from './confirm-deletion';
  import { ExpenseReportSummary } from './expense-report-summary';
  import { ExpenseReportsApi } from './expense-reports.api';

  const pageSize = 20;

  /**
   * An <input type="month"> value ("2025-10") within the API's bounds (ReportMonth.MinYear and MaxYear). Checked here
   * because some browsers show the field as plain text, where anything can be typed.
   */
  const monthPattern = /^(20[0-9]{2}|2100)-(0[1-9]|1[0-2])$/;

  const ruleMessages = new Map<string, string>([
    [
      'expense_report.inactive_user',
      'Cet utilisateur est inactif : il ne peut pas être affecté à une note de frais.',
    ],
    ['expense_report.already_exists', 'Une note de frais existe déjà pour cet utilisateur et ce mois.'],
  ]);

  /** The current month, in the browser's time zone, as an <input type="month"> value. */
  function currentMonth(): string {
    const today = new Date();
    return `${today.getFullYear()}-${String(today.getMonth() + 1).padStart(2, '0')}`;
  }

  function createErrorMessage(error: unknown): string {
    const ruleMessage = ruleMessages.get(businessRuleCode(error) ?? '');
    if (ruleMessage !== undefined) {
      return ruleMessage;
    }
    if (error instanceof HttpErrorResponse && error.status === 404) {
      // The user was deleted since the active users were loaded.
      return 'Utilisateur introuvable.';
    }
    return "L'opération a échoué. Veuillez réessayer.";
  }

  @Component({
    selector: 'app-expense-report-list-page',
    imports: [ReactiveFormsModule, RouterLink],
    template: `
      <h1 class="mb-4 text-2xl font-semibold">Notes de frais</h1>

      <section class="mb-8">
        <h2 class="mb-2 text-xl font-semibold">Nouvelle note de frais</h2>
        @if (usersFailed()) {
          <p role="alert">Impossible de charger les utilisateurs.</p>
        } @else if (activeUsers(); as users) {
          @if (users.length > 0) {
            <form [formGroup]="form" (ngSubmit)="create()" novalidate class="flex max-w-md flex-col gap-2">
              <label for="userId">Utilisateur</label>
              <select id="userId" formControlName="userId">
                <option value="" disabled>Choisissez un utilisateur</option>
                @for (user of users; track user.id) {
                  <option [value]="user.id">{{ user.name }}</option>
                }
              </select>
              @if (errorFor('userId'); as message) {
                <p class="field-error text-sm text-red-700">{{ message }}</p>
              }

              <label for="month">Mois</label>
              <input id="month" type="month" min="2000-01" max="2100-12" formControlName="month" />
              @if (errorFor('month'); as message) {
                <p class="field-error text-sm text-red-700">{{ message }}</p>
              }

              @if (createError(); as message) {
                <p role="alert">{{ message }}</p>
              }
              <div class="mt-2">
                <button type="submit" [disabled]="creating()">Créer</button>
              </div>
            </form>
          } @else {
            <p>Aucun utilisateur actif.</p>
          }
        } @else {
          <p>Chargement…</p>
        }
      </section>

      @if (failed()) {
        <p role="alert">Impossible de charger les notes de frais.</p>
      }
      @if (actionFailed()) {
        <p role="alert">L'opération a échoué. Veuillez réessayer.</p>
      }
      @if (page(); as page) {
        @if (page.items.length > 0) {
          <table class="w-full text-left">
            <thead>
              <tr>
                <th>Intitulé</th>
                <th>Actions</th>
              </tr>
            </thead>
            <tbody>
              @for (report of page.items; track report.id) {
                <tr>
                  <td>
                    <a [routerLink]="['/expense-reports', report.id]" class="underline">{{ report.title }}</a>
                  </td>
                  <td>
                    <button type="button" [disabled]="busy()" (click)="remove(report)">Supprimer</button>
                  </td>
                </tr>
              }
            </tbody>
          </table>
          <nav aria-label="Pagination" class="mt-4 flex items-center gap-4">
            <button type="button" [disabled]="busy() || page.page <= 1" (click)="load(page.page - 1)">
              Précédent
            </button>
            <span>Page {{ page.page }} sur {{ pageCount() }}</span>
            <button
              type="button"
              [disabled]="busy() || page.page >= pageCount()"
              (click)="load(page.page + 1)"
            >
              Suivant
            </button>
          </nav>
        } @else {
          <p>Aucune note de frais.</p>
        }
      } @else if (!failed()) {
        <p>Chargement…</p>
      }
    `,
  })
  export default class ExpenseReportListPage {
    private readonly reportsApi = inject(ExpenseReportsApi);
    private readonly router = inject(Router);

    protected readonly page = signal<PagedResult<ExpenseReportSummary> | undefined>(undefined);
    protected readonly failed = signal(false);
    protected readonly actionFailed = signal(false);
    /** A deletion runs, until the page reloaded after it shows: a second click would act on a stale row. */
    protected readonly busy = signal(false);
    protected readonly pageCount = computed(() => {
      const page = this.page();
      return page ? pageCountOf(page) : 1;
    });
    private readonly pageRequests = new Subject<number>();

    /** Undefined while they load. */
    protected readonly activeUsers = signal<UserSummary[] | undefined>(undefined);
    protected readonly usersFailed = signal(false);
    protected readonly creating = signal(false);
    protected readonly createError = signal<string | null>(null);
    protected readonly form = inject(NonNullableFormBuilder).group({
      userId: ['', Validators.required],
      month: [currentMonth(), [Validators.required, Validators.pattern(monthPattern)]],
    });

    constructor() {
      inject(UsersApi)
        .listAllActive()
        .subscribe({
          next: (users) => this.activeUsers.set(users),
          error: () => this.usersFailed.set(true),
        });
      this.pageRequests
        .pipe(
          // Only the latest request counts: an answer arriving late must not replace the page asked for since.
          switchMap((pageNumber) =>
            this.reportsApi.list(pageNumber, pageSize).pipe(
              catchError(() => {
                this.failed.set(true);
                this.busy.set(false);
                return EMPTY;
              })
            )
          ),
          takeUntilDestroyed()
        )
        .subscribe((page) => this.show(page));
      this.load(1);
    }

    protected load(pageNumber: number): void {
      this.pageRequests.next(pageNumber);
    }

    protected errorFor(field: 'userId' | 'month'): string | null {
      const control = this.form.controls[field];
      if (control.valid || !control.touched) {
        return null;
      }
      return field === 'userId'
        ? 'Choisissez un utilisateur.'
        : 'Indiquez un mois au format AAAA-MM, entre 2000-01 et 2100-12.';
    }

    protected create(): void {
      if (this.form.invalid) {
        this.form.markAllAsTouched();
        return;
      }
      const { userId, month } = this.form.getRawValue();
      this.creating.set(true);
      this.createError.set(null);
      this.reportsApi
        .create({ userId, year: Number(month.slice(0, 4)), month: Number(month.slice(5, 7)) })
        .subscribe({
          next: (report) => {
            void this.router.navigate(['/expense-reports', report.id]);
          },
          error: (error: unknown) => {
            this.creating.set(false);
            this.createError.set(createErrorMessage(error));
          },
        });
    }

    protected remove(report: ExpenseReportSummary): void {
      if (!confirmDeletion(report.title)) {
        return;
      }
      this.busy.set(true);
      this.actionFailed.set(false);
      this.reportsApi.delete(report.id).subscribe({
        next: () => this.load(this.page()?.page ?? 1),
        error: () => {
          // The report may be gone (deleted in another tab): reload so a stale row does not stay.
          this.actionFailed.set(true);
          this.load(this.page()?.page ?? 1);
        },
      });
    }

    private show(page: PagedResult<ExpenseReportSummary>): void {
      // Reports were deleted past the page shown: show the last page left rather than an empty one. Always an earlier
      // page, so this ends even if more reports go meanwhile.
      const lastPage = pageCountOf(page);
      if (page.items.length === 0 && page.page > lastPage) {
        this.load(lastPage);
        return;
      }
      this.page.set(page);
      this.failed.set(false);
      this.busy.set(false);
    }
  }
  ```

- [ ] **Step 6: Route and link the page**

  In `frontend/src/app/app.routes.ts`, replace the first route and the last one, and add the list route before the
  users routes:

  ```ts
    { path: '', pathMatch: 'full', redirectTo: 'expense-reports' },
    {
      path: 'expense-reports',
      title: 'Notes de frais',
      loadComponent: () => import('./features/expense-reports/expense-report-list.page'),
    },
  ```

  ```ts
    { path: '**', redirectTo: 'expense-reports' },
  ```

  In `frontend/src/app/app.html`, add before the *Utilisateurs* link:

  ```html
      <a routerLink="/expense-reports" routerLinkActive="underline">Notes de frais</a>
  ```

- [ ] **Step 7: Run the frontend checks**

  Run: `(cd frontend && npx nx run-many -t lint test build --skip-nx-cache)`
  Expected: PASS.

- [ ] **Step 8: Commit**

  ```bash
  (cd frontend && npx prettier --write src/app)
  git add -A frontend/src
  git commit -m "feat(expense-reports): add the expense reports page with its creation form"
  ```

---

### Task 8: Expense report page

**Files:**

- Create: `frontend/src/app/features/expense-reports/expense-report.page.ts`
- Modify: `frontend/src/app/app.routes.ts`
- Test: `frontend/src/app/features/expense-reports/expense-report.page.spec.ts`,
  `frontend/src/app/app.routes.spec.ts`

**Interfaces:**

- Consumes: Task 6's `ExpenseReportsApi.get` / `delete`, `ExpenseReportDetails`; Task 7's `confirmDeletion`.
- Produces: the default-exported standalone `ExpenseReportPage` at `/expense-reports/:id`. Slice 4 adds the
  expenses section under the header.

- [ ] **Step 1: Write the failing page spec**

  `frontend/src/app/features/expense-reports/expense-report.page.spec.ts`:

  ```ts
  import { registerLocaleData } from '@angular/common';
  import { HttpErrorResponse } from '@angular/common/http';
  import localeFr from '@angular/common/locales/fr';
  import { LOCALE_ID } from '@angular/core';
  import { TestBed } from '@angular/core/testing';
  import { provideRouter, Router } from '@angular/router';
  import { RouterTestingHarness } from '@angular/router/testing';
  import { Observable, of, throwError } from 'rxjs';
  import { ExpenseReportDetails } from './expense-report-details';
  import ExpenseReportPage from './expense-report.page';
  import { ExpenseReportsApi } from './expense-reports.api';

  registerLocaleData(localeFr);

  const october: ExpenseReportDetails = {
    id: '1',
    title: 'Juste Leblanc - Octobre 2025',
    userId: 'u1',
    year: 2025,
    month: 10,
  };

  function httpError(status: number): Observable<never> {
    return throwError(() => new HttpErrorResponse({ status }));
  }

  function fakeApi() {
    return {
      get: vi.fn((): Observable<ExpenseReportDetails> => of(october)),
      delete: vi.fn((): Observable<void> => of(undefined)),
    };
  }

  async function render(api = fakeApi()) {
    TestBed.configureTestingModule({
      providers: [
        provideRouter([
          { path: 'expense-reports', children: [] },
          { path: 'expense-reports/:id', component: ExpenseReportPage },
        ]),
        { provide: ExpenseReportsApi, useValue: api },
        { provide: LOCALE_ID, useValue: 'fr-FR' },
      ],
    });
    const harness = await RouterTestingHarness.create();
    await harness.navigateByUrl('/expense-reports/1', ExpenseReportPage);
    harness.detectChanges();
    const element = () => harness.routeNativeElement as HTMLElement;
    const deleteButton = () =>
      Array.from(element().querySelectorAll('button')).find(
        (button) => button.textContent?.trim() === 'Supprimer'
      ) as HTMLButtonElement;

    return {
      element,
      deleteButton,
      heading: () => element().querySelector('h1')?.textContent?.trim(),
      alert: () => element().querySelector('[role="alert"]')?.textContent?.trim(),
      remove: async () => {
        deleteButton().click();
        harness.detectChanges();
        await harness.fixture.whenStable();
      },
    };
  }

  describe('ExpenseReportPage', () => {
    afterEach(() => vi.restoreAllMocks());

    it('shows the title and the month of the report', async () => {
      const api = fakeApi();

      const page = await render(api);

      expect(api.get).toHaveBeenCalledWith('1');
      expect(page.heading()).toBe('Juste Leblanc - Octobre 2025');
      expect(page.element().textContent).toContain('Mois : octobre 2025');
    });

    it('links back to the list', async () => {
      const page = await render();

      const back = page.element().querySelector('a');
      expect(back?.textContent?.trim()).toBe('← Notes de frais');
      expect(back?.getAttribute('href')).toBe('/expense-reports');
    });

    it('says so when the report does not exist', async () => {
      const api = fakeApi();
      api.get.mockImplementation(() => httpError(404));

      const page = await render(api);

      expect(page.alert()).toBe('Note de frais introuvable.');
      expect(page.heading()).toBeUndefined();
    });

    it('says so when the report cannot be loaded', async () => {
      const api = fakeApi();
      api.get.mockImplementation(() => httpError(500));

      const page = await render(api);

      expect(page.alert()).toBe('Impossible de charger la note de frais.');
    });

    it('deletes the report once confirmed, then goes back to the list', async () => {
      const confirm = vi.spyOn(window, 'confirm').mockReturnValue(true);
      const api = fakeApi();
      const page = await render(api);

      await page.remove();

      expect(confirm).toHaveBeenCalledWith(
        'Supprimer la note de frais « Juste Leblanc - Octobre 2025 » ? Ses dépenses seront supprimées avec elle.'
      );
      expect(api.delete).toHaveBeenCalledWith('1');
      expect(TestBed.inject(Router).url).toBe('/expense-reports');
    });

    it('keeps the report when the deletion is cancelled', async () => {
      vi.spyOn(window, 'confirm').mockReturnValue(false);
      const api = fakeApi();
      const page = await render(api);

      await page.remove();

      expect(api.delete).not.toHaveBeenCalled();
      expect(TestBed.inject(Router).url).toBe('/expense-reports/1');
    });

    it('goes back to the list when the report was already deleted', async () => {
      vi.spyOn(window, 'confirm').mockReturnValue(true);
      const api = fakeApi();
      api.delete.mockImplementation(() => httpError(404));
      const page = await render(api);

      await page.remove();

      expect(TestBed.inject(Router).url).toBe('/expense-reports');
    });

    it('says so when the deletion fails', async () => {
      vi.spyOn(window, 'confirm').mockReturnValue(true);
      const api = fakeApi();
      api.delete.mockImplementation(() => httpError(500));
      const page = await render(api);

      await page.remove();

      expect(page.alert()).toBe('La suppression a échoué. Veuillez réessayer.');
      expect(page.deleteButton().disabled).toBe(false);
      expect(TestBed.inject(Router).url).toBe('/expense-reports/1');
    });
  });
  ```

  In `frontend/src/app/app.routes.spec.ts`, add `get` to the `ExpenseReportsApi` fake:

  ```ts
              get: () =>
                of({ id: '1', title: 'Juste Leblanc - Octobre 2025', userId: 'u1', year: 2025, month: 10 }),
  ```

  and a test:

  ```ts
    it('opens a report at /expense-reports/:id', async () => {
      const harness = await RouterTestingHarness.create();

      await harness.navigateByUrl('/expense-reports/1');

      expect(harness.routeNativeElement?.querySelector('h1')?.textContent?.trim()).toBe(
        'Juste Leblanc - Octobre 2025'
      );
    });
  ```

- [ ] **Step 2: Run the specs to verify they fail**

  Run: `(cd frontend && npx nx test n2f --skip-nx-cache)`
  Expected: FAIL — `./expense-report.page` cannot be resolved; `/expense-reports/1` redirects to the list.

- [ ] **Step 3: Write the page**

  `frontend/src/app/features/expense-reports/expense-report.page.ts`:

  ```ts
  import { DatePipe } from '@angular/common';
  import { HttpErrorResponse } from '@angular/common/http';
  import { Component, computed, inject, signal } from '@angular/core';
  import { ActivatedRoute, Router, RouterLink } from '@angular/router';
  import { confirmDeletion } from './confirm-deletion';
  import { ExpenseReportDetails } from './expense-report-details';
  import { ExpenseReportsApi } from './expense-reports.api';

  function isNotFound(error: unknown): boolean {
    return error instanceof HttpErrorResponse && error.status === 404;
  }

  @Component({
    selector: 'app-expense-report-page',
    imports: [DatePipe, RouterLink],
    template: `
      <a routerLink="/expense-reports" class="underline">← Notes de frais</a>
      @if (error(); as message) {
        <p role="alert">{{ message }}</p>
      }
      @if (report(); as report) {
        <h1 class="my-4 text-2xl font-semibold">{{ report.title }}</h1>
        <p>Mois : {{ firstDay() | date: 'MMMM y' }}</p>
        <button type="button" class="mt-4" [disabled]="deleting()" (click)="remove(report)">Supprimer</button>
      } @else if (!error()) {
        <p>Chargement…</p>
      }
    `,
  })
  export default class ExpenseReportPage {
    private readonly api = inject(ExpenseReportsApi);
    private readonly router = inject(Router);
    private readonly id = inject(ActivatedRoute).snapshot.paramMap.get('id') ?? '';
    protected readonly report = signal<ExpenseReportDetails | undefined>(undefined);
    protected readonly error = signal<string | null>(null);
    protected readonly deleting = signal(false);
    /** The DatePipe formats a date: the first day of the report's month, in local time. */
    protected readonly firstDay = computed(() => {
      const report = this.report();
      return report ? new Date(report.year, report.month - 1, 1) : null;
    });

    constructor() {
      this.api.get(this.id).subscribe({
        next: (report) => this.report.set(report),
        error: (error: unknown) =>
          this.error.set(
            isNotFound(error) ? 'Note de frais introuvable.' : 'Impossible de charger la note de frais.'
          ),
      });
    }

    protected remove(report: ExpenseReportDetails): void {
      if (!confirmDeletion(report.title)) {
        return;
      }
      this.deleting.set(true);
      this.error.set(null);
      this.api.delete(report.id).subscribe({
        next: () => {
          void this.router.navigateByUrl('/expense-reports');
        },
        error: (error: unknown) => {
          // Deleted meanwhile (another tab): it is gone, as asked.
          if (isNotFound(error)) {
            void this.router.navigateByUrl('/expense-reports');
            return;
          }
          this.deleting.set(false);
          this.error.set('La suppression a échoué. Veuillez réessayer.');
        },
      });
    }
  }
  ```

- [ ] **Step 4: Route the page**

  In `frontend/src/app/app.routes.ts`, after the `expense-reports` route:

  ```ts
    {
      path: 'expense-reports/:id',
      title: 'Note de frais',
      loadComponent: () => import('./features/expense-reports/expense-report.page'),
    },
  ```

- [ ] **Step 5: Run the frontend checks**

  Run: `(cd frontend && npx nx run-many -t lint test build --skip-nx-cache)`
  Expected: PASS.

- [ ] **Step 6: Commit**

  ```bash
  (cd frontend && npx prettier --write src/app)
  git add -A frontend/src
  git commit -m "feat(expense-reports): add the expense report page"
  ```

---

### Task 9: End-to-end expense report journeys

**Files:**

- Create: `backend/tests/EndToEndTests/ExpenseReportManagementTests.cs`
- Modify: `backend/tests/EndToEndTests/UsersPageTests.cs`

**Interfaces:**

- Consumes: Tasks 2–8 running together under the AppHost; `PageTest` (`Page`, `Fixture.AngularUrl`), the Angular
  dev proxy `/api/v1/...`, the labels *Utilisateur*, the button *Créer*, the heading *Notes de frais*, the link
  *← Notes de frais*, the row button *Supprimer*, the duplicate message.
- Produces: nothing for later tasks.

- [ ] **Step 1: Point the seeded-users test at `/users`**

  The root now opens the expense reports (the next file checks it). Replace
  `backend/tests/EndToEndTests/UsersPageTests.cs` with:

  ```csharp
  using EndToEndTests.Support;
  using Microsoft.Playwright;

  namespace EndToEndTests;

  public sealed class UsersPageTests(AppFixture fixture) : PageTest(fixture)
  {
      [Fact]
      public async Task Opening_the_users_page_shows_the_seeded_users()
      {
          await Page.GotoAsync("/users");

          await Assertions.Expect(Page.GetByRole(AriaRole.Heading, new()
          {
              Name = "Utilisateurs"
          })).ToBeVisibleAsync();
          var users = Page.GetByRole(AriaRole.Row);
          await Assertions.Expect(users.Filter(new()
          {
              HasText = "Juste Leblanc"
          })).ToBeVisibleAsync();
          await Assertions.Expect(users.Filter(new()
          {
              HasText = "Marc Assin"
          })).ToBeVisibleAsync();
      }
  }
  ```

- [ ] **Step 2: Write the journeys**

  `backend/tests/EndToEndTests/ExpenseReportManagementTests.cs`:

  ```csharp
  using System.Text.RegularExpressions;
  using EndToEndTests.Support;
  using Microsoft.Playwright;

  namespace EndToEndTests;

  public sealed partial class ExpenseReportManagementTests(AppFixture fixture) : PageTest(fixture)
  {
      [Fact]
      public async Task Opening_the_app_shows_the_expense_reports()
      {
          await Page.GotoAsync("/");

          await Assertions.Expect(Page).ToHaveURLAsync(new Uri(Fixture.AngularUrl, "expense-reports").ToString());
          await Assertions.Expect(Page.GetByRole(AriaRole.Heading, new()
          {
              Name = "Notes de frais", Exact = true
          })).ToBeVisibleAsync();
      }

      [Fact]
      public async Task An_administrator_creates_a_report_for_the_current_month_and_opens_it()
      {
          var name = await CreateUserAsync("Jeanne");

          await CreateReportAndOpenItAsync(name);

          await Assertions.Expect(Page.GetByRole(AriaRole.Heading, new()
          {
              Level = 1
          })).ToContainTextAsync($"{name} - ");
          await Assertions.Expect(Page.GetByText("Mois :")).ToBeVisibleAsync();
      }

      [Fact]
      public async Task An_administrator_is_told_when_the_report_already_exists()
      {
          var name = await CreateUserAsync("Paul");
          await CreateReportAndOpenItAsync(name);

          await CreateReportAsync(name);

          await Assertions.Expect(Page.GetByRole(AriaRole.Alert))
              .ToHaveTextAsync("Une note de frais existe déjà pour cet utilisateur et ce mois.");
      }

      [Fact]
      public async Task An_administrator_deletes_a_report_from_the_list()
      {
          var name = await CreateUserAsync("Lucie");
          await CreateReportAndOpenItAsync(name);
          await Page.GetByRole(AriaRole.Link, new()
          {
              Name = "← Notes de frais"
          }).ClickAsync();
          var row = Page.GetByRole(AriaRole.Row).Filter(new()
          {
              HasText = name
          });
          await Assertions.Expect(row).ToHaveCountAsync(1);

          Page.Dialog += AcceptConfirmation;
          await row.GetByRole(AriaRole.Button, new()
          {
              Name = "Supprimer"
          }).ClickAsync();

          await Assertions.Expect(row).ToHaveCountAsync(0);
      }

      [GeneratedRegex("/expense-reports/[0-9a-f-]{36}$")]
      private static partial Regex ReportPageUrl();

      /// <summary>Playwright dismisses the dialogs nobody handles: accept the deletion confirmation.</summary>
      private static void AcceptConfirmation(object? sender, IDialog dialog)
      {
          _ = dialog.AcceptAsync();
      }

      /// <summary>
      /// Creates an active user with a unique last name through the API (the users journeys already cover the form)
      /// and returns their full name.
      /// </summary>
      private async Task<string> CreateUserAsync(string firstName)
      {
          var lastName = $"E2e{Guid.NewGuid().ToString("N")[..8]}";
          var response = await Page.APIRequest.PostAsync("/api/v1/users", new()
          {
              DataObject = new
              {
                  firstName,
                  lastName,
                  address = new
                  {
                      street = "5 rue Victor Hugo", postalCode = "33000", city = "Bordeaux"
                  },
                  monthlyExpenseQuota = 8,
              },
          });
          response.Ok.ShouldBeTrue($"creating the user answered {response.Status}");
          return $"{firstName} {lastName}";
      }

      /// <summary>Creates a report for the current month (the form's default) from the reports page.</summary>
      private async Task CreateReportAsync(string name)
      {
          await Page.GotoAsync("/expense-reports");
          await Page.GetByLabel("Utilisateur", new()
          {
              Exact = true
          }).SelectOptionAsync(new SelectOptionValue
          {
              Label = name
          });
          await Page.GetByRole(AriaRole.Button, new()
          {
              Name = "Créer"
          }).ClickAsync();
      }

      private async Task CreateReportAndOpenItAsync(string name)
      {
          await CreateReportAsync(name);
          await Assertions.Expect(Page).ToHaveURLAsync(ReportPageUrl());
      }
  }
  ```

  The journeys create their own users and leave the seeded ones untouched; the reports of the first two journeys
  stay in the run's database, under unique names.

- [ ] **Step 3: Run the end-to-end tests**

  Run: `dtk dotnet test --project backend/tests/EndToEndTests` (Docker and Node.js required; the first run installs
  npm packages and Chromium)
  Expected: PASS — the four new journeys, `Opening_the_users_page_shows_the_seeded_users`, the users journeys and the
  dashboard test. On failure, read the trace and screenshot in
  `backend/tests/EndToEndTests/bin/Debug/net10.0/playwright-artifacts/`.

- [ ] **Step 4: Format and commit**

  ```bash
  dotnet jb cleanupcode N2JSoft.slnx --profile="ReformatAndReorder" --verbosity=ERROR
  git add -A backend/tests/EndToEndTests
  git commit -m "test(expense-reports): cover the expense report journeys end to end"
  ```

---

### Task 10: Documentation and final verification

**Files:**

- Modify: `README.md` ("Mes Notes"), `docs/superpowers/roadmap.md`

**Interfaces:**

- Consumes: everything above.
- Produces: the hand-off for slice 4 (roadmap conventions).

- [ ] **Step 1: Update "Mes Notes"**

  In `README.md`, after the "Tranche 2 — Utilisateurs" section (before "Lancer les tests"), add:

  ```markdown
  ### Tranche 3 — Notes de frais

  Réalisé :

  - **Modèle** : une note de frais (`ExpenseReport`) appartient à un utilisateur et couvre un mois (`ReportMonth`, de
    janvier 2000 à décembre 2100). Son intitulé, « Prénom Nom - Mois Année » (par ex. « Juste Leblanc - Octobre
    2025 »), est composé et enregistré à la création, puis ne change plus. Les noms de mois sont écrits en dur, avec
    leur majuscule, pour ne dépendre ni de la culture `fr-FR` ni de la présence d'ICU sur le serveur.
  - **Règles métier**, premières réponses `422` (avec un `code` que le front traduit) : seul un utilisateur actif peut
    être affecté à une note de frais (`expense_report.inactive_user`) ; un utilisateur a au plus une note de frais par
    mois (`expense_report.already_exists`). Cette seconde règle est vérifiée avant l'enregistrement et garantie par un
    index unique : deux créations simultanées donnent une note et un `422`, jamais une erreur `500`.
  - **API** sous `/v1/expense-reports` : liste paginée (le mois le plus récent d'abord), détail, création (`201` +
    `Location`) et suppression physique (`DELETE` ; `404` si la note a déjà été supprimée, y compris par une requête
    simultanée). `GET /v1/users` accepte un filtre `isActive`.
  - **Front** : la page des notes de frais, devenue la page d'accueil, permet d'en créer une en choisissant un
    utilisateur actif et un mois (le mois courant par défaut), puis ouvre la note créée. Elle liste les notes par pages
    de 20, avec suppression après confirmation. La page d'une note affiche son intitulé et son mois et permet de la
    supprimer ; ses dépenses arrivent avec la tranche 4.

  Hypothèses (le sujet ne précise pas) :

  - Un utilisateur a au plus une note de frais par mois.
  - L'intitulé est figé à la création : renommer l'utilisateur ne le change pas.
  - Les mois acceptés vont de janvier 2000 à décembre 2100, passés comme à venir.
  - Supprimer une note de frais supprimera aussi ses dépenses (tranche 4).
  - Les notes de frais d'un utilisateur supprimé restent visibles, avec leur intitulé d'origine.
  ```

  In "Pistes d'amélioration", add:

  ```markdown
  - Mutualiser la pagination des listes (utilisateurs, notes de frais) dans un composant partagé : la logique est
    aujourd'hui dupliquée dans les deux pages.
  ```

- [ ] **Step 2: Update the roadmap**

  In `docs/superpowers/roadmap.md`:

  - Set the Expense Reports row status to `Done` and its spec cell to
    `[expense reports](specs/2026-10-11-expense-reports-design.md), [plan](plans/2026-10-11-expense-reports.md)`.
  - Replace the "### Expense Reports" open questions with:

    ```markdown
    ### Expense Reports

    Settled in the [Expense Reports spec](specs/2026-10-11-expense-reports-design.md#decisions).
    ```

  - Under "Conventions every slice inherits", after the **Errors** item, add:

    ```markdown
    - **Business rules:** codes are `<feature>.<rule>` (`expense_report.already_exists`). A rule that a concurrent
      request can break between the check and the save (uniqueness) is also enforced by a database constraint, which
      the store maps to the same exception; its race is tested at the store level (two DI scopes, both read, then both
      save). The UI reads the code with `businessRuleCode` (`core/problem-details.ts`) and maps it to French.
    ```

- [ ] **Step 3: Run every suite**

  ```bash
  dtk dotnet build N2JSoft.slnx
  dtk dotnet test --project backend/tests/UnitTests
  dtk dotnet test --project backend/tests/AcceptanceTests
  dtk dotnet test --project backend/tests/EndToEndTests
  (cd frontend && npx nx run-many -t lint test build --skip-nx-cache)
  dotnet jb cleanupcode N2JSoft.slnx --profile="ReformatAndReorder" --verbosity=ERROR && git diff --exit-code
  npx --yes markdownlint-cli2 README.md docs/superpowers/roadmap.md docs/superpowers/plans/2026-10-11-expense-reports.md
  ```

  Expected: everything PASSES, the cleanup leaves no diff, markdownlint reports 0 issues.

- [ ] **Step 4: Check the running app**

  Run `dotnet run --project tools/N2JSoft.Aspire`, open the Angular resource from the dashboard and check by hand:
  the home page is *Notes de frais*; create a report for Juste Leblanc for the current month and land on its page;
  create it again and read the French refusal; deactivate Marc Assin on *Utilisateurs* and check he is no longer
  offered; delete the report from its page and from the list. Open Scalar from the `webapi` resource and check it
  lists the four `/v1/expense-reports` operations and the `isActive` parameter of `GET /v1/users`.

- [ ] **Step 5: Commit**

  ```bash
  git add README.md docs/superpowers/roadmap.md
  git commit -m "docs: expense reports slice notes and roadmap status"
  ```

  Then hand over to `superpowers:finishing-a-development-branch` (PR `feat/expense-reports` → `develop`; no
  "Generated with Claude Code" line in the PR description).
