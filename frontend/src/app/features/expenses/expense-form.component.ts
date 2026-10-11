import { HttpErrorResponse } from '@angular/common/http';
import { Component, computed, effect, inject, input, output, signal, untracked } from '@angular/core';
import {
  AbstractControl,
  NonNullableFormBuilder,
  ReactiveFormsModule,
  ValidationErrors,
  ValidatorFn,
  Validators,
} from '@angular/forms';
import { Observable } from 'rxjs';
import { businessRuleCode } from '../../core/problem-details';
import { ExpenseDetails, SaveExpense } from './expense';
import { ExpensesApi } from './expenses.api';

/**
 * The API's limits (Expense.DescriptionMaxLength, MinAmount, MaxAmount, BillingAddress.StoreNameMaxLength and the
 * address rules), checked here first for immediate feedback. The API stays the authority.
 */
const descriptionMaxLength = 50;
const minAmount = 0.01;
const maxAmount = 999_999.99;
const storeNameMaxLength = 100;
const streetMaxLength = 200;
const cityMaxLength = 100;
const postalCodePattern = /^[0-9]{5}$/;
/** A positive number with at most two decimals, as the API stores it. */
const amountPattern = /^[0-9]+(\.[0-9]{1,2})?$/;

const ruleMessages = new Map<string, string>([
  [
    'expense.quota_exceeded',
    "Le quota mensuel de dépenses de cet utilisateur est atteint : impossible d'ajouter cette dépense.",
  ],
  ['expense.date_outside_report_month', 'La date doit être comprise dans le mois de la note de frais.'],
  [
    'expense_report.deleted_user',
    "L'utilisateur de cette note de frais a été supprimé : elle est en lecture seule.",
  ],
]);

/** Like Validators.required, but text made only of spaces is missing too: the API would trim it to nothing. */
function requiredText(control: AbstractControl<string>): ValidationErrors | null {
  return control.value.trim() === '' ? { required: true } : null;
}

function twoDecimals(control: AbstractControl<number | null>): ValidationErrors | null {
  return control.value === null || amountPattern.test(String(control.value)) ? null : { decimals: true };
}

function hasStatus(error: unknown, ...statuses: number[]): boolean {
  return error instanceof HttpErrorResponse && statuses.includes(error.status);
}

type Field = 'date' | 'description' | 'amount' | 'storeName' | 'street' | 'postalCode' | 'city';

/**
 * Adds an expense to a report or, given one to edit, changes it. Validators mirror the API's rules; the API's refusals
 * show in French.
 */
@Component({
  selector: 'app-expense-form',
  imports: [ReactiveFormsModule],
  template: `
    <form [formGroup]="form" (ngSubmit)="save()" novalidate class="flex max-w-md flex-col gap-2">
      <h2 class="text-xl font-semibold">{{ expense() ? 'Modifier la dépense' : 'Nouvelle dépense' }}</h2>

      <label for="date">Date</label>
      <input id="date" type="date" [min]="firstDay()" [max]="lastDay()" formControlName="date" />
      @if (errorFor('date'); as message) {
        <p class="field-error text-sm text-red-700">{{ message }}</p>
      }

      <label for="description">Description</label>
      <input id="description" formControlName="description" />
      <p class="text-sm">{{ form.controls.description.value.length }}/{{ descriptionMaxLength }}</p>
      @if (errorFor('description'); as message) {
        <p class="field-error text-sm text-red-700">{{ message }}</p>
      }

      <label for="amount">Montant (€)</label>
      <input id="amount" type="number" min="0.01" max="999999.99" step="0.01" formControlName="amount" />
      @if (errorFor('amount'); as message) {
        <p class="field-error text-sm text-red-700">{{ message }}</p>
      }

      <label for="storeName">Enseigne (facultatif)</label>
      <input id="storeName" formControlName="storeName" />
      @if (errorFor('storeName'); as message) {
        <p class="field-error text-sm text-red-700">{{ message }}</p>
      }

      <label for="street">Rue</label>
      <input id="street" formControlName="street" />
      @if (errorFor('street'); as message) {
        <p class="field-error text-sm text-red-700">{{ message }}</p>
      }

      <label for="postalCode">Code postal</label>
      <input id="postalCode" formControlName="postalCode" inputmode="numeric" />
      @if (errorFor('postalCode'); as message) {
        <p class="field-error text-sm text-red-700">{{ message }}</p>
      }

      <label for="city">Ville</label>
      <input id="city" formControlName="city" />
      @if (errorFor('city'); as message) {
        <p class="field-error text-sm text-red-700">{{ message }}</p>
      }

      @if (error(); as message) {
        <p role="alert">{{ message }}</p>
      }
      <div class="mt-2 flex gap-4">
        <button type="submit" [disabled]="saving()">{{ expense() ? 'Enregistrer' : 'Ajouter' }}</button>
        @if (expense()) {
          <button type="button" (click)="cancelled.emit()">Annuler</button>
        }
      </div>
    </form>
  `,
})
export class ExpenseFormComponent {
  readonly reportId = input.required<string>();
  readonly year = input.required<number>();
  /** 1 for January to 12 for December. */
  readonly month = input.required<number>();
  /** The expense to edit, as loaded; undefined to add one. */
  readonly expense = input<ExpenseDetails>();

  /** An expense was added or changed; the form is empty again, ready for the next one. */
  readonly saved = output<'created' | 'updated'>();
  readonly cancelled = output<void>();
  /** The API says the report changed (its user was deleted): the page should read it again. */
  readonly reportChanged = output<void>();

  private readonly api = inject(ExpensesApi);
  protected readonly descriptionMaxLength = descriptionMaxLength;
  protected readonly saving = signal(false);
  protected readonly error = signal<string | null>(null);
  /** "2025-10" for October 2025: the start of every date of the report's month. */
  private readonly monthPrefix = computed(() => `${this.year()}-${String(this.month()).padStart(2, '0')}`);
  protected readonly firstDay = computed(() => `${this.monthPrefix()}-01`);
  /** Day 0 of the next month is the last day of this one. */
  protected readonly lastDay = computed(() => {
    const lastDay = new Date(this.year(), this.month(), 0).getDate();
    return `${this.monthPrefix()}-${String(lastDay).padStart(2, '0')}`;
  });

  /** One of the report's month's days: "2025-10-15" starts with "2025-10-". Empty is left to Validators.required. */
  private readonly withinMonth: ValidatorFn = (control: AbstractControl<string>) => {
    if (control.value === '') {
      return null;
    }
    const isDate = /^[0-9]{4}-[0-9]{2}-[0-9]{2}$/.test(control.value);
    return isDate && control.value.startsWith(`${this.monthPrefix()}-`) ? null : { outsideMonth: true };
  };

  private readonly formBuilder = inject(NonNullableFormBuilder);
  protected readonly form = this.formBuilder.group({
    date: ['', [Validators.required, this.withinMonth]],
    description: ['', [requiredText, Validators.maxLength(descriptionMaxLength)]],
    amount: this.formBuilder.control<number | null>(null, [
      Validators.required,
      Validators.min(minAmount),
      Validators.max(maxAmount),
      twoDecimals,
    ]),
    storeName: ['', Validators.maxLength(storeNameMaxLength)],
    street: ['', [requiredText, Validators.maxLength(streetMaxLength)]],
    postalCode: ['', [Validators.required, Validators.pattern(postalCodePattern)]],
    city: ['', [requiredText, Validators.maxLength(cityMaxLength)]],
  });

  constructor() {
    // Switching between adding and editing starts over from the expense given (or an empty form).
    effect(() => {
      const expense = this.expense();
      untracked(() => this.fill(expense));
    });
  }

  protected errorFor(field: Field): string | null {
    const control = this.form.controls[field];
    if (control.valid || !control.touched) {
      return null;
    }
    if (control.hasError('required')) {
      return 'Ce champ est obligatoire.';
    }
    if (control.hasError('maxlength')) {
      return `${control.getError('maxlength').requiredLength} caractères au maximum.`;
    }
    switch (field) {
      case 'date':
        return 'Choisissez une date du mois de la note de frais.';
      case 'amount':
        return 'Indiquez un montant entre 0,01 et 999 999,99 €, avec au plus deux décimales.';
      default:
        return 'Le code postal doit comporter 5 chiffres.';
    }
  }

  protected save(): void {
    if (this.form.invalid) {
      this.form.markAllAsTouched();
      return;
    }
    const value = this.form.getRawValue();
    const body: SaveExpense = {
      date: value.date,
      description: value.description,
      amount: value.amount ?? 0,
      billingAddress: {
        storeName: value.storeName.trim() === '' ? null : value.storeName,
        street: value.street,
        postalCode: value.postalCode,
        city: value.city,
      },
    };
    const expense = this.expense();
    const request: Observable<unknown> =
      expense === undefined
        ? this.api.create(this.reportId(), body)
        : this.api.update(this.reportId(), expense.id, body, expense.version);

    this.saving.set(true);
    this.error.set(null);
    request.subscribe({
      next: () => {
        this.saving.set(false);
        // Adding again starts from an empty form; after an edit, the page leaves edit mode, which empties it.
        if (expense === undefined) {
          this.fill(undefined);
        }
        this.saved.emit(expense === undefined ? 'created' : 'updated');
      },
      error: (error: unknown) => {
        this.saving.set(false);
        this.error.set(this.saveErrorMessage(error));
        if (businessRuleCode(error) === 'expense_report.deleted_user') {
          this.reportChanged.emit();
        }
      },
    });
  }

  private fill(expense: ExpenseDetails | undefined): void {
    this.error.set(null);
    this.form.reset({
      date: expense?.date ?? '',
      description: expense?.description ?? '',
      amount: expense?.amount ?? null,
      storeName: expense?.billingAddress.storeName ?? '',
      street: expense?.billingAddress.street ?? '',
      postalCode: expense?.billingAddress.postalCode ?? '',
      city: expense?.billingAddress.city ?? '',
    });
  }

  private saveErrorMessage(error: unknown): string {
    const ruleMessage = ruleMessages.get(businessRuleCode(error) ?? '');
    if (ruleMessage !== undefined) {
      return ruleMessage;
    }
    if (hasStatus(error, 409, 412)) {
      return "Cette dépense a été modifiée entre-temps. Rechargez-la avant de l'enregistrer.";
    }
    if (hasStatus(error, 404)) {
      return this.expense() === undefined ? 'Note de frais introuvable.' : 'Dépense introuvable.';
    }
    if (hasStatus(error, 400)) {
      return 'Les informations saisies sont invalides.';
    }
    return "L'opération a échoué. Veuillez réessayer.";
  }
}
