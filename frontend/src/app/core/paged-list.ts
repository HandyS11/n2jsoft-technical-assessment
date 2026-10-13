import { computed, Signal, signal } from '@angular/core';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { catchError, EMPTY, Observable, Subject, switchMap } from 'rxjs';
import { PagedResult, pageCountOf } from './paged-result';

export interface PagedList<T> {
  /** Undefined until the first page shows. */
  readonly page: Signal<PagedResult<T> | undefined>;
  readonly failed: Signal<boolean>;
  readonly actionFailed: Signal<boolean>;
  /** An action runs, until the page reloaded after it shows: a second click would act on a stale row. */
  readonly busy: Signal<boolean>;
  readonly pageCount: Signal<number>;
  load(page: number): void;
  /** Runs an action on a row, then reloads the page shown, whether it succeeded or not. */
  run(action: Observable<unknown>): void;
}

/**
 * The page of rows a list shows, starting with the first one. Call it in an injection context (e.g. a field
 * initializer): its requests stop when the component is destroyed.
 */
export function pagedList<T>(fetch: (page: number) => Observable<PagedResult<T>>): PagedList<T> {
  const page = signal<PagedResult<T> | undefined>(undefined);
  const failed = signal(false);
  const actionFailed = signal(false);
  const busy = signal(false);
  const pageRequests = new Subject<number>();

  const load = (pageNumber: number): void => pageRequests.next(pageNumber);
  const reload = (): void => load(page()?.page ?? 1);

  const show = (shown: PagedResult<T>): void => {
    // Rows were deleted past the page shown: show the last page left rather than an empty one. Always an earlier
    // page, so this ends even if more rows go meanwhile.
    const lastPage = pageCountOf(shown);
    if (shown.items.length === 0 && shown.page > lastPage) {
      load(lastPage);
      return;
    }
    page.set(shown);
    failed.set(false);
    busy.set(false);
  };

  pageRequests
    .pipe(
      // Only the latest request counts: an answer arriving late must not replace the page asked for since.
      switchMap((pageNumber) =>
        fetch(pageNumber).pipe(
          catchError(() => {
            failed.set(true);
            busy.set(false);
            return EMPTY;
          }),
        ),
      ),
      takeUntilDestroyed(),
    )
    .subscribe(show);
  load(1);

  return {
    page: page.asReadonly(),
    failed: failed.asReadonly(),
    actionFailed: actionFailed.asReadonly(),
    busy: busy.asReadonly(),
    pageCount: computed(() => {
      const shown = page();
      return shown ? pageCountOf(shown) : 1;
    }),
    load,
    run: (action) => {
      busy.set(true);
      actionFailed.set(false);
      action.subscribe({
        next: reload,
        error: () => {
          // The row may be gone (deleted in another tab): reload so a stale row does not stay.
          actionFailed.set(true);
          reload();
        },
      });
    },
  };
}
