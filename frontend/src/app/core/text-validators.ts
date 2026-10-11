import { AbstractControl, ValidationErrors, ValidatorFn } from '@angular/forms';

/** Like Validators.required, but text made only of spaces is missing too: the API would trim it to nothing. */
export function requiredText(control: AbstractControl<string>): ValidationErrors | null {
  return control.value.trim() === '' ? { required: true } : null;
}

/**
 * Like Validators.maxLength, but measured once trimmed, as the API does: the spaces around a text, which it drops, do
 * not make it too long. The error is Validators.maxLength's, so forms show it the same way.
 */
export function maxTrimmedLength(maxLength: number): ValidatorFn {
  return (control: AbstractControl<string>) => {
    const length = control.value.trim().length;
    return length > maxLength ? { maxlength: { requiredLength: maxLength, actualLength: length } } : null;
  };
}
