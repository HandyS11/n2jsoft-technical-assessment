import { HttpErrorResponse } from '@angular/common/http';
import { TestBed } from '@angular/core/testing';
import { provideRouter, Router } from '@angular/router';
import { RouterTestingHarness } from '@angular/router/testing';
import { Observable, of, throwError } from 'rxjs';
import { SaveUser, UserDetails } from './user-details';
import UserFormPage from './user-form.page';
import { UsersApi } from './users.api';

const jeanne: SaveUser = {
  firstName: 'Jeanne',
  lastName: 'Martin',
  address: { street: '5 rue Victor Hugo', postalCode: '33000', city: 'Bordeaux' },
  monthlyExpenseQuota: 8,
};
const juste: UserDetails = {
  id: '1',
  firstName: 'Juste',
  lastName: 'Leblanc',
  address: { street: '12 rue de Rivoli', postalCode: '75001', city: 'Paris' },
  monthlyExpenseQuota: 10,
  isActive: true,
  version: 3,
};

function httpError(status: number, error?: unknown): Observable<never> {
  return throwError(() => new HttpErrorResponse({ status, error }));
}

function fakeApi() {
  return {
    get: vi.fn((): Observable<UserDetails> => of(juste)),
    create: vi.fn(
      (): Observable<UserDetails> => of({ ...jeanne, id: '2', isActive: true, version: 1 })
    ),
    update: vi.fn((): Observable<void> => of(undefined)),
  };
}

async function render(api: ReturnType<typeof fakeApi>, url: string) {
  TestBed.configureTestingModule({
    providers: [
      provideRouter([
        { path: 'users', children: [] },
        { path: 'users/new', component: UserFormPage },
        { path: 'users/:id/edit', component: UserFormPage },
      ]),
      { provide: UsersApi, useValue: api },
    ],
  });
  const harness = await RouterTestingHarness.create();
  await harness.navigateByUrl(url, UserFormPage);
  harness.detectChanges();
  const element = () => harness.routeNativeElement as HTMLElement;
  const input = (label: string) => {
    const forId = Array.from(element().querySelectorAll('label')).find(
      (candidate) => candidate.textContent?.trim() === label
    )?.htmlFor;
    return element().querySelector<HTMLInputElement>(`#${forId}`) as HTMLInputElement;
  };

  return {
    element,
    input,
    fill: (label: string, value: string) => {
      const field = input(label);
      field.value = value;
      field.dispatchEvent(new Event('input'));
      harness.detectChanges();
    },
    submit: async () => {
      element().querySelector('form')?.dispatchEvent(new Event('submit'));
      harness.detectChanges();
      await harness.fixture.whenStable();
    },
    errors: () =>
      Array.from(element().querySelectorAll('.field-error')).map((error) =>
        error.textContent?.trim()
      ),
    alert: () => element().querySelector('[role="alert"]')?.textContent?.trim(),
  };
}

function fillJeanne(page: Awaited<ReturnType<typeof render>>): void {
  page.fill('Prénom', 'Jeanne');
  page.fill('Nom', 'Martin');
  page.fill('Rue', '5 rue Victor Hugo');
  page.fill('Code postal', '33000');
  page.fill('Ville', 'Bordeaux');
  page.fill('Quota mensuel de dépenses', '8');
}

describe('UserFormPage', () => {
  it('opens an empty creation form', async () => {
    const page = await render(fakeApi(), '/users/new');

    expect(page.element().querySelector('h1')?.textContent?.trim()).toBe(
      'Nouvel utilisateur'
    );
    expect(page.input('Prénom').value).toBe('');
    expect(page.input('Quota mensuel de dépenses').value).toBe('');
  });

  it('creates the user, then goes back to the list', async () => {
    const api = fakeApi();
    const page = await render(api, '/users/new');
    fillJeanne(page);

    await page.submit();

    expect(api.create).toHaveBeenCalledWith(jeanne);
    expect(TestBed.inject(Router).url).toBe('/users');
  });

  it('explains every missing field instead of saving', async () => {
    const api = fakeApi();
    const page = await render(api, '/users/new');

    await page.submit();

    expect(api.create).not.toHaveBeenCalled();
    expect(page.errors()).toEqual([
      'Ce champ est obligatoire.',
      'Ce champ est obligatoire.',
      'Ce champ est obligatoire.',
      'Ce champ est obligatoire.',
      'Ce champ est obligatoire.',
      'Ce champ est obligatoire.',
    ]);
  });

  it('treats a name made only of spaces as missing', async () => {
    const api = fakeApi();
    const page = await render(api, '/users/new');
    fillJeanne(page);
    page.fill('Prénom', '   ');

    await page.submit();

    expect(api.create).not.toHaveBeenCalled();
    expect(page.errors()).toEqual(['Ce champ est obligatoire.']);
  });

  it('requires a five-digit postal code', async () => {
    const page = await render(fakeApi(), '/users/new');
    fillJeanne(page);
    page.fill('Code postal', '3300');

    await page.submit();

    expect(page.errors()).toEqual(['Le code postal doit comporter 5 chiffres.']);
  });

  it('requires a whole monthly quota of at least 1', async () => {
    const page = await render(fakeApi(), '/users/new');
    fillJeanne(page);
    page.fill('Quota mensuel de dépenses', '0');

    await page.submit();

    expect(page.errors()).toEqual([
      'Le quota doit être un nombre entier supérieur ou égal à 1.',
    ]);
  });

  it('limits a name to 100 characters', async () => {
    const page = await render(fakeApi(), '/users/new');
    fillJeanne(page);
    page.fill('Nom', 'a'.repeat(101));

    await page.submit();

    expect(page.errors()).toEqual(['100 caractères au maximum.']);
  });

  it('says so when the API rejects the details', async () => {
    const api = fakeApi();
    api.create.mockImplementation(() => httpError(400));
    const page = await render(api, '/users/new');
    fillJeanne(page);

    await page.submit();

    expect(page.alert()).toBe('Les informations saisies sont invalides.');
    expect(TestBed.inject(Router).url).toBe('/users/new');
  });

  it('flags the fields the API rejects', async () => {
    const api = fakeApi();
    api.create.mockImplementation(() =>
      httpError(400, {
        errors: {
          'Address.Street': ['The field Street must be a string with a maximum length of 150.'],
          city: ['The value must not be blank.'],
        },
      })
    );
    const page = await render(api, '/users/new');
    fillJeanne(page);

    await page.submit();

    expect(page.errors()).toEqual([
      'Valeur refusée par le serveur.',
      'Valeur refusée par le serveur.',
    ]);
    page.fill('Rue', '6 rue Victor Hugo');
    expect(page.errors()).toEqual(['Valeur refusée par le serveur.']);
  });

  it('loads the user to edit into the form', async () => {
    const page = await render(fakeApi(), '/users/1/edit');

    expect(page.element().querySelector('h1')?.textContent?.trim()).toBe(
      "Modifier l'utilisateur"
    );
    expect(page.input('Prénom').value).toBe('Juste');
    expect(page.input('Ville').value).toBe('Paris');
    expect(page.input('Quota mensuel de dépenses').value).toBe('10');
  });

  it('updates the user, then goes back to the list', async () => {
    const api = fakeApi();
    const page = await render(api, '/users/1/edit');
    page.fill('Ville', 'Lyon');

    await page.submit();

    expect(api.update).toHaveBeenCalledWith(
      '1',
      {
        firstName: 'Juste',
        lastName: 'Leblanc',
        address: { street: '12 rue de Rivoli', postalCode: '75001', city: 'Lyon' },
        monthlyExpenseQuota: 10,
      },
      3
    );
    expect(TestBed.inject(Router).url).toBe('/users');
  });

  it('says so when the user to edit does not exist', async () => {
    const api = fakeApi();
    api.get.mockImplementation(() => httpError(404));
    const page = await render(api, '/users/1/edit');

    expect(page.alert()).toBe('Utilisateur introuvable.');
    expect(page.element().querySelector('form')).toBeNull();
  });

  it.each([409, 412])(
    'says so when the user was changed meanwhile (%i)',
    async (status) => {
      const api = fakeApi();
      api.update.mockImplementation(() => httpError(status));
      const page = await render(api, '/users/1/edit');

      await page.submit();

      expect(page.alert()).toBe(
        "L'utilisateur a été modifié entre-temps. Rechargez la page pour repartir de ses dernières informations."
      );
      expect(TestBed.inject(Router).url).toBe('/users/1/edit');
    }
  );

  it('says so when the user was deleted while being edited', async () => {
    const api = fakeApi();
    api.update.mockImplementation(() => httpError(404));
    const page = await render(api, '/users/1/edit');

    await page.submit();

    expect(page.alert()).toBe('Utilisateur introuvable.');
  });
});
