import {
  ChangeDetectionStrategy,
  Component,
  inject,
  OnInit,
  signal,
} from '@angular/core';
import { ReactiveFormsModule, FormBuilder, Validators } from '@angular/forms';
import { Router, ActivatedRoute } from '@angular/router';
import { MatButtonModule } from '@angular/material/button';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatInputModule } from '@angular/material/input';
import { MatProgressSpinnerModule } from '@angular/material/progress-spinner';
import { MatIconModule } from '@angular/material/icon';
import { StaffManagementService } from '../../../admin/services/staff-management.service';

type PageState = 'form' | 'expired' | 'success';

/**
 * AC-2/AC-4: Account activation page reached via invitation email link.
 * Reads `email` and `token` from query parameters, presents a set-password form,
 * shows "Invitation Expired" when the API returns 400/410, and redirects to
 * /login on success.
 */
@Component({
  selector: 'app-activate',
  standalone: true,
  imports: [
    ReactiveFormsModule,
    MatButtonModule,
    MatFormFieldModule,
    MatInputModule,
    MatProgressSpinnerModule,
    MatIconModule,
  ],
  templateUrl: './activate.component.html',
  styleUrl: './activate.component.scss',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class ActivateComponent implements OnInit {
  private readonly fb = inject(FormBuilder);
  private readonly route = inject(ActivatedRoute);
  private readonly router = inject(Router);
  private readonly staffService = inject(StaffManagementService);

  readonly pageState = signal<PageState>('form');
  readonly isSubmitting = signal(false);
  readonly serverError = signal<string | null>(null);

  private email = '';
  private token = '';

  readonly form = this.fb.group(
    {
      password: ['', [Validators.required, Validators.minLength(8)]],
      confirmPassword: ['', Validators.required],
    },
    { validators: this.passwordMatchValidator },
  );

  ngOnInit(): void {
    const params = this.route.snapshot.queryParamMap;
    this.email = params.get('email') ?? '';
    this.token = params.get('token') ?? '';

    if (!this.email || !this.token) {
      this.pageState.set('expired');
    }
  }

  private passwordMatchValidator(
    group: import('@angular/forms').AbstractControl,
  ): Record<string, boolean> | null {
    const pw = group.get('password')?.value;
    const cpw = group.get('confirmPassword')?.value;
    return pw && cpw && pw !== cpw ? { passwordMismatch: true } : null;
  }

  onSubmit(): void {
    if (this.form.invalid || this.isSubmitting()) return;

    this.isSubmitting.set(true);
    this.serverError.set(null);

    this.staffService
      .activateStaff({
        email: this.email,
        token: this.token,
        password: this.form.value.password!,
      })
      .subscribe({
        next: () => {
          this.isSubmitting.set(false);
          this.pageState.set('success');
          setTimeout(() => void this.router.navigate(['/login']), 3000);
        },
        error: (err: { status?: number; error?: { detail?: string } }) => {
          this.isSubmitting.set(false);
          if (err.status === 400 || err.status === 410) {
            this.pageState.set('expired');
          } else {
            this.serverError.set(
              err.error?.detail ?? 'Activation failed. Please try again.',
            );
          }
        },
      });
  }

  navigateToLogin(): void {
    void this.router.navigate(['/login']);
  }
}
