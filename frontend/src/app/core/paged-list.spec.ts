import { TestBed } from '@angular/core/testing';
import { Observable, of, Subject, throwError } from 'rxjs';
import { pagedList } from './paged-list';
import { PagedResult } from './paged-result';

function pageOf(items: string[], page = 1, totalCount = items.length): PagedResult<string> {
  return { items, page, pageSize: 20, totalCount };
}

function create(fetch: (page: number) => Observable<PagedResult<string>>) {
  const fetchSpy = vi.fn(fetch);
  const list = TestBed.runInInjectionContext(() => pagedList(fetchSpy));
  return { fetch: fetchSpy, list };
}

describe('pagedList', () => {
  it('loads the first page at once', () => {
    const { fetch, list } = create((page) => of(pageOf(['a'], page, 45)));

    expect(fetch).toHaveBeenCalledWith(1);
    expect(list.page()).toEqual(pageOf(['a'], 1, 45));
    expect(list.pageCount()).toBe(3);
  });

  it('counts one page until the first one shows', () => {
    const { list } = create(() => new Subject<PagedResult<string>>());

    expect(list.page()).toBeUndefined();
    expect(list.pageCount()).toBe(1);
  });

  it('shows the page asked for', () => {
    const { list } = create((page) => of(pageOf([`row ${page}`], page, 45)));

    list.load(2);

    expect(list.page()).toEqual(pageOf(['row 2'], 2, 45));
  });

  it('says so when a page cannot be loaded, until one loads', () => {
    let fails = true;
    const { list } = create((page) =>
      fails ? throwError(() => new Error('Service unavailable')) : of(pageOf(['a'], page)),
    );
    expect(list.failed()).toBe(true);

    fails = false;
    list.load(1);

    expect(list.failed()).toBe(false);
  });

  it('ignores an answer arriving after a later request', () => {
    const pageTwo = new Subject<PagedResult<string>>();
    const { list } = create((page) => (page === 1 ? of(pageOf(['a'], 1, 45)) : pageTwo));
    list.load(2);
    list.load(1);

    pageTwo.next(pageOf(['b'], 2, 45));

    expect(list.page()).toEqual(pageOf(['a'], 1, 45));
  });

  it('jumps straight to the last page left when rows were deleted past it', () => {
    const { fetch, list } = create((page) => of(page === 1 ? pageOf(['a'], 1, 20) : pageOf([], page, 20)));

    list.load(5);

    expect(fetch.mock.calls.map(([page]) => page)).toEqual([1, 5, 1]);
    expect(list.page()).toEqual(pageOf(['a'], 1, 20));
  });

  it('is busy while an action runs, until the page reloaded after it shows', () => {
    const action = new Subject<void>();
    const { fetch, list } = create((page) => of(pageOf(['a'], page, 45)));
    list.load(2);

    list.run(action);
    expect(list.busy()).toBe(true);
    action.next();

    expect(fetch).toHaveBeenLastCalledWith(2);
    expect(list.busy()).toBe(false);
    expect(list.actionFailed()).toBe(false);
  });

  it('says so when an action fails, and reloads the page', () => {
    const { fetch, list } = create((page) => of(pageOf(['a'], page)));

    list.run(throwError(() => new Error('Not found')));

    expect(list.actionFailed()).toBe(true);
    expect(fetch).toHaveBeenCalledTimes(2);
    expect(list.busy()).toBe(false);
  });

  it('forgets a failed action once another one runs', () => {
    const { list } = create((page) => of(pageOf(['a'], page)));
    list.run(throwError(() => new Error('Not found')));

    list.run(new Subject<void>());

    expect(list.actionFailed()).toBe(false);
  });

  it('is no longer busy when the page reloaded after an action cannot be loaded', () => {
    let fails = false;
    const { list } = create((page) =>
      fails ? throwError(() => new Error('Service unavailable')) : of(pageOf(['a'], page)),
    );
    fails = true;

    list.run(of(undefined));

    expect(list.failed()).toBe(true);
    expect(list.busy()).toBe(false);
  });

  it('stops loading once its injector is destroyed', () => {
    const { fetch, list } = create((page) => of(pageOf(['a'], page)));

    TestBed.resetTestingModule();
    list.load(2);

    expect(fetch).toHaveBeenCalledTimes(1);
  });
});
