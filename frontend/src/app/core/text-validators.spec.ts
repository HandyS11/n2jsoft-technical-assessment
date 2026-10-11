import { FormControl } from '@angular/forms';
import { maxTrimmedLength, requiredText } from './text-validators';

describe('requiredText', () => {
  it('accepts text', () => {
    expect(requiredText(new FormControl('Taxi', { nonNullable: true }))).toBeNull();
  });

  it('refuses text made only of spaces', () => {
    expect(requiredText(new FormControl('   ', { nonNullable: true }))).toEqual({ required: true });
  });
});

describe('maxTrimmedLength', () => {
  it('does not count the spaces around the text', () => {
    expect(maxTrimmedLength(4)(new FormControl('  Taxi  ', { nonNullable: true }))).toBeNull();
  });

  it('refuses text longer than the limit once trimmed, with the error of Validators.maxLength', () => {
    expect(maxTrimmedLength(3)(new FormControl(' Taxi ', { nonNullable: true }))).toEqual({
      maxlength: { requiredLength: 3, actualLength: 4 },
    });
  });
});
