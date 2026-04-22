import { ChangeDetectionStrategy, Component, inject, signal } from '@angular/core';
import {
  FormControl,
  FormGroup,
  ReactiveFormsModule,
  Validators,
} from '@angular/forms';
import { RouterLink } from '@angular/router';
import { MatButtonModule } from '@angular/material/button';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatIconModule } from '@angular/material/icon';
import { MatInputModule } from '@angular/material/input';
import { MatProgressSpinnerModule } from '@angular/material/progress-spinner';
import { AuthService } from '../../services/auth.service';

@Component({
  selector: 'app-forgot-password',
  standalone: true,
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [
    ReactiveFormsModule,
    RouterLink,
    MatButtonModule,
    MatFormFieldModule,
    MatIconModule,
    MatInputModule,
    MatProgressSpinnerModule,
  ],
  templateUrl: './forgot-password.component.html',
  styleUrl: './forgot-password.component.scss',
})
export class ForgotPasswordComponent {
  private readonly authService = inject(AuthService);

  readonly isLoading = signal(false);
  /** True after a submission attempt — shows the generic confirmation view (AC-1). */
  readonly isSubmitted = signal(false);

  readonly forgotForm = new FormGroup({
    email: new FormControl('', [Validators.required, Validators.email]),
  });

  onSubmit(): void {
    if (this.forgotForm.invalid) {
      this.forgotForm.markAllAsTouched();
      return;
    }

    this.isLoading.set(true);

    this.authService.forgotPassword(this.forgotForm.value.email!).subscribe({
      next: () => {
        this.isLoading.set(false);
        this.isSubmitted.set(true);
      },
      error: () => {
        // Show the same confirmation regardless of outcome — never confirm email existence (AC-1).
        this.isLoading.set(false);
        this.isSubmitted.set(true);
      },
    });
  }
}
