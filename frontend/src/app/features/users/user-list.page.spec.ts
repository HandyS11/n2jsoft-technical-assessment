import { TestBed } from '@angular/core/testing';
import { Observable, of } from 'rxjs';
import { UserSummary } from './user-summary';
import UserListPage from './user-list.page';
import { UsersApi } from './users.api';

function render(users: Observable<UserSummary[]>): HTMLElement {
  TestBed.configureTestingModule({
    imports: [UserListPage],
    providers: [{ provide: UsersApi, useValue: { list: () => users } }],
  });
  const fixture = TestBed.createComponent(UserListPage);
  fixture.detectChanges();
  return fixture.nativeElement as HTMLElement;
}

describe('UserListPage', () => {
  it('renders the name of every user returned by the API', () => {
    const page = render(
      of([
        { id: '1', name: 'Juste Leblanc' },
        { id: '2', name: 'Marc Assin' },
      ])
    );

    const names = Array.from(page.querySelectorAll('li')).map((item) =>
      item.textContent?.trim()
    );
    expect(names).toEqual(['Juste Leblanc', 'Marc Assin']);
  });

  it('says so when there is no user', () => {
    const page = render(of([]));

    expect(page.querySelector('ul')).toBeNull();
    expect(page.textContent).toContain('Aucun utilisateur.');
  });

  it('shows a loading message until the API answers', () => {
    const page = render(new Observable<UserSummary[]>());

    expect(page.textContent).toContain('Chargement…');
  });
});
