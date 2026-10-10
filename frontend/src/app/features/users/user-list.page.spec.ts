import { TestBed } from '@angular/core/testing';
import { Observable, of, throwError } from 'rxjs';
import { PagedResult } from '../../core/paged-result';
import { UserSummary } from './user-summary';
import UserListPage from './user-list.page';
import { UsersApi } from './users.api';

function render(users: Observable<PagedResult<UserSummary>>): HTMLElement {
  TestBed.configureTestingModule({
    imports: [UserListPage],
    providers: [{ provide: UsersApi, useValue: { list: () => users } }],
  });
  const fixture = TestBed.createComponent(UserListPage);
  fixture.detectChanges();
  return fixture.nativeElement as HTMLElement;
}

function firstPage(items: UserSummary[]): PagedResult<UserSummary> {
  return { items, page: 1, pageSize: 20, totalCount: items.length };
}

describe('UserListPage', () => {
  it('renders the name of every user returned by the API', () => {
    const page = render(
      of(
        firstPage([
          { id: '1', name: 'Juste Leblanc' },
          { id: '2', name: 'Marc Assin' },
        ])
      )
    );

    const names = Array.from(page.querySelectorAll('li')).map((item) =>
      item.textContent?.trim()
    );
    expect(names).toEqual(['Juste Leblanc', 'Marc Assin']);
  });

  it('says so when there is no user', () => {
    const page = render(of(firstPage([])));

    expect(page.querySelector('ul')).toBeNull();
    expect(page.textContent).toContain('Aucun utilisateur.');
  });

  it('shows a loading message until the API answers', () => {
    const page = render(new Observable<PagedResult<UserSummary>>());

    expect(page.textContent).toContain('Chargement…');
  });

  it('says so when the API fails', () => {
    const page = render(throwError(() => new Error('Service unavailable')));

    expect(page.querySelector('[role="alert"]')?.textContent?.trim()).toBe(
      'Impossible de charger les utilisateurs.'
    );
    expect(page.textContent).not.toContain('Chargement…');
  });
});
