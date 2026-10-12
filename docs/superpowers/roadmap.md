# Roadmap — N2JSoft expense management assessment

This file is the hand-off point between working sessions. Each slice below goes through its own
superpowers cycle; a session starting slice N reads this file and the specs of the slices already
done, then brainstorms only what is new.

## How a slice is delivered

1. **Brainstorm** the slice (`superpowers:brainstorming`), starting from its "Open questions" below.
2. **Spec** saved to `docs/superpowers/specs/YYYY-MM-DD-<slice>-design.md`, reviewed and approved.
3. **Plan** written with `superpowers:writing-plans`, saved to `docs/superpowers/plans/`.
4. **Execute** the plan on a branch `feat/<slice>` cut from `develop`, TDD across the test tiers.
5. **PR** into `develop`; CI green; README "Mes Notes" updated; this file's status updated.

## Slices

| # | Slice           | Scope                                                                                                                                               | Depends on | Status      | Spec                                                         |
|---|-----------------|-----------------------------------------------------------------------------------------------------------------------------------------------------|------------|-------------|--------------------------------------------------------------|
| 1 | Foundation      | Clean Architecture restructure, API versioning, OpenAPI + Scalar, error contract, Redis removal, 3 backend test tiers, frontend shell, CI split | —          | Done        | [foundation](specs/2026-10-10-foundation-design.md), [plan](plans/2026-10-10-foundation.md) |
| 2 | Users           | Full user model (address, active flag, monthly expense quota, soft delete), CRUD API, users management UI                                       | 1          | Done        | [users](specs/2026-10-11-users-design.md), [plan](plans/2026-10-11-users.md) |
| 3 | Expense Reports | Report entity, generated title "Prénom Nom - Mois Année", create from user + month (active users only), physical delete, reports list page       | 2          | Done        | [expense reports](specs/2026-10-11-expense-reports-design.md), [plan](plans/2026-10-11-expense-reports.md) |
| 4 | Expenses        | Expense entity and validation, monthly quota rule, edit, soft delete, report page with add form and expenses in pages of 5                     | 3          | Done        | [expenses](specs/2026-10-11-expenses-design.md), [plan](plans/2026-10-11-expenses.md) |
| 5 | Wrap-up         | OpenTelemetry for the webapi, shared pagination component, `prestart` removal, fresh-clone run, final "Mes Notes" with a traceability table; private GitHub repo + invite `n2jsoft-hr-cr` | 4 | Done | [wrap-up](specs/2026-10-11-wrap-up-design.md), [plan](plans/2026-10-11-wrap-up.md) |
| 6 | UI polish       | Tailwind actually compiled (`.postcssrc.json`), shared control styles, status filter on the users page, link from a report to its user        | 5          | Done        | — (bounded change, designed in conversation)                  |

## Conventions every slice inherits

These are decided in the Foundation spec; slices follow them unless their own spec says otherwise.

- **Layers:** `Domain ← Application ← Infrastructure`, `WebApi` is the composition root. Enforced by
  architecture tests.
- **Lightweight CQRS, no mediator:** one command handler per write use case (loads entities, calls domain
  methods, saves). Data access goes through one **store per feature** (`I<Feature>Store` in Application,
  implemented in Infrastructure over the scoped `AppDbContext`): reads are `AsNoTracking` projections, writes
  are `FindAsync` / `Add` / `SaveChangesAsync`. No repository, no `IUnitOfWork`: the `DbContext` already is
  the unit of work. (Decided in slice 2; supersedes the foundation spec.)
- **Business rules live in Domain entities** and are unit-tested there or through the command
  handlers. Store reads hold no logic and are covered by the Acceptance tier.
- **Errors:** request shape validated by .NET 10 built-in minimal-API validation (DataAnnotations → `400`).
  Request records must be declared `public`: the validation source generator silently skips other types.
  `NotFoundException` → `404`; `BusinessRuleViolationException` → `422` with a stable `code` extension
  (e.g. `expense.quota_exceeded`). Unknown routes and unsupported versions answer a `404` ProblemDetails.
  The UI maps `code` to its French message.
- **Business rules:** codes are `<feature>.<rule>` (`expense_report.already_exists`). A rule that a concurrent
  request can break between the check and the save (uniqueness) is also enforced by a database constraint, which
  the store maps to the same exception; its race is tested at the store level (two DI scopes, both read, then both
  save). The UI reads the code with `businessRuleCode` (`core/problem-details.ts`) and maps it to French.
- **Rules over a count** (the monthly quota): the handler locks the parent row (`FOR UPDATE`) in its transaction
  before counting, and always locks in the same order (report, then user), so concurrent writes run one after the
  other. The race is tested with two concurrent HTTP requests while another connection holds the parent's lock, released
  once both wait on it (`DatabaseLocks`): without the handler's lock, both would pass the count.
- **API:** URL-segment versioning, all endpoints under `/v1/...`; the Angular app calls `/api/v1/...`
  through the dev proxy. Lists are paged: `?page=&pageSize=` (1-based, default 20, at most 100) returning
  `PagedResult<T>` (`items`, `page`, `pageSize`, `totalCount`).
- **Telemetry:** configured in `WebApi` only (`AddTelemetry()`); exported over OTLP only when
  `OTEL_EXPORTER_OTLP_ENDPOINT` is set (Aspire sets it). Domain and Application never reference OpenTelemetry.
- **Tests:** xUnit v3 on Microsoft.Testing.Platform, Shouldly, NSubstitute.
  - *Unit* — Domain + command handlers.
  - *Acceptance* — Reqnroll (Gherkin in **English**) + Testcontainers Postgres + `WebApplicationFactory`.
    *Given* arranges data through `AppDbContext`, *When* calls HTTP, *Then* asserts responses.
    Reuse ApiDriver and HttpSteps ("When I request …", "Then the response is a problem with status …").
  - *End-to-end* — Aspire.Hosting.Testing boots the AppHost, Playwright drives the Angular UI through
    a few critical journeys per slice. Derive page tests from PageTest; the demo seed (30 users, among them Juste
    Leblanc and Marc Assin, and a year of reports) is present, so find a list row with FindRowAsync, which pages.
  - *Frontend* — Vitest specs per page and API service.
- **Frontend:** feature folders under `src/app/features/<feature>/`, standalone components with
  signals, lists paged through `pagedList` and `<app-pagination>`, UI text in French, `fr-FR` locale, Playwright
  locators by role/text (no `data-testid`).
- **Delivery:** small conventional commits, no AI attribution in commits or PRs, "Mes Notes" in French.

## Open questions per slice

Ambiguities in the README to settle during each slice's brainstorm.

### Users

Settled in the [Users spec](specs/2026-10-11-users-design.md#decisions).

### Expense Reports

Settled in the [Expense Reports spec](specs/2026-10-11-expense-reports-design.md#decisions).

### Expenses

Settled in the [Expenses spec](specs/2026-10-11-expenses-design.md#decisions).

### Wrap-up

Settled in the [Wrap-up spec](specs/2026-10-11-wrap-up-design.md#decisions).
