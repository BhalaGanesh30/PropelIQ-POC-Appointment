import { Pipe, PipeTransform } from '@angular/core';

/**
 * Pure pipe used in the reset-password checklist to test individual
 * password complexity rules without running the full validator.
 *
 * Usage: `value | passwordCheck:'uppercase'`
 * Rules: 'uppercase' | 'digit' | 'special'
 */
@Pipe({ name: 'passwordCheck', standalone: true, pure: true })
export class PasswordCheckPipe implements PipeTransform {
  transform(value: string | null | undefined, rule: string): boolean {
    if (!value) return false;
    switch (rule) {
      case 'uppercase': return /[A-Z]/.test(value);
      case 'digit':     return /[0-9]/.test(value);
      case 'special':   return /[^a-zA-Z0-9]/.test(value);
      default:          return false;
    }
  }
}
