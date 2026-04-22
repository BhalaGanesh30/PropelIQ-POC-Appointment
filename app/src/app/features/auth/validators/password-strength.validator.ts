import { AbstractControl, ValidationErrors, ValidatorFn } from '@angular/forms';

/**
 * Custom reactive-form validator for password strength.
 * Enforces: uppercase, lowercase, digit, and special character.
 * Mirrors backend RegisterRequestValidator rules (UXR-205, NFR-007).
 */
export function passwordStrengthValidator(): ValidatorFn {
  return (control: AbstractControl): ValidationErrors | null => {
    const value: string = control.value ?? '';
    if (!value) return null;

    const hasUpperCase = /[A-Z]/.test(value);
    const hasLowerCase = /[a-z]/.test(value);
    const hasDigit = /\d/.test(value);
    const hasSpecialChar = /[^a-zA-Z0-9]/.test(value);

    if (hasUpperCase && hasLowerCase && hasDigit && hasSpecialChar) {
      return null;
    }

    return {
      passwordStrength: {
        hasUpperCase,
        hasLowerCase,
        hasDigit,
        hasSpecialChar,
      },
    };
  };
}

/**
 * Cross-field validator: confirms that confirmPassword matches password.
 * Applied at FormGroup level (UXR-205).
 */
export function passwordMatchValidator(
  passwordKey: string,
  confirmKey: string
): ValidatorFn {
  return (group: AbstractControl): ValidationErrors | null => {
    const password = group.get(passwordKey)?.value;
    const confirm = group.get(confirmKey)?.value;
    return password === confirm ? null : { passwordMismatch: true };
  };
}
