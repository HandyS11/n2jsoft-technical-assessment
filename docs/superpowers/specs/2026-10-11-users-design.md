# Users — design

Slice 2 of the [roadmap](../roadmap.md). Status: approved design, pending implementation plan.
Builds on the [Foundation design](2026-10-10-foundation-design.md), whose conventions apply unless stated
otherwise here.

## Context

After slice 1, `User` holds only `Id`, `FirstName` and `LastName`. The API exposes a paged
`GET /v1/users` and the Angular shell shows the first page of names. There are no command handlers yet:
the write side is created by this slice and becomes the template slices 3 and 4 copy.

The README requires that a user has a first name, a last name and a postal address; a maximum number of
expenses per calendar month; an active/inactive state (only active users can be assigned to an expense
report); and can be deleted logically. It names no user page.

## Goals

- Complete the `User` model: postal address, monthly expense quota, active flag, soft delete.
- Introduce the write side (command handlers over a per-feature store) and replace the foundation's read-side
  query interface with that same store.
- Expose create, read, update, activate/deactivate and delete under `/v1/users`.
- Give the administrator a French management UI: paged list, create/edit form, activation toggle, delete.

## Non-goals

- Restoring a deleted user, or listing deleted users.
- Enforcing the quota (slice 4, when expenses are created) or the "active users only" rule on reports
  (slice 3). This slice only stores the quota and the flag.
- Filtering or searching the user list.
- Any `422` response: no business rule of this slice can be violated through the API.

## Decisions

Open questions from the roadmap, settled during the brainstorm.

| Question                                   | Decision                                                                                                   | Why                                                                                            |
|--------------------------------------------|------------------------------------------------------------------------------------------------------------|------------------------------------------------------------------------------------------------|
| Which screens?                             | Paged list, one create/edit form, activate/deactivate from the list, delete with confirmation             | Quota and active flag must be settable for slices 3–4 to be demonstrable                        |
| Address shape                              | `Address(Street, PostalCode, City)` value object; French 5-digit postal code; no country                  | Matches the billing address of slice 4, which reuses it and adds an optional store name        |
| Quota bounds                               | Whole number ≥ 1, no upper bound                                                                           | The README sets none; 0 would overlap with "inactive". Documented as an assumption              |
| Is the quota editable?                     | Yes, like any field. Lowering it below a month's existing count is allowed and only affects new expenses | No coupling from Users to Expenses; slice 4 checks the quota when an expense is created         |
| Text lengths                               | First and last name ≤ 100, street ≤ 200, city ≤ 100 (after trimming, non-blank)                           | Column sizing only; documented as an assumption                                                |
| What does soft delete mean?                | `DeletedAt` + EF global query filter: hidden from every list, read, update and assignment; answers `404` | One mechanism hides deleted users everywhere; existing reports keep their stored title (slice 3) |
| Inactive users                             | Still listed, with an *Inactif* status; still editable                                                   | Inactive only restricts assignment to reports                                                   |
| New users                                  | Created active                                                                                            | The common case; no extra form field                                                           |
| Write API style                            | Task-based: `PUT` for details, `POST …/activate` and `…/deactivate`, `DELETE`                            | Rich entity with intention-revealing methods; the list toggle needs no full payload            |
| Activating an active user (or vice versa) | Idempotent `204`, no error                                                                                 | Friendlier for a toggle; the README states no such rule                                        |
| Data access (supersedes the foundation)    | One store per feature (`IUserStore` in Application, `UserStore(AppDbContext)` in Infrastructure) for reads and writes; no repository, no `IUnitOfWork`, no `IUserQueries` | The `DbContext` already is the unit of work; one typed facade per feature keeps Application free of EF Core |

## Domain

`Domain/ValueObjects/Address.cs`: `sealed record Address(string Street, string PostalCode, string City)`.
Its constructor trims the values and throws `ArgumentException` when a value is blank, too long, or the postal
code is not exactly five digits.

`Domain/Entities/User.cs` gets private setters and behaviour:

```csharp
public sealed class User
{
    public Guid Id { get; private set; }
    public string FirstName { get; private set; }
    public string LastName { get; private set; }
    public Address Address { get; private set; }
    public int MonthlyExpenseQuota { get; private set; }
    public bool IsActive { get; private set; }
    public DateTimeOffset? DeletedAt { get; private set; }

    public static User Create(string firstName, string lastName, Address address, int monthlyExpenseQuota);
    public void UpdateDetails(string firstName, string lastName, Address address, int monthlyExpenseQuota);
    public void Activate();
    public void Deactivate();
    public void Delete(DateTimeOffset now);
}
```

- `Create` starts the user active and not deleted.
- `Create` and `UpdateDetails` trim names and throw `ArgumentException` for a blank or too long name or a
  quota below 1. These guards are a backstop: request validation rejects such input with a `400` first.
  `BusinessRuleViolationException` stays reserved for real business rules (slices 3 and 4).
- `Activate`, `Deactivate` and `Delete` are idempotent; `Delete` keeps the first deletion date.

## Persistence

- `UserEntityTypeConfiguration` maps the lengths above, `Address` as a `ComplexProperty` (columns
  `Address_Street`, `Address_PostalCode`, `Address_City`, keeping the existing PascalCase column style), and
  a global query filter `DeletedAt == null`.
- Migration `UserDetails` adds the columns as generated by EF. No backfill is designed: the AppHost's Postgres
  has no data volume, so every run starts from an empty database, migrates, then seeds; the test tiers do the
  same. (Rows with an empty address could not be loaded anyway, since `Address` validates on construction.)
- The dev seed gives Juste Leblanc and Marc Assin a real address and a quota of 10, through `User.Create`.

## Application

This slice replaces the foundation's data-access convention (write-side repositories + `IUnitOfWork`, read-side
query interfaces). The `DbContext` already is the unit of work, so each feature gets one **store**: an interface
in Application, implemented in Infrastructure as a thin class over the scoped `AppDbContext`.

```text
Application/
  Users/
    IUserStore.cs        the feature's data access (below)
    UserSummary.cs       (Id, Name, City, MonthlyExpenseQuota, IsActive)      moved from Users/Queries/
    UserDetails.cs       (Id, FirstName, LastName, UserAddress Address, MonthlyExpenseQuota, IsActive)
    Commands/  CreateUser.cs         CreateUserCommand + CreateUserHandler (returns the new id)
               UpdateUser.cs         UpdateUserCommand + UpdateUserHandler
               SetUserActivation.cs  SetUserActivationCommand(Id, IsActive) + handler
               DeleteUser.cs         DeleteUserCommand + DeleteUserHandler (uses TimeProvider)
  DependencyInjectionExtensions.cs   AddApplication(): registers the handlers and TimeProvider.System
Infrastructure/
  Users/UserStore.cs     replaces UserQueries.cs
```

```csharp
public interface IUserStore
{
    // Reads: AsNoTracking projections, no logic.
    Task<PagedResult<UserSummary>> ListAsync(PageRequest request, CancellationToken cancellationToken);
    Task<UserDetails?> GetDetailsAsync(Guid id, CancellationToken cancellationToken);

    // Writes: tracked entities, changed through their domain methods.
    Task<User?> FindAsync(Guid id, CancellationToken cancellationToken);
    void Add(User user);
    Task SaveChangesAsync(CancellationToken cancellationToken); // AppDbContext.SaveChangesAsync
}
```

- Read endpoints call the store directly (`ListAsync`, `GetDetailsAsync`); write endpoints call a handler.
- Commands carry the `Address` value object; the endpoint builds it from the validated request.
- A handler whose `FindAsync` returns `null` throws `NotFoundException` and saves nothing.
- The list keeps its sort: last name, first name, id.
- Stores are scoped and share the request's `AppDbContext`: when slice 4 changes two features in one use case,
  one `SaveChangesAsync` commits both.

## API

All routes under `/v1/users`.

| Method   | Route              | Body              | Success                                   | Errors     |
|----------|--------------------|-------------------|-------------------------------------------|------------|
| `GET`    | `/`                | —                 | `200 PagedResult<UserSummary>`            | `400`      |
| `GET`    | `/{id}`            | —                 | `200 UserDetails`                         | `404`      |
| `POST`   | `/`                | `SaveUserRequest` | `201`, `Location: /v1/users/{id}`, `UserDetails` | `400` |
| `PUT`    | `/{id}`            | `SaveUserRequest` | `204`                                     | `400`, `404` |
| `POST`   | `/{id}/activate`   | —                 | `204`                                     | `404`      |
| `POST`   | `/{id}/deactivate` | —                 | `204`                                     | `404`      |
| `DELETE` | `/{id}`            | —                 | `204`                                     | `404`      |

- `SaveUserRequest { firstName, lastName, address { street, postalCode, city }, monthlyExpenseQuota }` is a
  public record shared by create and update, validated with DataAnnotations: `[Required]` and `[StringLength]`
  on texts, `[RegularExpression(@"^\d{5}$")]` on the postal code, `[Range(1, int.MaxValue)]` on the quota,
  `[Required]` on the address, whose nested record is validated too.
- A deleted or unknown id answers `404` on every route, including a second `DELETE`.

## Frontend

Feature folder `src/app/features/users/`, standalone components with signals, French text.

- **Routes:** `/users` (list), `/users/new` and `/users/:id/edit` (one `user-form.page.ts`).
- **List page:** a table with *Nom*, *Ville*, *Quota mensuel*, *Statut* (*Actif* / *Inactif*) and actions:
  *Modifier* (link to the form), *Activer* / *Désactiver*, *Supprimer* (native `confirm()`, then delete). Each
  action reloads the current page. A *Nouvel utilisateur* link opens the empty form. *Précédent* / *Suivant*
  buttons and "Page X sur Y" page through 20 users at a time. A failed load or action shows a `role="alert"`
  message.
- **Form page:** typed reactive forms with *Prénom*, *Nom*, *Rue*, *Code postal*, *Ville*,
  *Quota mensuel de dépenses*. Validators mirror the API rules, with French messages once a field is touched.
  Edit mode loads `GET /users/{id}` and shows "Utilisateur introuvable." on `404`. Submitting calls `POST` or
  `PUT`, then navigates to `/users`; a server `400` shows "Les informations saisies sont invalides."
- **`UsersApi`:** `list(page, pageSize)`, `get(id)`, `create`, `update`, `activate`, `deactivate`, `delete`;
  `UserSummary` and `UserDetails` interfaces mirror the API.

## Test tiers

Written test-first.

- **Unit:**
  - `User`: creation, update, idempotent activation and deactivation, deletion date, and every guard.
  - `Address`: trimming and guards.
  - Each handler: saves on success; throws `NotFoundException` without saving for an unknown id. Uses an
    NSubstitute `IUserStore` and `FakeTimeProvider` (`Microsoft.Extensions.TimeProvider.Testing`).
  - The store holds no logic and is covered by the Acceptance tier.
- **Acceptance:** `Users.feature` splits into `ListUsers`, `CreateUser`, `GetUser`, `UpdateUser`,
  `ChangeUserActivation` and `DeleteUser` features, reusing `ApiDriver` and `HttpSteps`. They cover every
  success path (including `201` with `Location`), a `400` outline (blank fields, bad postal code, quota 0,
  missing address), `404` for unknown ids, and a deleted user disappearing from the list, `GET`, `PUT` and a
  second `DELETE`.
- **End-to-end:** each journey creates its own uniquely named user, leaving the seeded users untouched:
  1. create a user from the form and find them in the list;
  2. edit their city and see it in the list;
  3. deactivate them (*Inactif*), then delete them after confirming, and they are gone.

  The existing seeded-users test moves from list items to table rows.
- **Frontend (Vitest):** `users.api.spec` (verbs, URLs, bodies), `user-list.page.spec` (rows, empty and error
  states, pagination, toggle, delete confirmed and cancelled), `user-form.page.spec` (create and edit modes,
  validation messages, submit then navigation, not-found state).

## Risks to verify first

The implementation plan starts with a spike for each; a failing spike changes the plan before dependent work.

1. .NET 10 minimal-API validation validates the nested `address` record of `SaveUserRequest` (fallback: flatten
   the address fields into the request).
2. EF Core 10 with Npgsql maps `Address` as a `ComplexProperty` with a migration, a global query filter and a
   projection into `UserDetails` (fallback: `OwnsOne`).

## Delivery

- Branch `feat/users` from `develop`, small conventional commits, PR into `develop`.
- README "Mes Notes": a *Tranche 2 — Utilisateurs* section listing the work and the assumptions (quota ≥ 1
  without ceiling, text lengths, soft delete hides users everywhere); the pagination item leaves
  "Pistes d'amélioration".
- Roadmap: Users marked *Done* with links to this spec and its plan; its open questions replaced by a pointer
  to the decisions above; the inherited data-access convention now describes the per-feature store.

## Definition of done

- Unit, Acceptance, E2E and frontend suites green locally and in CI.
- `dotnet run --project tools/N2JSoft.Aspire`: an administrator can create, edit, deactivate, reactivate and
  delete a user, and page through the list.
- Scalar shows the seven `/v1/users` operations.
- README and roadmap updated; PR `feat/users` → `develop` opened.
