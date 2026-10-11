import { TestBed } from '@angular/core/testing';
import { provideRouter } from '@angular/router';
import { Observable, of, Subject, throwError } from 'rxjs';
import { PagedResult } from '../../core/paged-result';
import UserListPage from './user-list.page';
import { UserSummary } from './user-summary';
import { UsersApi } from './users.api';

const juste: UserSummary = {
  id: '1',
  name: 'Juste Leblanc',
  city: 'Paris',
  monthlyExpenseQuota: 10,
  isActive: true,
};
const marc: UserSummary = {
  id: '2',
  name: 'Marc Assin',
  city: 'Lyon',
  monthlyExpenseQuota: 5,
  isActive: false,
};

function pageOf(
  items: UserSummary[],
  page = 1,
  totalCount = items.length
): PagedResult<UserSummary> {
  return { items, page, pageSize: 20, totalCount };
}

function fakeApi(
  list: (page: number) => Observable<PagedResult<UserSummary>>
) {
  return {
    list: vi.fn(list),
    activate: vi.fn(() => of(undefined)),
    deactivate: vi.fn(() => of(undefined)),
    delete: vi.fn(() => of(undefined)),
  };
}

function render(api: ReturnType<typeof fakeApi>) {
  TestBed.configureTestingModule({
    imports: [UserListPage],
    providers: [provideRouter([]), { provide: UsersApi, useValue: api }],
  });
  const fixture = TestBed.createComponent(UserListPage);
  fixture.detectChanges();
  const element = fixture.nativeElement as HTMLElement;
  const button = (label: string, rowIndex?: number) => {
    const scope =
      rowIndex === undefined
        ? element
        : element.querySelectorAll('tbody tr')[rowIndex];
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
    detectChanges: () => fixture.detectChanges(),
    rows: () =>
      Array.from(element.querySelectorAll('tbody tr')).map((row) =>
        Array.from(row.querySelectorAll('td'))
          .slice(0, 4)
          .map((cell) => cell.textContent?.trim())
      ),
    click: (label: string, rowIndex?: number) => {
      button(label, rowIndex).click();
      fixture.detectChanges();
    },
  };
}

describe('UserListPage', () => {
  afterEach(() => vi.restoreAllMocks());

  it("shows each user's name, city, monthly quota and status", () => {
    const page = render(fakeApi(() => of(pageOf([juste, marc]))));

    expect(page.rows()).toEqual([
      ['Juste Leblanc', 'Paris', '10', 'Actif'],
      ['Marc Assin', 'Lyon', '5', 'Inactif'],
    ]);
  });

  it('asks the API for the first page of 20 users', () => {
    const api = fakeApi(() => of(pageOf([juste])));

    render(api);

    expect(api.list).toHaveBeenCalledWith(1, 20);
  });

  it('says so when there is no user', () => {
    const page = render(fakeApi(() => of(pageOf([]))));

    expect(page.element.querySelector('table')).toBeNull();
    expect(page.element.textContent).toContain('Aucun utilisateur.');
  });

  it('shows a loading message until the API answers', () => {
    const page = render(
      fakeApi(() => new Observable<PagedResult<UserSummary>>())
    );

    expect(page.element.textContent).toContain('Chargement…');
  });

  it('says so when the API fails', () => {
    const page = render(
      fakeApi(() => throwError(() => new Error('Service unavailable')))
    );

    expect(
      page.element.querySelector('[role="alert"]')?.textContent?.trim()
    ).toBe('Impossible de charger les utilisateurs.');
    expect(page.element.textContent).not.toContain('Chargement…');
  });

  it('pages through the users', () => {
    const api = fakeApi((page) =>
      of(pageOf(page === 1 ? [juste] : [marc], page, 45))
    );
    const page = render(api);
    expect(page.element.textContent).toContain('Page 1 sur 3');

    page.click('Suivant');

    expect(api.list).toHaveBeenLastCalledWith(2, 20);
    expect(page.rows()).toEqual([['Marc Assin', 'Lyon', '5', 'Inactif']]);
    expect(page.element.textContent).toContain('Page 2 sur 3');
  });

  it('cannot go before the first page', () => {
    const page = render(fakeApi(() => of(pageOf([juste], 1, 45))));

    const previous = Array.from(page.element.querySelectorAll('button')).find(
      (button) => button.textContent?.trim() === 'Précédent'
    );
    expect(previous?.disabled).toBe(true);
  });

  it('deactivates an active user, then reloads the page', () => {
    const api = fakeApi(() => of(pageOf([juste])));
    const page = render(api);

    page.click('Désactiver', 0);

    expect(api.deactivate).toHaveBeenCalledWith('1');
    expect(api.list).toHaveBeenCalledTimes(2);
  });

  it('activates an inactive user, then reloads the page', () => {
    const api = fakeApi(() => of(pageOf([marc])));
    const page = render(api);

    page.click('Activer', 0);

    expect(api.activate).toHaveBeenCalledWith('2');
    expect(api.list).toHaveBeenCalledTimes(2);
  });

  it('deletes a user once the deletion is confirmed', () => {
    const confirm = vi.spyOn(window, 'confirm').mockReturnValue(true);
    const api = fakeApi(() => of(pageOf([juste])));
    const page = render(api);

    page.click('Supprimer', 0);

    expect(confirm).toHaveBeenCalledWith(
      "Supprimer l'utilisateur Juste Leblanc ?"
    );
    expect(api.delete).toHaveBeenCalledWith('1');
    expect(api.list).toHaveBeenCalledTimes(2);
  });

  it('keeps the user when the deletion is cancelled', () => {
    vi.spyOn(window, 'confirm').mockReturnValue(false);
    const api = fakeApi(() => of(pageOf([juste])));
    const page = render(api);

    page.click('Supprimer', 0);

    expect(api.delete).not.toHaveBeenCalled();
  });

  it('goes back one page when the last user of the last page is deleted', () => {
    vi.spyOn(window, 'confirm').mockReturnValue(true);
    let deleted = false;
    const api = fakeApi((page) => {
      if (page === 1) {
        return of(pageOf([marc], 1, deleted ? 20 : 21));
      }
      return of(deleted ? pageOf([], 2, 20) : pageOf([juste], 2, 21));
    });
    api.delete.mockImplementation(() => {
      deleted = true;
      return of(undefined);
    });
    const page = render(api);
    page.click('Suivant');

    page.click('Supprimer', 0);

    expect(page.rows()).toEqual([['Marc Assin', 'Lyon', '5', 'Inactif']]);
    expect(page.element.textContent).toContain('Page 1 sur 1');
  });

  it('jumps straight to the last page left when users were deleted past it', () => {
    vi.spyOn(window, 'confirm').mockReturnValue(true);
    let deleted = false;
    const api = fakeApi((page) => {
      if (deleted) {
        return of(page === 1 ? pageOf([marc], 1, 20) : pageOf([], page, 20));
      }
      return of(pageOf([juste], page, 100));
    });
    api.delete.mockImplementation(() => {
      deleted = true;
      return of(undefined);
    });
    const page = render(api);
    for (let i = 0; i < 4; i++) {
      page.click('Suivant');
    }

    page.click('Supprimer', 0);

    expect(api.list.mock.calls.map(([pageNumber]) => pageNumber)).toEqual([
      1, 2, 3, 4, 5, 5, 1,
    ]);
    expect(page.element.textContent).toContain('Page 1 sur 1');
  });

  it('ignores an answer arriving after a later request', () => {
    const pageTwo = new Subject<PagedResult<UserSummary>>();
    const api = fakeApi((page) =>
      page === 1 ? of(pageOf([juste], 1, 45)) : pageTwo
    );
    const page = render(api);
    page.click('Suivant');
    // Page 2 is still loading when the action reloads page 1, which answers at once.
    page.click('Désactiver', 0);

    pageTwo.next(pageOf([marc], 2, 45));
    page.detectChanges();

    expect(page.rows()).toEqual([['Juste Leblanc', 'Paris', '10', 'Actif']]);
    expect(page.element.textContent).toContain('Page 1 sur 3');
  });

  it('disables the actions until the page reloaded after one shows', () => {
    const action = new Subject<void>();
    const api = fakeApi(() => of(pageOf([juste], 1, 45)));
    api.deactivate.mockImplementation(() => action);
    const page = render(api);

    page.click('Désactiver', 0);

    expect(page.button('Désactiver', 0).disabled).toBe(true);
    expect(page.button('Supprimer', 0).disabled).toBe(true);
    expect(page.button('Suivant').disabled).toBe(true);

    action.next();
    page.detectChanges();

    expect(page.button('Désactiver', 0).disabled).toBe(false);
    expect(page.button('Supprimer', 0).disabled).toBe(false);
    expect(page.button('Suivant').disabled).toBe(false);
  });

  it('says so when an action fails', () => {
    const api = fakeApi(() => of(pageOf([juste])));
    api.deactivate.mockImplementation(() =>
      throwError(() => new Error('Service unavailable'))
    );
    const page = render(api);

    page.click('Désactiver', 0);

    expect(
      page.element.querySelector('[role="alert"]')?.textContent?.trim()
    ).toBe("L'opération a échoué. Veuillez réessayer.");
  });

  it('reloads the current page when an action fails, keeping the alert', () => {
    const api = fakeApi(() => of(pageOf([juste])));
    api.deactivate.mockImplementation(() =>
      throwError(() => new Error('Not found'))
    );
    const page = render(api);

    page.click('Désactiver', 0);

    expect(api.list).toHaveBeenCalledTimes(2);
    expect(api.list).toHaveBeenLastCalledWith(1, 20);
    expect(
      page.element.querySelector('[role="alert"]')?.textContent?.trim()
    ).toBe("L'opération a échoué. Veuillez réessayer.");
  });
});
