import { HttpErrorResponse } from '@angular/common/http';
import { Component, inject, signal } from '@angular/core';
import { NonNullableFormBuilder, ReactiveFormsModule, Validators } from '@angular/forms';
import { ActivatedRoute, Router, RouterLink } from '@angular/router';
import { Observable } from 'rxjs';
import { cityMaxLength, postalCodePattern, streetMaxLength } from '../../core/address-rules';
import { hasStatus } from '../../core/problem-details';
import { maxTrimmedLength, requiredText } from '../../core/text-validators';
import { SaveUser } from './user-details';
import { UsersApi } from './users.api';

/**
 * The API's limits (User.NameMaxLength, the quota rule and, from address-rules, the address ones), checked here first
 * for immediate feedback. The API stays the authority: should the two differ, the fields it
 * rejects are flagged from its answer.
 */
const nameMaxLength = 100;
const minMonthlyExpenseQuota = 1;

const fields = ['firstName', 'lastName', 'street', 'postalCode', 'city', 'monthlyExpenseQuota'] as const;
type Field = (typeof fields)[number];

/**
 * The fields named in a 400's validation errors. The API names them by path ("FirstName", "Address.PostalCode") or,
 * for a rule its domain checks, by parameter ("street"): the last segment, whatever its case, is the field.
 */
function rejectedFields(error: unknown): Field[] {
  if (!(error instanceof HttpErrorResponse) || error.status !== 400) {
    return [];
  }
  const errors: unknown = (error.error as { errors?: unknown } | null)?.errors;
  if (typeof errors !== 'object' || errors === null) {
    return [];
  }
  return Object.keys(errors).flatMap((key) => {
    const name = key.split('.').at(-1)?.toLowerCase();
    return fields.filter((field) => field.toLowerCase() === name);
  });
}

function saveErrorMessage(error: unknown): string {
  if (hasStatus(error, 404)) {
    return 'Utilisateur introuvable.';
  }
  if (hasStatus(error, 400)) {
    return 'Les informations saisies sont invalides.';
  }
  if (hasStatus(error, 409, 412)) {
    return "L'utilisateur a été modifié entre-temps. Rechargez la page pour repartir de ses dernières informations.";
  }
  return "L'enregistrement a échoué. Veuillez réessayer.";
}

@Component({
  selector: 'app-user-form-page',
  imports: [ReactiveFormsModule, RouterLink],
  template: `
    <a routerLink="/users" class="link text-sm">← Utilisateurs</a>
    <h1 class="mt-2 mb-6 text-2xl font-semibold text-slate-900">{{ title }}</h1>
    @if (error(); as message) {
      <p role="alert" class="mb-4 max-w-2xl">{{ message }}</p>
    }
    @if (loaded()) {
      <form
        [formGroup]="form"
        (ngSubmit)="save()"
        novalidate
        class="card grid max-w-2xl gap-x-4 gap-y-5 p-6 sm:grid-cols-2"
      >
        <div class="field">
          <label for="firstName">Prénom</label>
          <input id="firstName" formControlName="firstName" />
          @if (errorFor('firstName'); as message) {
            <p class="field-error">{{ message }}</p>
          }
        </div>

        <div class="field">
          <label for="lastName">Nom</label>
          <input id="lastName" formControlName="lastName" />
          @if (errorFor('lastName'); as message) {
            <p class="field-error">{{ message }}</p>
          }
        </div>

        <div class="field sm:col-span-2">
          <label for="street">Rue</label>
          <input id="street" formControlName="street" />
          @if (errorFor('street'); as message) {
            <p class="field-error">{{ message }}</p>
          }
        </div>

        <div class="field">
          <label for="postalCode">Code postal</label>
          <input id="postalCode" formControlName="postalCode" inputmode="numeric" />
          @if (errorFor('postalCode'); as message) {
            <p class="field-error">{{ message }}</p>
          }
        </div>

        <div class="field">
          <label for="city">Ville</label>
          <input id="city" formControlName="city" />
          @if (errorFor('city'); as message) {
            <p class="field-error">{{ message }}</p>
          }
        </div>

        <div class="field">
          <label for="monthlyExpenseQuota">Quota mensuel de dépenses</label>
          <input id="monthlyExpenseQuota" type="number" min="1" step="1" formControlName="monthlyExpenseQuota" />
          @if (errorFor('monthlyExpenseQuota'); as message) {
            <p class="field-error">{{ message }}</p>
          }
        </div>

        <div class="flex gap-3 border-t border-slate-100 pt-5 sm:col-span-2">
          <button type="submit" class="btn-primary" [disabled]="saving()">Enregistrer</button>
          <a routerLink="/users" class="btn">Annuler</a>
        </div>
      </form>
    } @else if (!error()) {
      <p class="muted">Chargement…</p>
    }
  `,
})
export default class UserFormPage {
  private readonly api = inject(UsersApi);
  private readonly router = inject(Router);
  /** Null on /users/new. */
  private readonly id = inject(ActivatedRoute).snapshot.paramMap.get('id');
  protected readonly title = this.id === null ? 'Nouvel utilisateur' : "Modifier l'utilisateur";
  protected readonly loaded = signal(this.id === null);
  protected readonly saving = signal(false);
  protected readonly error = signal<string | null>(null);
  /** The version of the user being edited, as loaded. */
  private version = 0;

  private readonly formBuilder = inject(NonNullableFormBuilder);
  protected readonly form = this.formBuilder.group({
    firstName: ['', [requiredText, maxTrimmedLength(nameMaxLength)]],
    lastName: ['', [requiredText, maxTrimmedLength(nameMaxLength)]],
    street: ['', [requiredText, maxTrimmedLength(streetMaxLength)]],
    postalCode: ['', [Validators.required, Validators.pattern(postalCodePattern)]],
    city: ['', [requiredText, maxTrimmedLength(cityMaxLength)]],
    monthlyExpenseQuota: this.formBuilder.control<number | null>(null, [
      Validators.required,
      Validators.min(minMonthlyExpenseQuota),
      Validators.pattern(/^\d+$/),
    ]),
  });

  constructor() {
    if (this.id !== null) {
      this.api.get(this.id).subscribe({
        next: (user) => {
          this.form.setValue({
            firstName: user.firstName,
            lastName: user.lastName,
            street: user.address.street,
            postalCode: user.address.postalCode,
            city: user.address.city,
            monthlyExpenseQuota: user.monthlyExpenseQuota,
          });
          this.version = user.version;
          this.loaded.set(true);
        },
        error: (error: unknown) =>
          this.error.set(hasStatus(error, 404) ? 'Utilisateur introuvable.' : "Impossible de charger l'utilisateur."),
      });
    }
  }

  protected errorFor(field: Field): string | null {
    const control = this.form.controls[field];
    if (control.valid || !control.touched) {
      return null;
    }
    if (control.hasError('required')) {
      return 'Ce champ est obligatoire.';
    }
    if (control.hasError('rejected')) {
      return 'Valeur refusée par le serveur.';
    }
    if (control.hasError('maxlength')) {
      return `${control.getError('maxlength').requiredLength} caractères au maximum.`;
    }
    return field === 'postalCode'
      ? 'Le code postal doit comporter 5 chiffres.'
      : 'Le quota doit être un nombre entier supérieur ou égal à 1.';
  }

  protected save(): void {
    if (this.form.invalid) {
      this.form.markAllAsTouched();
      return;
    }
    const value = this.form.getRawValue();
    const user: SaveUser = {
      firstName: value.firstName,
      lastName: value.lastName,
      address: { street: value.street, postalCode: value.postalCode, city: value.city },
      monthlyExpenseQuota: value.monthlyExpenseQuota ?? 0,
    };
    const request: Observable<unknown> =
      this.id === null ? this.api.create(user) : this.api.update(this.id, user, this.version);

    this.saving.set(true);
    this.error.set(null);
    request.subscribe({
      next: () => {
        void this.router.navigateByUrl('/users');
      },
      error: (error: unknown) => {
        this.saving.set(false);
        this.error.set(saveErrorMessage(error));
        // Cleared by the next edit of the field, which runs its validators again.
        for (const field of rejectedFields(error)) {
          this.form.controls[field].setErrors({ rejected: true });
          this.form.controls[field].markAsTouched();
        }
      },
    });
  }
}
