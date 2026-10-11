# Expense Reports — design

Slice 3 of the [roadmap](../roadmap.md). Status: approved design; see the [implementation plan](../plans/2026-10-11-expense-reports.md).
Builds on the [Foundation](2026-10-10-foundation-design.md) and [Users](2026-10-11-users-design.md) designs, whose
conventions apply unless stated otherwise here.

## Context

After slice 2, users are complete: address, monthly quota, active flag, soft delete, and a management UI. The write
side follows one template: a store per feature (`IUserStore`), one handler per write use case, domain entities that
guard their own rules. No business rule has been violated through the API yet, so no `422` exists.

The README requires that an expense report:

- has exactly one assigned user, who must be active; a user can be assigned to several reports;
- holds a user's expenses for one month;
- is titled "Utilisateur - Mois Année" (e.g. "Juste Leblanc - Octobre 2025"), and the title is not editable;
- can be deleted physically;
- is created from the reports page by selecting the month and the user.

## Goals

- Add the `ExpenseReport` entity, created for an active user and a month, with a stored title.
- Expose list, read, create and delete under `/v1/expense-reports`, with the project's first two `422` rules.
- Give the administrator a French reports page (create form and paged list) and a report page (header and
  delete), which slice 4 fills with expenses.

## Non-goals

- Expenses, the quota rule, and the expenses section of the report page (slice 4).
- Editing a report: every field is fixed at creation.
- Filtering or searching the reports list.
- Seeding demo reports.

## Decisions

Open questions from the roadmap, and the others raised during the brainstorm.

| Question                                          | Decision                                                                                                                  | Why                                                                                                           |
|---------------------------------------------------|---------------------------------------------------------------------------------------------------------------------------|---------------------------------------------------------------------------------------------------------------|
| Several reports for one user and month?           | No: one report per user per month. A duplicate answers `422 expense_report.already_exists`                                | "A report holds a user's expenses for the month"; two identical titles would be ambiguous for slice 4        |
| Stored or computed title?                         | Stored at creation, never changed                                                                                         | "Not editable" holds even when the user is renamed or deleted                                                 |
| Month capitalisation                              | French month name with a capital first letter: "Octobre 2025"                                                             | The README's example                                                                                          |
| Deleting a report that holds expenses             | Cascade: the report's expenses are deleted physically with it (FK `ON DELETE CASCADE`, added in slice 4)                  | A report is the only way to reach its expenses; the confirmation warns the administrator                     |
| Reports of a soft-deleted user                    | Kept: still listed and readable with their stored title                                                                   | The accounting record outlives the user; reads never join the user, so the user's query filter cannot hide them |
| Which months?                                     | Any month from January 2000 to December 2100; the form pre-fills the current month                                        | Catching up on past months and preparing the next one are both legitimate; the README sets no limit          |
| Where does the user picker get active users?      | An `isActive` filter on the existing paged `GET /v1/users`; the picker reads every page                                   | No new endpoint; one request until there are more than 100 active users                                       |
| Detail page in this slice?                        | Yes, header only (title, month, delete); slice 4 adds the expenses                                                        | List rows link somewhere, and slice 4 only adds a section                                                     |
| Where are the two rules enforced?                 | Active user: `ExpenseReport.Create`. Uniqueness: the handler asks the store first; a unique index backs it up and its violation maps to the same `422` | Rules stay in Domain and Application, unit-tested; a racing duplicate still gets the right answer           |
| Unknown or deleted user in the create request     | `404` through the existing `NotFoundException`                                                                            | Reuses the mapping; the user, not the report, is missing                                                      |
| Concurrency                                       | No version or `If-Match`                                                                                                  | A report is immutable; a second `DELETE` answers `404`                                                        |

## Domain

`Domain/ValueObjects/ReportMonth.cs`: `sealed record ReportMonth(int Year, int Month)`.

- The constructor throws `InvalidValueException` (→ `400`) when `Year` is outside 2000–2100 or `Month` outside
  1–12. A private parameterless constructor serves EF Core, like `Address`.
- `ToString()` returns the French label, "Octobre 2025", from a hard-coded array of the twelve month names
  ("Janvier", "Février", … "Août", … "Décembre"). It does not use `CultureInfo("fr-FR")`, so the title neither
  depends on ICU being installed nor on a culture's capitalisation.

`Domain/Entities/ExpenseReport.cs`:

```csharp
public sealed class ExpenseReport
{
    public const int TitleMaxLength = 250;

    public Guid Id { get; } = Guid.CreateVersion7();
    public Guid UserId { get; private set; }
    public int Year { get; private set; }
    public int Month { get; private set; }
    public ReportMonth Period => new(Year, Month);   // not mapped

    /// <summary>"First Last - Month Year", fixed at creation.</summary>
    public string Title { get; private set; }

    public static ExpenseReport Create(User user, ReportMonth period);
}
```

- `Create` throws `InactiveUserException` when the user is inactive, and otherwise sets
  `Title = $"{user.FirstName} {user.LastName} - {period}"` (names are already trimmed by `User`). The longest
  title is 100 + 1 + 100 + 3 + 16 characters, under `TitleMaxLength`.
- There are no other methods: nothing is editable, and deletion is physical.

`Domain/Exceptions/`, both deriving from `BusinessRuleViolationException`:

| Exception                         | Code                            | Thrown by                                   |
|-----------------------------------|---------------------------------|---------------------------------------------|
| `InactiveUserException`           | `expense_report.inactive_user`  | `ExpenseReport.Create`                      |
| `DuplicateExpenseReportException` | `expense_report.already_exists` | `CreateExpenseReportHandler`, the store     |

## Persistence

- `ExpenseReportEntityTypeConfiguration`: table `expense_reports` (the `users` naming); columns `Year` and `Month`
  as plain integers, because EF Core 10 cannot index a complex property's columns (verified while planning);
  `Title` required, at most `TitleMaxLength`.
- Foreign key `UserId` → `users.Id`, `OnDelete(Restrict)` (users are only ever soft-deleted). No navigation
  property: report reads never join `users`, so the users' `DeletedAt` query filter cannot hide a report.
- Unique index `IX_expense_reports_UserId_Month` on `(UserId, Year, Month)`.
- Migration `ExpenseReports` adds the table, as generated by EF.
- `AppDbContext` applies the new configuration. The seed is unchanged.

## Application

```text
Application/
  ExpenseReports/
    IExpenseReportStore.cs
    ExpenseReportSummary.cs   (Id, Title, Year, Month)        slice 4 adds the expense count
    ExpenseReportDetails.cs   (Id, Title, UserId, Year, Month)
    Commands/  CreateExpenseReport.cs   CreateExpenseReportCommand(UserId, ReportMonth) + handler (returns the new id)
               DeleteExpenseReport.cs   DeleteExpenseReportCommand(Id) + handler
  Users/
    IUserStore.cs             ListAsync gains a bool? isActive filter
Infrastructure/
  ExpenseReports/ExpenseReportStore.cs
```

```csharp
public interface IExpenseReportStore
{
    // Reads: AsNoTracking projections, no logic.
    Task<PagedResult<ExpenseReportSummary>> ListAsync(PageRequest request, CancellationToken cancellationToken);
    Task<ExpenseReportDetails?> GetDetailsAsync(Guid id, CancellationToken cancellationToken);
    Task<bool> ExistsAsync(Guid userId, ReportMonth month, CancellationToken cancellationToken);

    // Writes.
    Task<ExpenseReport?> FindAsync(Guid id, CancellationToken cancellationToken);
    void Add(ExpenseReport report);
    void Remove(ExpenseReport report);

    /// Throws DuplicateExpenseReportException when IX_expense_reports_UserId_Month is violated,
    /// NotFoundException when a removed report was already deleted by another request.
    Task SaveChangesAsync(CancellationToken cancellationToken);
}
```

- **`CreateExpenseReportHandler(IUserStore, IExpenseReportStore)`**, in this order:
  1. `userStore.GetAsync(userId)`: `NotFoundException` for an unknown or deleted user;
  2. `ExistsAsync(userId, month)`: `DuplicateExpenseReportException` when true;
  3. `ExpenseReport.Create(user, month)`: `InactiveUserException` for an inactive user;
  4. `Add`, then `SaveChangesAsync`.
- **`DeleteExpenseReportHandler`**: `FindAsync`, `NotFoundException` when null, else `Remove` and save.
- **Store**:
  - `SaveChangesAsync` catches a `DbUpdateException` whose inner `PostgresException` has `SqlState` `23505` and
    `ConstraintName` `IX_expense_reports_UserId_Month`, and throws `DuplicateExpenseReportException`. It maps a
    `DbUpdateConcurrencyException` (the report was deleted by another request since it was read) to
    `NotFoundException`. Any other failure propagates.
  - The list sorts by `Year` descending, `Month` descending, then `Title`, then `Id`.
- **Users**: `IUserStore.ListAsync(PageRequest request, bool? isActive, CancellationToken)`; `null` lists every
  user, as today. `UserStore` adds the `Where` before counting, so `totalCount` matches the filter.
- `AddApplication()` registers both handlers; `AddInfrastructure()` registers the store, scoped.

## API

All routes under `/v1/expense-reports`.

| Method   | Route    | Body                         | Success                                                         | Errors              |
|----------|----------|------------------------------|-----------------------------------------------------------------|---------------------|
| `GET`    | `/`      | —                            | `200 PagedResult<ExpenseReportSummary>`                         | `400`               |
| `GET`    | `/{id}`  | —                            | `200 ExpenseReportDetails`                                      | `404`               |
| `POST`   | `/`      | `CreateExpenseReportRequest` | `201`, `Location: /v1/expense-reports/{id}`, `ExpenseReportDetails` | `400`, `404`, `422` |
| `DELETE` | `/{id}`  | —                            | `204`                                                           | `404`               |

- `CreateExpenseReportRequest { userId, year, month }` is a public record: `[Required] Guid? UserId`,
  `[Required, Range(2000, 2100)] int? Year`, `[Required, Range(1, 12)] int? Month`. Nullable types make a
  missing field a `400` instead of a default value.
- `POST` answers `404` for an unknown or deleted user, and `422` with `code` `expense_report.inactive_user` or
  `expense_report.already_exists`.
- `GET /v1/users` gains an optional `isActive` query parameter.

## Frontend

Feature folder `src/app/features/expense-reports/`, standalone components with signals, French text.

- **Shell**: the nav gets *Notes de frais*, before *Utilisateurs*. Routes `/expense-reports` (list) and
  `/expense-reports/:id` (report). `''` and `**` now redirect to `/expense-reports`, the main use case.
- **List page** (`expense-report-list.page.ts`):
  - A create form at the top: *Utilisateur*, a `<select>` of the active users, sorted by name and loaded with
    `UsersApi.listAllActive()`, which reads `GET /users?isActive=true` page by page (100 per page) until
    `totalCount`; *Mois*, an `<input type="month">` pre-filled with the current month; *Créer*. With no active
    user, "Aucun utilisateur actif." replaces the select and *Créer* is disabled.
  - Creating navigates to the new report's page. Errors show in a `role="alert"` message:

    | Response                             | Message                                                                          |
    |--------------------------------------|----------------------------------------------------------------------------------|
    | `422 expense_report.inactive_user`   | Cet utilisateur est inactif : il ne peut pas être affecté à une note de frais.   |
    | `422 expense_report.already_exists`  | Une note de frais existe déjà pour cet utilisateur et ce mois.                   |
    | `404`                                | Utilisateur introuvable.                                                         |
    | anything else                        | L'opération a échoué. Veuillez réessayer.                                        |

  - A table with *Intitulé* (a link to the report) and *Actions* (*Supprimer*: native `confirm()` naming the
    report and saying its expenses are deleted with it, then delete and reload the current page). *Précédent* /
    *Suivant* and "Page X sur Y" page through 20 reports at a time. A failed load shows
    "Impossible de charger les notes de frais."
- **Report page** (`expense-report.page.ts`): the title as the heading, "Mois : octobre 2025" (Angular
  `DatePipe`, format `MMMM y`, `fr-FR` locale), a "← Notes de frais" link, and *Supprimer* (confirmation, then
  back to the list). A `404` shows "Note de frais introuvable." The expenses section is left to slice 4.
- **`core/problem-details.ts`**: `businessRuleCode(error: unknown): string | undefined` reads the `code` of a `422`
  `HttpErrorResponse`. Slice 4 reuses it for the quota.
- **`ExpenseReportsApi`**: `list(page, pageSize)`, `get(id)`, `create({ userId, year, month })`, `delete(id)`;
  `ExpenseReportSummary` and `ExpenseReportDetails` interfaces mirror the API. **`UsersApi`**: `list` takes an
  optional `isActive`, and `listAllActive()` is added.

## Test tiers

Written test-first.

- **Unit**:
  - `ReportMonth`: bounds, and the label of all twelve months ("Février", "Août", "Décembre" included).
  - `ExpenseReport.Create`: title format, and `InactiveUserException` with its code for an inactive user.
  - `CreateExpenseReportHandler`: saves on success; throws `NotFoundException`, `DuplicateExpenseReportException`
    or `InactiveUserException` without saving. NSubstitute stores.
  - `DeleteExpenseReportHandler`: removes and saves; `NotFoundException` without saving.
- **Acceptance**: features `ListExpenseReports`, `CreateExpenseReport`, `GetExpenseReport` and
  `DeleteExpenseReport`, reusing `ApiDriver` and `HttpSteps`, plus an `isActive` scenario in `ListUsers`. They
  cover `201` with `Location`; a `400` outline (missing user id, year or month; year 1999 and 2101; month 0 and
  13); `404` for an unknown and a deleted user; `422` with its `code` for an inactive user and a duplicate; list
  order and paging; a deleted user's report still listed and readable with its original title; `404` on `GET`
  and on a second `DELETE` of a deleted report.
- **End-to-end**: each journey creates its own uniquely named user through the API.
  1. Create a report for that user and the current month; the report page shows its title.
  2. Create the same report again; the "already exists" message shows.
  3. Delete the report from the list after confirming; it is gone.
- **Frontend (Vitest)**: `expense-reports.api.spec`; `users.api.spec` (`isActive`, `listAllActive` over several
  pages); `problem-details.spec`; `expense-report-list.page.spec` (current month pre-filled, active users only,
  no-active-user state, each error message, navigation after create, rows and links, delete confirmed and
  cancelled, pagination, load error); `expense-report.page.spec` (header, not-found state, delete).

## Risks to verify first

Both were spiked while planning, against Postgres 18.3 and the repository's analyzers:

1. EF Core 10 does **not** accept an index over a complex property's columns (neither by lambda nor by column
   name), so the fallback applies: plain `Year` and `Month` columns, with an unmapped `Period` getter. `AnyAsync`
   and the sort on them translate.
2. A duplicate surfaces as a `DbUpdateException` whose inner `PostgresException` has `SqlState` `23505` and
   `ConstraintName` `IX_expense_reports_UserId_Month`.

## Delivery

- Branch `feat/expense-reports` from `develop`, small conventional commits, PR into `develop`.
- README "Mes Notes": a *Tranche 3 — Notes de frais* section listing the work and the assumptions (one report per
  user per month, title stored at creation, months 2000–2100, cascade delete, reports of a deleted user kept).
- Roadmap: Expense Reports marked *Done* with links to this spec and its plan; its open questions replaced by a
  pointer to the decisions above.

## Definition of done

- Unit, Acceptance, E2E and frontend suites green locally and in CI.
- `dotnet run --project tools/N2JSoft.Aspire`: an administrator can create a report for an active user, is told
  in French when it already exists, opens it, and deletes it.
- Scalar shows the four `/v1/expense-reports` operations and the `isActive` parameter of `GET /v1/users`.
- README and roadmap updated; PR `feat/expense-reports` → `develop` opened.
