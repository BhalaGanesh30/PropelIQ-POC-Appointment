import { AbstractControl, ValidationErrors, ValidatorFn } from '@angular/forms';

/**
 * Validates password complexity (us_018 AC-2):
 * 8+ characters, 1 uppercase, 1 digit, 1 special character.
 *
 * Returns `{ passwordStrength: { minLength?, uppercase?, digit?, special? } }`
 * so templates can render per-rule messages.
 */
export function passwordStrengthValidator(): ValidatorFn {
  return (control: AbstractControl): ValidationErrors | null => {
    const value = control.value as string;
    if (!value) return null;

    const errors: Record<string, string> = {};

    if (value.length < 8) {
      errors['minLength'] = 'Password must be at least 8 characters.';
    }
    if (!/[A-Z]/.test(value)) {
      errors['uppercase'] = 'Password must contain at least one uppercase letter.';
    }
    if (!/[0-9]/.test(value)) {
      errors['digit'] = 'Password must contain at least one digit.';
    }
    if (!/[^a-zA-Z0-9]/.test(value)) {
      errors['special'] = 'Password must contain at least one special character.';
    }

    return Object.keys(errors).length > 0 ? { passwordStrength: errors } : null;
  };
}

/**
 * Cross-field validator: sets `passwordMismatch` on the confirm field
 * when it does not equal the password field.
 */
export function passwordMatchValidator(
  passwordField: string,
  confirmField: string,
): ValidatorFn {
  return (group: AbstractControl): ValidationErrors | null => {
    const password = group.get(passwordField)?.value as string | null;
    const confirm = group.get(confirmField)?.value as string | null;

    if (password && confirm && password !== confirm) {
      group.get(confirmField)?.setErrors({ passwordMismatch: true });
      return { passwordMismatch: true };
    }

    // Clear the error when the values match (avoid stale error).
    const confirmCtrl = group.get(confirmField);
    if (confirmCtrl?.hasError('passwordMismatch')) {
      const { passwordMismatch: _, ...remaining } = confirmCtrl.errors ?? {};
      confirmCtrl.setErrors(Object.keys(remaining).length ? remaining : null);
    }

    return null;
  };
}
