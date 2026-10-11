import { HttpErrorResponse } from '@angular/common/http';
import { Component, inject, signal } from '@angular/core';
import {
  AbstractControl,
  NonNullableFormBuilder,
  ReactiveFormsModule,
  ValidationErrors,
  Validators,
} from '@angular/forms';
import { ActivatedRoute, Router, RouterLink } from '@angular/router';
import { Observable } from 'rxjs';
import { SaveUser } from './user-details';
import { UsersApi } from './users.api';

type Field =
  | 'firstName'
  | 'lastName'
  | 'street'
  | 'postalCode'
  | 'city'
  | 'monthlyExpenseQuota';

/** Like Validators.required, but text made only of spaces is missing too: the API would trim it to nothing. */
function requiredText(control: AbstractControl<string>): ValidationErrors | null {
  return control.value.trim() === '' ? { required: true } : null;
}

function isNotFound(error: unknown): boolean {
  return error instanceof HttpErrorResponse && error.status === 404;
}

function saveErrorMessage(error: unknown): string {
  if (isNotFound(error)) {
    return 'Utilisateur introuvable.';
  }
  if (error instanceof HttpErrorResponse && error.status === 400) {
    return 'Les informations saisies sont invalides.';
  }
  return "L'enregistrement a échoué. Veuillez réessayer.";
}

@Component({
  selector: 'app-user-form-page',
  imports: [ReactiveFormsModule, RouterLink],
  template: `
    <h1 class="mb-4 text-2xl font-semibold">{{ title }}</h1>
    @if (error(); as message) {
      <p role="alert">{{ message }}</p>
    }
    @if (loaded()) {
      <form [formGroup]="form" (ngSubmit)="save()" novalidate class="flex max-w-md flex-col gap-2">
        <label for="firstName">Prénom</label>
        <input id="firstName" formControlName="firstName" />
        @if (errorFor('firstName'); as message) {
          <p class="field-error text-sm text-red-700">{{ message }}</p>
        }

        <label for="lastName">Nom</label>
        <input id="lastName" formControlName="lastName" />
        @if (errorFor('lastName'); as message) {
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

        <label for="monthlyExpenseQuota">Quota mensuel de dépenses</label>
        <input id="monthlyExpenseQuota" type="number" min="1" step="1" formControlName="monthlyExpenseQuota" />
        @if (errorFor('monthlyExpenseQuota'); as message) {
          <p class="field-error text-sm text-red-700">{{ message }}</p>
        }

        <div class="mt-2 flex gap-4">
          <button type="submit" [disabled]="saving()">Enregistrer</button>
          <a routerLink="/users" class="underline">Annuler</a>
        </div>
      </form>
    } @else if (!error()) {
      <p>Chargement…</p>
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

  private readonly formBuilder = inject(NonNullableFormBuilder);
  protected readonly form = this.formBuilder.group({
    firstName: ['', [requiredText, Validators.maxLength(100)]],
    lastName: ['', [requiredText, Validators.maxLength(100)]],
    street: ['', [requiredText, Validators.maxLength(200)]],
    postalCode: ['', [Validators.required, Validators.pattern(/^[0-9]{5}$/)]],
    city: ['', [requiredText, Validators.maxLength(100)]],
    monthlyExpenseQuota: this.formBuilder.control<number | null>(null, [
      Validators.required,
      Validators.min(1),
      Validators.pattern(/^[0-9]+$/),
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
          this.loaded.set(true);
        },
        error: (error: unknown) =>
          this.error.set(
            isNotFound(error) ? 'Utilisateur introuvable.' : "Impossible de charger l'utilisateur."
          ),
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
      this.id === null ? this.api.create(user) : this.api.update(this.id, user);

    this.saving.set(true);
    this.error.set(null);
    request.subscribe({
      next: () => {
        void this.router.navigateByUrl('/users');
      },
      error: (error: unknown) => {
        this.saving.set(false);
        this.error.set(saveErrorMessage(error));
      },
    });
  }
}
