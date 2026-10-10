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
| 2 | Users           | Full user model (address, active flag, monthly expense quota, soft delete), CRUD API, users management UI                                       | 1          | Not started | —                                                            |
| 3 | Expense Reports | Report entity, generated title "Prénom Nom - Mois Année", create from user + month (active users only), physical delete, reports list page       | 2          | Not started | —                                                            |
| 4 | Expenses        | Expense entity and validation, monthly quota rule, edit, soft delete, report page with add form and expenses in pages of 5                     | 3          | Not started | —                                                            |
| 5 | Wrap-up         | Final "Mes Notes", polish, private GitHub repo + invite `n2jsoft-hr-cr`                                                                            | 4          | Not started | —                                                            |

## Conventions every slice inherits

These are decided in the Foundation spec; slices follow them unless their own spec says otherwise.

- **Layers:** `Domain ← Application ← Infrastructure`, `WebApi` is the composition root. Enforced by
  architecture tests.
- **Lightweight CQRS, no mediator:** one command handler per write use case (loads entities through
  write-side repositories, calls domain methods, saves through `IUnitOfWork`); read side is query
  interfaces declared in Application and implemented in Infrastructure as `AsNoTracking` projections.
- **Business rules live in Domain entities** and are unit-tested there or through the command
  handlers. Queries hold no logic and are covered by the Acceptance tier.
- **Errors:** request shape validated by .NET 10 built-in minimal-API validation (DataAnnotations → `400`).
  Request records must be declared `public`: the validation source generator silently skips other types.
  `NotFoundException` → `404`; `BusinessRuleViolationException` → `422` with a stable `code` extension
  (e.g. `expense.quota_exceeded`). Unknown routes and unsupported versions answer a `404` ProblemDetails.
  The UI maps `code` to its French message.
- **API:** URL-segment versioning, all endpoints under `/v1/...`; the Angular app calls `/api/v1/...`
  through the dev proxy. Lists are paged: `?page=&pageSize=` (1-based, default 20, at most 100) returning
  `PagedResult<T>` (`items`, `page`, `pageSize`, `totalCount`).
- **Tests:** xUnit v3 on Microsoft.Testing.Platform, Shouldly, NSubstitute.
  - *Unit* — Domain + command handlers.
  - *Acceptance* — Reqnroll (Gherkin in **English**) + Testcontainers Postgres + `WebApplicationFactory`.
    *Given* arranges data through `AppDbContext`, *When* calls HTTP, *Then* asserts responses.
    Reuse ApiDriver and HttpSteps ("When I request …", "Then the response is a problem with status …").
  - *End-to-end* — Aspire.Hosting.Testing boots the AppHost, Playwright drives the Angular UI through
    a few critical journeys per slice. Derive page tests from PageTest; the dev seed (Juste Leblanc, Marc Assin) is
    present.
  - *Frontend* — Vitest specs per page and API service.
- **Frontend:** feature folders under `src/app/features/<feature>/`, standalone components with
  signals, UI text in French, `fr-FR` locale, Playwright locators by role/text (no `data-testid`).
- **Delivery:** small conventional commits, no AI attribution in commits or PRs, "Mes Notes" in French.

## Open questions per slice

Ambiguities in the README to settle during each slice's brainstorm.

### Users

- Postal address shape: street + postal code + city, or richer?
- Is the monthly quota editable after creation? What does lowering it below the current month's count do?
- What happens to an inactive or soft-deleted user's existing reports and expenses (still visible? still editable?)
- Which management screens are expected (list, create, edit, activate/deactivate, delete)? The README
  lists user attributes but names no user page.

### Expense Reports

- One report per user per month (uniqueness)?
- The title is not editable: store a snapshot at creation, or compute it from the user's current name?
- French month capitalisation in the title ("Octobre 2025").
- Physical delete of a report that holds expenses: cascade, block, or detach?

### Expenses

- Must the expense date fall within its report's month?
- Is the quota counted per expense date month, and are soft-deleted expenses excluded from it?
- Does editing an expense (e.g. moving its date) re-check the quota?
- Amount precision and sign (`decimal(18,2)`, strictly positive?).
- Billing address shape ("enseigne / rue + code postal + ville").
- "Par lot de 5": numbered pages or "load more"?
