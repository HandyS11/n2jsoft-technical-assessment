# Expenses — design

Slice 4 of the [roadmap](../roadmap.md). Status: approved design; see the [implementation plan](../plans/2026-10-11-expenses.md).
Builds on the [Foundation](2026-10-10-foundation-design.md), [Users](2026-10-11-users-design.md) and
[Expense Reports](2026-10-11-expense-reports-design.md) designs, whose conventions apply unless stated otherwise here.

## Context

After slice 3, an expense report belongs to one user and one month (`ReportMonth`), holds a stored title, and is
deleted physically. Its creation runs in a transaction (`ITransactionRunner`) that locks the user row
(`FOR SHARE`); `AppDbContext.SaveChangesAsync` translates database errors for every store. The report page shows the
title, the month and a delete button; its expenses section was left to this slice.

The README requires that an expense:

- has a date shown like "mercredi 15 octobre 2025", a description of at most 50 characters, an amount in euros and
  a billing address ("enseigne / rue + code postal + ville");
- stays editable at any time;
- is refused, with an error message, when the user has reached their monthly quota of expenses;
- can be deleted logically.

The report page lets the administrator add expenses and shows the existing ones "par lot de 5".

## Goals

- Add the `Expense` entity with its validation, the monthly quota rule and the report-month rule.
- Expose list, read, create, update and delete under `/v1/expense-reports/{reportId}/expenses`.
- Fill the report page: an add/edit form, the expenses in pages of 5, the report's count and total.
- Show each report's expense count and total in the reports list.

## Non-goals

- Restoring a deleted expense, or listing deleted expenses.
- Moving an expense to another report.
- Receipts (attachments), currencies other than the euro, VAT.
- Filtering, sorting or searching expenses.

## Decisions

Open questions from the roadmap, and the others raised during the brainstorm.

| Question                                            | Decision                                                                                                                         | Why                                                                                                                         |
|-----------------------------------------------------|----------------------------------------------------------------------------------------------------------------------------------|-----------------------------------------------------------------------------------------------------------------------------|
| Must the date fall within the report's month?       | Yes: otherwise `422 expense.date_outside_report_month`; the form's date picker is bounded to the month                         | A report holds a user's expenses for its month, and its title names that month                                               |
| How is the quota counted?                           | The report's live (not deleted) expenses, against the user's current `MonthlyExpenseQuota`; at the limit, `422 expense.quota_exceeded` | One report per user and month (slice 3) plus in-month dates make the report's count the month's count                       |
| Do deleted expenses count?                          | No: deleting an expense frees a slot                                                                                             | A mistake fixed by deleting and re-entering should not burn the quota                                                        |
| Does editing re-check the quota?                    | No; it re-checks the month only                                                                                                  | An edit cannot leave the month, so it cannot change the count                                                                |
| Report of an inactive user                          | Fully usable                                                                                                                     | "Inactive" only forbids assigning the user to a new report                                                                   |
| Report of a deleted user                            | Read-only: adding, editing or deleting an expense answers `422 expense_report.deleted_user`; the page hides the form and actions | The report stays visible (slice 3), but the account should not keep changing after its owner is gone; the quota is unreadable |
| Amount                                              | 0,01 to 999 999,99 €, at most 2 decimals (`numeric(9,2)`); more decimals is a `400`, never rounded                              | Strictly positive expenses; the ceiling catches typos. Refunds are out of scope                                              |
| Billing address                                     | `BillingAddress(StoreName?, Address)`: an optional store name (≤ 100) plus the existing street, 5-digit postal code and city   | Reuses `Address` as the Users design planned; a receipt without a store name (parking, toll) can still be entered            |
| "Par lot de 5"                                      | Numbered pages of 5 (*Précédent* / *Suivant*, "Page X sur Y") through the existing paged API and `pagedList` helper             | Same pattern as the other lists; no new cursor                                                                               |
| Sort                                                | Date, then id (creation order)                                                                                                   | A monthly report reads chronologically                                                                                       |
| Where is an expense edited?                         | The add form on the report page switches to edit mode                                                                            | One form, no new route, the list stays in view                                                                               |
| Date format                                         | ISO `yyyy-MM-dd` in the API; "mercredi 15 octobre 2025" in the UI (`EEEE d MMMM y`, `fr-FR`)                                   | Display is the frontend's concern; `DateOnly` keeps the API unambiguous                                                      |
| Concurrency                                         | Expenses carry an `xmin` version: `ETag` on read, `If-Match` on `PUT` (`412`), `409` for a write saved over another, like users | Expenses are editable, unlike reports                                                                                        |
| Two simultaneous adds at quota − 1                  | Serialised: every expense write locks the report row (`FOR UPDATE`), then the user row (`FOR SHARE`), in that order             | Without the lock both would count N − 1 and pass; the fixed order rules out deadlocks between expense writes                 |
| Expenses of a deleted report                        | Deleted with it by the database (`ON DELETE CASCADE`), deleted expenses included                                                 | Decided in slice 3                                                                                                           |

## Domain

`Domain/ValueObjects/BillingAddress.cs`: `sealed record BillingAddress(string? StoreName, Address Address)`.

- The constructor trims `StoreName`, turns a blank one into `null`, and throws `InvalidValueException` when it is
  longer than `StoreNameMaxLength` (100). `Address` is required and keeps its own validation.
- A private parameterless constructor serves EF Core, like `Address`.

`ReportMonth.Contains(DateOnly date)`: true when the date's year and month are the report's.

`Domain/Entities/Expense.cs`:

```csharp
public sealed class Expense
{
    public const int DescriptionMaxLength = 50;
    public const decimal MinAmount = 0.01m;
    public const decimal MaxAmount = 999_999.99m;

    public Guid Id { get; } = Guid.CreateVersion7();
    public Guid ExpenseReportId { get; private set; }
    public DateOnly Date { get; private set; }
    public string Description { get; private set; }
    public decimal Amount { get; private set; }
    public BillingAddress BillingAddress { get; private set; }
    public DateTimeOffset? DeletedAt { get; private set; }
    public uint Version { get; }

    public static Expense Create(ExpenseReport report, User user, int liveExpenseCount,
        DateOnly date, string description, decimal amount, BillingAddress billingAddress);
    public void Update(ExpenseReport report,
        DateOnly date, string description, decimal amount, BillingAddress billingAddress);
    public void Delete(DateTimeOffset now);
}
```

- Value guards throw `InvalidValueException` (→ `400`): the description is trimmed, non-blank and at most 50
  characters; the amount lies between `MinAmount` and `MaxAmount` and has at most two decimals
  (`decimal.Round(amount, 2) != amount` is refused); the billing address is required.
- Then the rules, in this order, each a `BusinessRuleViolationException` (→ `422`):
  1. `ExpenseDateOutsideReportMonthException` (`expense.date_outside_report_month`) when
     `!report.Period.Contains(date)`, in `Create` and `Update`;
  2. `ExpenseQuotaExceededException` (`expense.quota_exceeded`) when `liveExpenseCount >= user.MonthlyExpenseQuota`,
     in `Create` only.
- `Create` also checks that `user.Id == report.UserId` (`ArgumentException`: a programming error, not a request
  error). `Update` checks that `report.Id == ExpenseReportId`, likewise.
- Every check runs before any change, so a refused update leaves the expense as it was.
- `Delete` is idempotent and keeps the first deletion date.

`Domain/Exceptions/DeletedUserException.cs` (`expense_report.deleted_user`), thrown by the expense handlers when the
report's user is soft-deleted (the domain cannot see a user that the query filter hides).

## Persistence

- `ExpenseEntityTypeConfiguration`: table `expenses`.
  - `Date` as `date`; `Description` required, at most 50; `Amount` as `numeric(9,2)`.
  - `BillingAddress` as a `ComplexProperty` with `Address` nested in it: columns `BillingAddress_StoreName`
    (nullable, at most 100), `BillingAddress_Address_Street`, `BillingAddress_Address_PostalCode`,
    `BillingAddress_Address_City`, sized like the users' address.
  - Global query filter `DeletedAt == null`; `Version` as row version (`xmin`).
  - Foreign key `ExpenseReportId` → `expense_reports.Id`, `OnDelete(Cascade)`, without navigation properties.
  - Index `(ExpenseReportId, Date)`, for the list and the count.
- Migration `Expenses` adds the table, as generated by EF.
- `AppDbContext`: applies the configuration. Its `SaveChangesAsync` maps a `DbUpdateConcurrencyException` on an
  `Expense` to `ConcurrencyConflictException`, like a user (only a report maps to `NotFoundException`).
- The seed is unchanged.

## Application

```text
Application/
  Expenses/
    IExpenseStore.cs
    ExpenseBillingAddress.cs   (StoreName?, Street, PostalCode, City)
    ExpenseSummary.cs          (Id, Date, Description, Amount, ExpenseBillingAddress BillingAddress)
    ExpenseDetails.cs          ExpenseSummary's fields + Version
    Commands/  CreateExpense.cs   CreateExpenseCommand(ReportId, Date, Description, Amount, BillingAddress) + handler
               UpdateExpense.cs   UpdateExpenseCommand(ReportId, Id, …, uint? ExpectedVersion) + handler
               DeleteExpense.cs   DeleteExpenseCommand(ReportId, Id) + handler (uses TimeProvider)
  ExpenseReports/
    IExpenseReportStore.cs     gains FindLockedAsync
    ExpenseReportSummary.cs    gains ExpenseCount, Total
    ExpenseReportDetails.cs    gains ExpenseCount, Total, MonthlyExpenseQuota, IsReadOnly
Infrastructure/
  Expenses/ExpenseStore.cs
```

```csharp
public interface IExpenseStore
{
    // Reads: AsNoTracking projections of live expenses, no logic.
    Task<PagedResult<ExpenseSummary>> ListAsync(Guid reportId, PageRequest request, CancellationToken cancellationToken);
    Task<ExpenseDetails?> GetDetailsAsync(Guid reportId, Guid id, CancellationToken cancellationToken);
    Task<int> CountLiveAsync(Guid reportId, CancellationToken cancellationToken);

    // Writes.
    /// The tracked expense; null when unknown, deleted, or in another report.
    Task<Expense?> FindAsync(Guid reportId, Guid id, CancellationToken cancellationToken);
    void Add(Expense expense);
    Task SaveChangesAsync(CancellationToken cancellationToken);
}
```

`IExpenseReportStore.FindLockedAsync(Guid id)`: the tracked report, read with `SELECT … FOR UPDATE`.

All three handlers run in one transaction (`ITransactionRunner`) and start the same way, so locks are always taken
in the same order:

1. `expenseReportStore.FindLockedAsync(reportId)` (`FOR UPDATE`): `NotFoundException` ("ExpenseReport") when null.
   Concurrent writes to the same report's expenses, and a concurrent deletion of the report, wait for the commit.
2. `userStore.FindLockedAsync(report.UserId)` (`FOR SHARE`): `DeletedUserException` when null (the user's query
   filter hides a deleted user; reports never point to an unknown one). A concurrent deletion or quota change of the
   user waits for the commit.

Then:

- **`CreateExpenseHandler`**: `CountLiveAsync(reportId)`, `Expense.Create(report, user, count, …)`, `Add`, save;
  returns the `ExpenseDetails`.
- **`UpdateExpenseHandler`**: `FindAsync(reportId, id)` → `NotFoundException` ("Expense"); when `ExpectedVersion` is
  given and differs from `expense.Version`, `ConcurrencyConflictException`; `expense.Update(report, …)`; save.
- **`DeleteExpenseHandler`**: `FindAsync` → `NotFoundException`; `expense.Delete(timeProvider.GetUtcNow())`; save.

Reads:

- `ExpenseStore.ListAsync` sorts by `Date`, then `Id`. A report that does not exist answers `404` (the endpoint asks
  `IExpenseReportStore.ExistsAsync(id)` first), so an empty list always means an existing, empty report.
- `ExpenseReportSummary` and `ExpenseReportDetails` compute `ExpenseCount` and `Total` (sum of `Amount`, `0` when
  empty) over the report's live expenses with correlated subqueries.
- `ExpenseReportDetails.MonthlyExpenseQuota` and `IsReadOnly` (the user is deleted) come from the report's user,
  read with `IgnoreQueryFilters()` in a second query: in the same query it would also bring the deleted expenses back
  into the counts. This is the only report read that touches `users`; the list still does not.
- `ExpenseReportDetails.From(report, user)` describes a report just created: no expense, a total of 0, the user's
  quota, not read-only.

`AddApplication()` registers the three handlers; `AddInfrastructure()` registers the store, scoped.

## API

All routes under `/v1/expense-reports/{reportId}/expenses`.

| Method   | Route   | Body                 | Success                                                      | Errors                          |
|----------|---------|----------------------|--------------------------------------------------------------|---------------------------------|
| `GET`    | `/`     | —                    | `200 PagedResult<ExpenseSummary>`                            | `400`, `404`                    |
| `GET`    | `/{id}` | —                    | `200 ExpenseDetails`, `ETag`                                 | `404`                           |
| `POST`   | `/`     | `SaveExpenseRequest` | `201`, `Location: …/expenses/{id}`, `ETag`, `ExpenseDetails` | `400`, `404`, `422`             |
| `PUT`    | `/{id}` | `SaveExpenseRequest` | `204`                                                        | `400`, `404`, `409`, `412`, `422` |
| `DELETE` | `/{id}` | —                    | `204`                                                        | `404`, `409`, `422`             |

- `SaveExpenseRequest { date, description, amount, billingAddress }` is a public record shared by create and update:
  `[Required] DateOnly? Date`; `[Required, StringLength(50)] string Description`;
  `[Required, Range(typeof(decimal), "0.01", "999999.99")] decimal? Amount`; `[Required] BillingAddressRequest`.
  `BillingAddressRequest { storeName?, street, postalCode, city }` has `[StringLength(100)]` on `StoreName` and
  the same attributes as `AddressRequest` on the rest. A third decimal is refused by the domain, still as a `400`.
- `422` codes: `expense.quota_exceeded` (`POST`), `expense.date_outside_report_month` (`POST`, `PUT`),
  `expense_report.deleted_user` (`POST`, `PUT`, `DELETE`).
- `404` for an unknown report, and for an expense that is unknown, deleted, or in another report — including a
  second `DELETE`.
- `ETag` / `If-Match` follow the users' format (`"<xmin>"`); the `If-Match` parsing in `UserEndpoints` moves to a
  shared helper both endpoint classes use (`Endpoints/EntityTags.cs`).
- `GET /v1/expense-reports` and `GET /v1/expense-reports/{id}` return the new fields (`expenseCount`, `total`, and
  on the detail `monthlyExpenseQuota`, `isReadOnly`).

## Frontend

New feature folder `src/app/features/expenses/`, composed by the existing report page. Standalone components with
signals, French text.

- **Report page** (`expense-report.page.ts`, extended):
  - Header as today, plus "3 dépenses sur 10 ce mois-ci" and "Total : 1 234,56 €" (`currency` pipe, `EUR`,
    `fr-FR`). For a read-only report, "3 dépenses" without the quota.
  - A read-only report shows "L'utilisateur de cette note de frais a été supprimé : elle est en lecture seule."
    instead of the form, and the table has no actions. The report itself can still be deleted.
  - The report's delete button reads *Supprimer la note de frais*, so the rows' *Supprimer* stay unambiguous.
  - The form, then the expenses table.
- **`ExpenseFormComponent`** (`expense-form.component.ts`), inputs `reportId`, `period` (year and month), and the
  expense being edited (or none); output `saved`.
  - Typed reactive form: *Date* (`<input type="date">`, `min` and `max` the report month's first and last day),
    *Description* (required, at most 50, with a "12/50" counter), *Montant (€)* (`step="0.01"`, between 0,01 and
    999 999,99, two decimals at most), *Enseigne (facultatif)*, *Rue*, *Code postal*, *Ville*. Validators mirror
    the API, with French messages once a field is touched.
  - Add mode: *Ajouter*. Edit mode, entered by *Modifier* on a row: the page loads `GET …/{id}` (for its `version`)
    and fills the form; the button reads *Enregistrer*, with *Annuler* to return to add mode. A failed load shows
    "Dépense introuvable." and reloads the list.
  - On success it emits `saved` and returns to an empty add form; the page reloads the current page of expenses
    and the report header, and shows "Dépense ajoutée." or "Dépense modifiée.".
  - Errors in a `role="alert"` message, through `businessRuleCode()`:

    | Response                              | Message                                                                                         |
    |---------------------------------------|-------------------------------------------------------------------------------------------------|
    | `422 expense.quota_exceeded`          | Le quota mensuel de dépenses de cet utilisateur est atteint : impossible d'ajouter cette dépense. |
    | `422 expense.date_outside_report_month` | La date doit être comprise dans le mois de la note de frais.                                  |
    | `422 expense_report.deleted_user`     | L'utilisateur de cette note de frais a été supprimé : elle est en lecture seule.                |
    | `412`                                 | Cette dépense a été modifiée entre-temps. Rechargez-la avant de l'enregistrer.                  |
    | `404`                                 | Note de frais introuvable. (adding) / Dépense introuvable. (editing)                            |
    | `400`                                 | Les informations saisies sont invalides.                                                        |
    | anything else                         | L'opération a échoué. Veuillez réessayer.                                                       |

    A `deleted_user` answer also reloads the report header, which turns the page read-only.
- **Expenses table**: *Date* ("mercredi 15 octobre 2025", `formatDate` with `EEEE d MMMM y` and `fr-FR`),
  *Description*, *Montant*, *Adresse de facturation* ("Fnac, 12 rue de Rivoli, 75001 Paris", the store name
  omitted when absent), *Actions* (*Modifier*; *Supprimer*: native `confirm()` "Supprimer la dépense « Taxi » ?",
  then delete and reload). Pages of 5
  through `pagedList`, with *Précédent* / *Suivant* and "Page X sur Y". "Aucune dépense." when empty; a failed load
  shows "Impossible de charger les dépenses.".
- **`ExpensesApi`**: `list(reportId, page, pageSize)`, `get(reportId, id)` (the body carries `version`),
  `create(reportId, request)`, `update(reportId, id, request, version)` (`If-Match: "<version>"`, like
  `UsersApi.update`), `delete(reportId, id)`;
  `ExpenseSummary` and `ExpenseDetails` interfaces mirror the API.
- **Reports list**: the table gains *Dépenses* and *Total* columns; `ExpenseReportSummary` and
  `ExpenseReportDetails` gain the new fields.

## Test tiers

Written test-first.

- **Unit**:
  - `BillingAddress`: trimming, blank store name to `null`, length limit, required address.
  - `ReportMonth.Contains`: first and last day of the month, the day before and the day after.
  - `Expense.Create`: success; a count of quota − 1 accepted and of quota refused with its code; a date outside the
    month refused with its code; description of 50 accepted, 51 and blank refused; amounts 0.01 and 999 999.99
    accepted, 0, −1, 0.001 and 1 000 000 refused; a user who is not the report's refused.
  - `Expense.Update`: changes every field; refuses a date outside the month and leaves the expense unchanged; never
    checks the quota. `Delete`: sets the date once.
  - Handlers, with NSubstitute stores and `FakeTimeProvider`: each saves on success; an unknown report, a deleted
    user, an unknown expense, a stale version and each rule throw without saving.
- **Acceptance**: features `ListExpenses`, `CreateExpense`, `GetExpense`, `UpdateExpense` and `DeleteExpense`,
  reusing `ApiDriver` and `HttpSteps`, plus scenarios in the report features. They cover `201` with `Location` and
  `ETag`; a `400` outline (missing fields, description of 51, amount 0 and 0.001 and 1 000 000, bad postal code,
  store name of 101); `422` with its code for the quota at the limit, a date outside the month, and a deleted user
  on `POST`, `PUT` and `DELETE`; a deleted expense freeing a quota slot; editing at the quota limit succeeding;
  `412` on a stale `If-Match` and `204` on a fresh one; `404` for an unknown report, an expense of another report,
  and a second `DELETE`; paging by 5 in date order; a deleted report taking its expenses with it; the reports list
  and detail showing count and total, deleted expenses excluded.
- **End-to-end**: each journey creates its own uniquely named user (quota 2) and report through the API.
  1. Add an expense from the report page; its row shows the date as "mercredi 15 octobre 2025".
  2. Edit its amount; the row and the total show the new amount.
  3. Add expenses up to the quota, then one more; the quota message shows.
  4. Delete an expense after confirming; it is gone and an expense can be added again.
- **Frontend (Vitest)**: `expenses.api.spec` (verbs, URLs, bodies, `If-Match`, `ETag`); `expense-form.component.spec`
  (add and edit modes, validators and messages, the counter, date bounds, cancel, each error message, `saved`);
  `expense-report.page.spec` extended (counts and total, table rows and date format, paging, edit flow, delete
  confirmed and cancelled, read-only state, load error); `expense-report-list.page.spec` (new columns).

## Risks to verify first

All three were verified while planning, in a throwaway worktree against Postgres 18.3: each holds as designed (see the plan's "Verified during planning"), so no fallback applies.

1. EF Core 10 with Npgsql maps a nested complex property (`BillingAddress` holding `Address`) with a nullable
   `StoreName`, in a migration and in a projection into `ExpenseSummary` (fallback: flatten `BillingAddress` into
   four properties on `Expense`).
2. `FromSql` with `FOR UPDATE` on `expense_reports` followed by `FOR SHARE` on `users` in the same transaction, and a
   concurrent report `DELETE` waiting rather than deadlocking.
3. `OnDelete(Cascade)` on a foreign key without navigation properties generates `ON DELETE CASCADE`, and deleting a
   report through EF removes its expenses (soft-deleted ones included) without loading them.

## Delivery

- Branch `feat/expenses` from `develop`, small conventional commits, PR into `develop`.
- README "Mes Notes": a *Tranche 4 — Dépenses* section listing the work and the assumptions (dates within the
  report's month, quota counted on live expenses and not re-checked on edit, a deleted user's reports read-only,
  amount limits, optional store name).
- Roadmap: Expenses marked *Done* with links to this spec and its plan; its open questions replaced by a pointer to
  the decisions above.

## Definition of done

- Unit, Acceptance, E2E and frontend suites green locally and in CI.
- `dotnet run --project tools/N2JSoft.Aspire`: an administrator can add, edit and delete expenses on a report, page
  through them by 5, sees dates as "mercredi 15 octobre 2025", and is told in French when the quota is reached.
- Scalar shows the five `/v1/expense-reports/{reportId}/expenses` operations.
- README and roadmap updated; PR `feat/expenses` → `develop` opened.
