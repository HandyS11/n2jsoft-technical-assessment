import { signal } from '@angular/core';
import { TestBed } from '@angular/core/testing';
import { PagedList } from './paged-list';
import { PagedResult, pageCountOf } from './paged-result';
import { PaginationComponent } from './pagination.component';

function pageOf(page: number, totalCount: number): PagedResult<unknown> {
  return { items: [], page, pageSize: 20, totalCount };
}

function fakeList(shown: PagedResult<unknown> | undefined, busy = false) {
  return {
    page: signal(shown),
    failed: signal(false),
    actionFailed: signal(false),
    busy: signal(busy),
    pageCount: signal(shown ? pageCountOf(shown) : 1),
    load: vi.fn(),
    run: vi.fn(),
  } satisfies PagedList<unknown>;
}

function render(list: PagedList<unknown>) {
  const fixture = TestBed.createComponent(PaginationComponent);
  fixture.componentRef.setInput('list', list);
  fixture.detectChanges();

  const element = fixture.nativeElement as HTMLElement;
  const button = (name: string) =>
    Array.from(element.querySelectorAll('button')).find((b) => b.textContent?.trim() === name) as HTMLButtonElement;
  return { element, button };
}

describe('PaginationComponent', () => {
  it('renders nothing before the first page shows', () => {
    const { element } = render(fakeList(undefined));

    expect(element.querySelector('nav')).toBeNull();
  });

  it('shows the page shown out of the page count', () => {
    const { element } = render(fakeList(pageOf(2, 50)));

    expect(element.querySelector('nav')?.getAttribute('aria-label')).toBe('Pagination');
    expect(element.textContent).toContain('Page 2 sur 3');
  });

  it('loads the previous and the next page', () => {
    const list = fakeList(pageOf(2, 50));
    const { button } = render(list);

    button('Précédent').click();
    button('Suivant').click();

    expect(list.load.mock.calls).toEqual([[1], [3]]);
  });

  it('disables Précédent on the first page', () => {
    const { button } = render(fakeList(pageOf(1, 50)));

    expect(button('Précédent').disabled).toBe(true);
    expect(button('Suivant').disabled).toBe(false);
  });

  it('disables Suivant on the last page', () => {
    const { button } = render(fakeList(pageOf(3, 50)));

    expect(button('Précédent').disabled).toBe(false);
    expect(button('Suivant').disabled).toBe(true);
  });

  it('disables both while an action runs', () => {
    const { button } = render(fakeList(pageOf(2, 50), true));

    expect(button('Précédent').disabled).toBe(true);
    expect(button('Suivant').disabled).toBe(true);
  });

  it('disables both on the only page of an empty list', () => {
    const { element, button } = render(fakeList(pageOf(1, 0)));

    expect(element.textContent).toContain('Page 1 sur 1');
    expect(button('Précédent').disabled).toBe(true);
    expect(button('Suivant').disabled).toBe(true);
  });
});
