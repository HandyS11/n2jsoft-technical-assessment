# Wrap-up — design

Slice 5 of the [roadmap](../roadmap.md). Status: approved design; the implementation plan follows.
Builds on the [Foundation](2026-10-10-foundation-design.md), [Users](2026-10-11-users-design.md),
[Expense Reports](2026-10-11-expense-reports-design.md) and [Expenses](2026-10-11-expenses-design.md) designs, whose
conventions apply unless stated otherwise here.

## Context

After slice 4, every functional requirement of the README is implemented, tested across the four tiers and merged
into `develop`. The GitHub repository is already private and `n2jsoft-hr-cr` is already a collaborator: the delivery
step of the roadmap's slice 5 is done.

What is left is what an evaluator meets first: they run `dotnet run --project tools/N2JSoft.Aspire` from a fresh
clone, open the Aspire dashboard, then read "Mes Notes". Today:

- nothing in `webapi` exports telemetry: the dashboard's *Traces*, *Metrics* and *Structured logs* tabs are empty for
  it, although the AppHost already passes `OTEL_EXPORTER_OTLP_ENDPOINT` and the Aspire Npgsql integration already
  emits spans;
- the first run from a clean clone has never been tried as an evaluator would;
- "Mes Notes" is four slice reports one after the other, with no overview and no mapping from the subject's
  requirements to the code and tests;
- two items of « Pistes d'amélioration » are cheap enough to do now: the pagination bar repeated on three lists,
  and the redundant `prestart: npm install` script.

## Goals

- Export the `webapi` logs, traces and metrics over OTLP, so the Aspire dashboard shows them.
- Extract the pagination bar into one shared component.
- Remove `prestart: npm install`; clean up the merged local branches and the expenses spike worktree.
- Run the application from a fresh clone as an evaluator would, and fix what breaks.
- Finalise "Mes Notes": an overview, a slice 5 section, a requirements traceability table, updated « Pistes ».

## Non-goals

- Authentication and authorisation. The subject describes a single administrator and no login; doing it properly
  means an OIDC provider, not self-issued JWTs. Stays in « Pistes ».
- Internationalisation. The subject, its users and the UI are French; the API's stable business-rule codes, mapped
  to French by the front, are already the seam a translation would use. Goes to « Pistes ».
- Browser telemetry from the Angular app, and business metrics (e.g. a counter of `expense.quota_exceeded`). Go to
  « Pistes ».
- A `ServiceDefaults` project, resilience and service discovery: there is one .NET service and it makes no outgoing
  HTTP calls.
- Zoneless change detection, a generated OpenAPI client, an Aspire health check on `webapi`: stay in « Pistes ».

## Decisions

| Question                                  | Decision                                                                                                                        | Why                                                                                                                              |
|-------------------------------------------|---------------------------------------------------------------------------------------------------------------------------------|----------------------------------------------------------------------------------------------------------------------------------|
| Telemetry scope                           | Backend only: logs, traces, metrics of `webapi`                                                                                 | Fills the dashboard the README points to; browser telemetry needs OTel JS packages, dashboard CORS and front configuration      |
| Where the setup lives                     | `WebApi/Telemetry/TelemetryExtensions.cs`, `AddTelemetry()` on `IHostApplicationBuilder`, called first in `Program.cs`        | `WebApi` is the composition root; a `ServiceDefaults` project only pays off with several services. Easy to extract later        |
| When the OTLP exporter is on              | Only when `OTEL_EXPORTER_OTLP_ENDPOINT` is set                                                                                  | Aspire sets it; the acceptance tier, CI and a bare `dotnet run` never try to export                                             |
| Service name                              | From `OTEL_SERVICE_NAME`, set by Aspire; no resource code                                                                       | The OTel SDK reads it already                                                                                                    |
| Traced requests                           | All but `/health`                                                                                                               | Probes would drown the requests that matter                                                                                     |
| Database spans                            | From the Aspire Npgsql integration (Npgsql's `ActivitySource`); no EF Core instrumentation                                       | The EF Core instrumentation is only published as a beta; Npgsql spans already carry the SQL                                      |
| HttpClient instrumentation                | None                                                                                                                            | `webapi` makes no outgoing HTTP calls                                                                                             |
| Where telemetry may be referenced         | `WebApi` only: Domain and Application must not reference `OpenTelemetry` (architecture tests)                                   | Telemetry is wiring, like the rest of the composition root                                                                       |
| Pagination component                      | `core/pagination.component.ts`, `<app-pagination [list]="…" />`, input `list: PagedList<unknown>`, markup unchanged             | The three bars are identical but for the list's name; `core/` already holds `PagedList`; unchanged markup keeps the page specs and E2E locators valid |
| `prestart: npm install`                   | Removed                                                                                                                         | `AddJavaScriptApp` already installs the dependencies before starting the front (checked by the fresh-clone run)                 |
| Local branches                            | `feat/users`, `feat/expense-reports`, `feat/expenses` deleted once their PR is confirmed merged; the `spike/expenses` worktree (`../n2j-spike-expenses`) and branch removed | Squash merges leave them behind; the spike was throwaway by design                                                              |
| Fresh-clone run                           | Last step, on the finished branch, in an empty temporary folder                                                                 | It checks what the evaluator gets, `prestart` removal and telemetry included                                                     |
| Traceability                              | A table in "Mes Notes", in French: *Exigence → Implémentation → Test*, one row per requirement of the subject                    | Evaluators read the notes against the subject; the table shows nothing was missed and where each rule is proven                 |

## Telemetry

`TelemetryExtensions.AddTelemetry(this IHostApplicationBuilder builder)`:

- **Logs:** `builder.Logging.AddOpenTelemetry(options => { options.IncludeFormattedMessage = true;
  options.IncludeScopes = true; })`, so each structured log in the dashboard carries its message and links to its
  trace.
- **Traces:** `AddAspNetCoreInstrumentation`, with a `Filter` that drops requests whose path starts with the
  `/health` segment. Npgsql spans come from the Aspire integration registered by `AddInfrastructure`.
- **Metrics:** `AddAspNetCoreInstrumentation` and `AddRuntimeInstrumentation`; plus the `Npgsql` meter if the Aspire
  integration does not already add it (see *Risks*).
- **Export:** `builder.Services.AddOpenTelemetry().UseOtlpExporter()` only when
  `builder.Configuration["OTEL_EXPORTER_OTLP_ENDPOINT"]` is not blank.

Packages, in a new *OpenTelemetry* group of `Directory.Packages.props`, latest stable versions at implementation
time: `OpenTelemetry.Extensions.Hosting`, `OpenTelemetry.Exporter.OpenTelemetryProtocol`,
`OpenTelemetry.Instrumentation.AspNetCore`, `OpenTelemetry.Instrumentation.Runtime` (WebApi);
`OpenTelemetry.Exporter.InMemory` (AcceptanceTests).

## Frontend

`PaginationComponent` (standalone, selector `app-pagination`) renders the current bar:

```html
<nav aria-label="Pagination" class="mt-4 flex items-center gap-4">
  <button type="button" [disabled]="list().busy() || page.page <= 1" (click)="list().load(page.page - 1)">Précédent</button>
  <span>Page {{ page.page }} sur {{ list().pageCount() }}</span>
  <button type="button" [disabled]="list().busy() || page.page >= list().pageCount()" (click)="list().load(page.page + 1)">Suivant</button>
</nav>
```

where `page` is `list().page()`, and nothing renders while it is undefined. The three pages
(`user-list.page.ts`, `expense-report-list.page.ts`, `expense-report.page.ts`) replace their bar with
`<app-pagination [list]="list" />` (`[list]="expenses"` on the report page), inside the same `@if` as before.

## Test tiers

- **Unit:** `DependencyRuleTests` gains `OpenTelemetry` in the forbidden lists of Domain and Application.
- **Acceptance:** `Telemetry.feature`, against a `TestEnvironment.TelemetryFactory` built on first use like
  `DevelopmentFactory`, adding an in-memory trace exporter that collects into a list the steps read (cleared before
  each scenario):
  - *A request is traced down to the database:* `When I request GET /v1/users` → a server span for the route
    `/v1/users` with status 200, and an Npgsql span with the same trace id.
  - *Health probes are not traced:* `When I request GET /health` → no server span for `/health`.
  - The server span ends after the response is written: the *Then* steps wait for it (short bounded polling) rather
    than assert at once.
- **End-to-end:** no new journey; the existing ones keep passing with the shared pagination.
- **Frontend:** `pagination.component.spec.ts` (Vitest): the label « Page X sur Y »; *Précédent* / *Suivant* call
  `load(page ∓ 1)`; disabled on the first page, on the last page and while busy; nothing rendered before the first
  page. The existing page specs pass unchanged.
- **Manual (fresh-clone run):**
  1. `git clone` the branch into an empty temporary folder; no `node_modules`, `bin` or `obj`.
  2. `dotnet run --project tools/N2JSoft.Aspire`.
  3. Every resource reaches *Running*; the front serves the reports page with the dev seed (Juste Leblanc, Marc
     Assin); a report can be created and an expense added.
  4. The dashboard shows `webapi` traces (an HTTP request with its Npgsql span), metrics and structured logs.
  5. Driven with Playwright, with screenshots as evidence. Each problem found gets its own fix commit; a hidden
     prerequisite (e.g. the ASP.NET Core development certificate) is documented under « Prérequis techniques ».

## Mes Notes

In French, in `README.md`:

- **Overview** at the top of "Mes Notes": what was delivered and how the notes are organised (slices, roadmap, specs,
  plans).
- **« Tranche 5 — Finitions »:** observability (what the dashboard shows, OTLP only under Aspire), shared
  pagination, `prestart` removal, fresh-clone check.
- **« Couverture du sujet »:** the traceability table, one row per requirement:

  | Area          | Requirements                                                                                                                                                                                                                           |
  |---------------|----------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------|
  | Users         | assignable to a report; one assigned user per report, several reports per user; monthly expense quota; active or inactive, only active users assignable; last name, first name, postal address; logical deletion                    |
  | Expenses      | date shown as "mercredi 15 octobre 2025"; description of at most 50 characters; amount in euros; billing address (store / street + postal code + city); editable at any time; quota reached → error message, creation refused; logical deletion |
  | Reports       | a user's expenses for one month; title "Utilisateur - Mois Année"; title not editable; physical deletion; list page adds a report by month and user; report page adds expenses and shows them by 5                                   |

  Each row names the implementation (entity, endpoint, page) and the test proving it (Gherkin scenario, unit test,
  Vitest spec or E2E journey).
- **« Pistes d'amélioration »:** remove the pagination and `prestart` items; add browser telemetry, business
  metrics and i18n (with the business-rule codes seam); expand authentication to name OIDC rather than self-issued
  JWTs.

## Risks to verify first

1. The Aspire Npgsql EF Core integration registers Npgsql's tracing (and metrics) with the OpenTelemetry SDK that
   `AddTelemetry` sets up, so database spans appear without extra code. Fallback: add Npgsql's source and meter in
   `AddTelemetry`.
2. Under `WebApplicationFactory`, an in-memory exporter added in `ConfigureTestServices` receives the server spans
   of the in-process requests. Fallback: an `ActivityListener` on `Microsoft.AspNetCore` and `Npgsql` in the test.
3. `AddJavaScriptApp` installs the frontend's npm dependencies before `npm start`, so removing `prestart` breaks
   nothing on a fresh clone. Fallback: keep `prestart` and drop the « Pistes » item.

## Delivery

- Branch `feat/wrap-up` from `develop`, small conventional commits, PR into `develop`.
- Order: telemetry (TDD) → pagination (TDD) → chores → docs → fresh-clone run → PR.
- Roadmap: Wrap-up marked *Done*, its scope updated, with links to this spec and its plan.

## Definition of done

- Unit, Acceptance, E2E and frontend suites green locally and in CI; CI also green on `develop` before the PR.
- From a fresh clone, `dotnet run --project tools/N2JSoft.Aspire` starts everything and the dashboard shows `webapi`
  traces, metrics and structured logs.
- The three lists page through the shared component.
- "Mes Notes" has its overview, the slice 5 section, the traceability table and the updated « Pistes »; roadmap
  updated; PR `feat/wrap-up` → `develop` opened.
