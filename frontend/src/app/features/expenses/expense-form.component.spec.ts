import { HttpErrorResponse } from '@angular/common/http';
import { TestBed } from '@angular/core/testing';
import { Observable, of, throwError } from 'rxjs';
import { ExpenseDetails } from './expense';
import { ExpenseFormComponent } from './expense-form.component';
import { ExpensesApi } from './expenses.api';

const taxi: ExpenseDetails = {
  id: 'e1',
  date: '2025-10-15',
  description: 'Taxi',
  amount: 23.5,
  billingAddress: { storeName: 'G7', street: '12 rue de Rivoli', postalCode: '75001', city: 'Paris' },
  version: 7,
};

function httpError(status: number, body: unknown = null): Observable<never> {
  return throwError(() => new HttpErrorResponse({ status, error: body }));
}

function ruleViolation(code: string): Observable<never> {
  return httpError(422, { status: 422, code });
}

function fakeApi() {
  return {
    create: vi.fn((): Observable<ExpenseDetails> => of(taxi)),
    update: vi.fn((): Observable<void> => of(undefined)),
  };
}

function render(options: { api?: ReturnType<typeof fakeApi>; expense?: ExpenseDetails; year?: number; month?: number } = {}) {
  const api = options.api ?? fakeApi();
  TestBed.configureTestingModule({ providers: [{ provide: ExpensesApi, useValue: api }] });
  const fixture = TestBed.createComponent(ExpenseFormComponent);
  fixture.componentRef.setInput('reportId', 'r1');
  fixture.componentRef.setInput('year', options.year ?? 2025);
  fixture.componentRef.setInput('month', options.month ?? 10);
  fixture.componentRef.setInput('expense', options.expense);
  const saved = vi.fn();
  const cancelled = vi.fn();
  const reportChanged = vi.fn();
  fixture.componentInstance.saved.subscribe(saved);
  fixture.componentInstance.cancelled.subscribe(cancelled);
  fixture.componentInstance.reportChanged.subscribe(reportChanged);
  fixture.detectChanges();

  const element = fixture.nativeElement as HTMLElement;
  const input = (id: string) => element.querySelector(`#${id}`) as HTMLInputElement;
  const button = (name: string) =>
    Array.from(element.querySelectorAll('button')).find((b) => b.textContent?.trim() === name);
  const type = (id: string, value: string) => {
    input(id).value = value;
    input(id).dispatchEvent(new Event('input'));
    input(id).dispatchEvent(new Event('blur'));
    fixture.detectChanges();
  };
  const fillValid = () => {
    type('date', '2025-10-15');
    type('description', 'Taxi');
    type('amount', '23.5');
    type('street', '12 rue de Rivoli');
    type('postalCode', '75001');
    type('city', 'Paris');
  };
  const submit = () => {
    element.querySelector('form')?.dispatchEvent(new Event('submit'));
    fixture.detectChanges();
  };

  return {
    api,
    fixture,
    element,
    input,
    button,
    type,
    fillValid,
    submit,
    saved,
    cancelled,
    reportChanged,
    heading: () => element.querySelector('h2')?.textContent?.trim(),
    alert: () => element.querySelector('[role="alert"]')?.textContent?.trim(),
    fieldErrors: () => Array.from(element.querySelectorAll('.field-error')).map((e) => e.textContent?.trim()),
  };
}

describe('ExpenseFormComponent', () => {
  describe('adding', () => {
    it('offers an empty form to add an expense', () => {
      const form = render();

      expect(form.heading()).toBe('Nouvelle dépense');
      expect(form.button('Ajouter')).toBeDefined();
      expect(form.button('Annuler')).toBeUndefined();
      expect(form.input('description').value).toBe('');
    });

    it("bounds the date picker to the report's month", () => {
      const form = render({ year: 2024, month: 2 });

      expect(form.input('date').min).toBe('2024-02-01');
      expect(form.input('date').max).toBe('2024-02-29');
    });

    it('creates the expense, then empties the form', () => {
      const form = render();
      form.fillValid();
      form.type('storeName', '   ');

      form.submit();

      expect(form.api.create).toHaveBeenCalledWith('r1', {
        date: '2025-10-15',
        description: 'Taxi',
        amount: 23.5,
        billingAddress: { storeName: null, street: '12 rue de Rivoli', postalCode: '75001', city: 'Paris' },
      });
      expect(form.saved).toHaveBeenCalledWith('created');
      expect(form.input('description').value).toBe('');
    });

    it('sends the store name when one is given', () => {
      const form = render();
      form.fillValid();
      form.type('storeName', 'G7');

      form.submit();

      expect(form.api.create).toHaveBeenCalledWith(
        'r1',
        expect.objectContaining({ billingAddress: expect.objectContaining({ storeName: 'G7' }) })
      );
    });

    it('flags every missing field and sends nothing', () => {
      const form = render();

      form.submit();

      expect(form.fieldErrors()).toEqual(Array(6).fill('Ce champ est obligatoire.'));
      expect(form.api.create).not.toHaveBeenCalled();
    });

    it("refuses a date outside the report's month", () => {
      const form = render();
      form.fillValid();
      form.type('date', '2025-11-01');

      form.submit();

      expect(form.fieldErrors()).toEqual(['Choisissez une date du mois de la note de frais.']);
      expect(form.api.create).not.toHaveBeenCalled();
    });

    it('counts the characters of the description and refuses more than 50', () => {
      const form = render();
      form.fillValid();
      form.type('description', 'a'.repeat(51));

      form.submit();

      expect(form.element.textContent).toContain('51/50');
      expect(form.fieldErrors()).toEqual(['50 caractères au maximum.']);
      expect(form.api.create).not.toHaveBeenCalled();
    });

    it.each(['0', '-1', '0.001', '1000000'])('refuses the amount %s', (amount) => {
      const form = render();
      form.fillValid();
      form.type('amount', amount);

      form.submit();

      expect(form.fieldErrors()).toEqual([
        'Indiquez un montant entre 0,01 et 999 999,99 €, avec au plus deux décimales.',
      ]);
      expect(form.api.create).not.toHaveBeenCalled();
    });

    it('accepts the largest amount', () => {
      const form = render();
      form.fillValid();
      form.type('amount', '999999.99');

      form.submit();

      expect(form.api.create).toHaveBeenCalled();
    });

    it.each([
      [
        'expense.quota_exceeded',
        "Le quota mensuel de dépenses de cet utilisateur est atteint : impossible d'ajouter cette dépense.",
      ],
      ['expense.date_outside_report_month', 'La date doit être comprise dans le mois de la note de frais.'],
    ])('explains the refusal %s and keeps what was typed', (code, message) => {
      const api = fakeApi();
      api.create.mockImplementation(() => ruleViolation(code));
      const form = render({ api });
      form.fillValid();

      form.submit();

      expect(form.alert()).toBe(message);
      expect(form.saved).not.toHaveBeenCalled();
      expect(form.input('description').value).toBe('Taxi');
      expect(form.button('Ajouter')?.disabled).toBe(false);
    });

    it("asks the page to read the report again when the report's user was deleted", () => {
      const api = fakeApi();
      api.create.mockImplementation(() => ruleViolation('expense_report.deleted_user'));
      const form = render({ api });
      form.fillValid();

      form.submit();

      expect(form.alert()).toBe("L'utilisateur de cette note de frais a été supprimé : elle est en lecture seule.");
      expect(form.reportChanged).toHaveBeenCalled();
    });

    it('says so when the report no longer exists', () => {
      const api = fakeApi();
      api.create.mockImplementation(() => httpError(404));
      const form = render({ api });
      form.fillValid();

      form.submit();

      expect(form.alert()).toBe('Note de frais introuvable.');
    });

    it('says so when the server fails', () => {
      const api = fakeApi();
      api.create.mockImplementation(() => httpError(500));
      const form = render({ api });
      form.fillValid();

      form.submit();

      expect(form.alert()).toBe("L'opération a échoué. Veuillez réessayer.");
    });
  });

  describe('editing', () => {
    it('shows the expense to edit', () => {
      const form = render({ expense: taxi });

      expect(form.heading()).toBe('Modifier la dépense');
      expect(form.input('date').value).toBe('2025-10-15');
      expect(form.input('description').value).toBe('Taxi');
      expect(form.input('amount').value).toBe('23.5');
      expect(form.input('storeName').value).toBe('G7');
      expect(form.input('city').value).toBe('Paris');
      expect(form.button('Enregistrer')).toBeDefined();
    });

    it('updates the expense with the version it was read at', () => {
      const form = render({ expense: taxi });
      form.type('amount', '30');

      form.submit();

      expect(form.api.update).toHaveBeenCalledWith(
        'r1',
        'e1',
        expect.objectContaining({ amount: 30, billingAddress: expect.objectContaining({ storeName: 'G7' }) }),
        7
      );
      expect(form.saved).toHaveBeenCalledWith('updated');
    });

    it('can be cancelled', () => {
      const form = render({ expense: taxi });

      form.button('Annuler')?.click();

      expect(form.cancelled).toHaveBeenCalled();
    });

    it('empties the form when the page leaves edit mode', () => {
      const form = render({ expense: taxi });

      form.fixture.componentRef.setInput('expense', undefined);
      form.fixture.detectChanges();

      expect(form.heading()).toBe('Nouvelle dépense');
      expect(form.input('description').value).toBe('');
    });

    it.each([409, 412])('says so when the expense changed meanwhile (%s)', (status) => {
      const api = fakeApi();
      api.update.mockImplementation(() => httpError(status));
      const form = render({ api, expense: taxi });

      form.submit();

      expect(form.alert()).toBe("Cette dépense a été modifiée entre-temps. Rechargez-la avant de l'enregistrer.");
    });

    it('says so when the expense no longer exists', () => {
      const api = fakeApi();
      api.update.mockImplementation(() => httpError(404));
      const form = render({ api, expense: taxi });

      form.submit();

      expect(form.alert()).toBe('Dépense introuvable.');
    });
  });
});
