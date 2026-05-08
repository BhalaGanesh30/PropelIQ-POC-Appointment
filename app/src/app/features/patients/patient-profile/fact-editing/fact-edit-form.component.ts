import {
  ChangeDetectionStrategy,
  Component,
  inject,
  input,
  output,
} from '@angular/core';
import { ReactiveFormsModule, FormBuilder, Validators } from '@angular/forms';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatInputModule } from '@angular/material/input';
import { MatButtonModule } from '@angular/material/button';
import { MatProgressSpinnerModule } from '@angular/material/progress-spinner';
import { MatIconModule } from '@angular/material/icon';

import type { ClinicalFactDto, PatchFactRequestDto } from '../../../../shared/models/clinical-fact.model';

/** Emitted when the user submits a valid edit. */
export interface FactEditSubmitEvent {
  factId: string;
  dto: PatchFactRequestDto;
  etag: string | null;
}

/**
 * Inline edit form for a single clinical fact (US_047 AC-1).
 *
 * Renders name + value text inputs pre-populated from `fact`.
 * Emits `submitted` with the request payload and ETag on valid submit.
 * Emits `cancelled` when the user clicks Cancel.
 *
 * Accessibility:
 * - Error messages use `aria-describedby` (UXR-205).
 * - Submit button is disabled and shows spinner during `saving` (UXR-501).
 * - Cancel button restores focus to Edit trigger via the parent component.
 */
@Component({
  selector: 'app-fact-edit-form',
  standalone: true,
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [
    ReactiveFormsModule,
    MatFormFieldModule,
    MatInputModule,
    MatButtonModule,
    MatProgressSpinnerModule,
    MatIconModule,
  ],
  template: `
    <form
      [formGroup]="form"
      (ngSubmit)="onSubmit()"
      class="fact-edit-form"
      aria-label="Edit clinical fact"
      novalidate
    >
      <mat-form-field appearance="outline" class="fact-edit-form__field">
        <mat-label>Name</mat-label>
        <input
          matInput
          formControlName="name"
          id="fact-name-input"
          aria-describedby="fact-name-error"
          autocomplete="off"
        />
        @if (form.controls['name'].invalid && form.controls['name'].touched) {
          <mat-error id="fact-name-error" role="alert">
            @if (form.controls['name'].errors?.['required']) { Name is required. }
            @if (form.controls['name'].errors?.['maxlength']) { Name must be 255 characters or fewer. }
          </mat-error>
        }
      </mat-form-field>

      <mat-form-field appearance="outline" class="fact-edit-form__field">
        <mat-label>Value</mat-label>
        <input
          matInput
          formControlName="value"
          id="fact-value-input"
          aria-describedby="fact-value-error"
          autocomplete="off"
        />
        @if (form.controls['value'].invalid && form.controls['value'].touched) {
          <mat-error id="fact-value-error" role="alert">
            Value is required.
          </mat-error>
        }
      </mat-form-field>

      <div class="fact-edit-form__actions">
        <button
          mat-flat-button
          color="primary"
          type="submit"
          [disabled]="form.invalid || saving()"
          aria-label="Save changes"
        >
          @if (saving()) {
            <mat-spinner diameter="18" strokeWidth="2" />
          } @else {
            Save
          }
        </button>
        <button
          mat-button
          type="button"
          (click)="cancelled.emit()"
          [disabled]="saving()"
        >
          Cancel
        </button>
      </div>
    </form>
  `,
  styles: [`
    .fact-edit-form {
      display: flex;
      flex-direction: column;
      gap: 0;
      padding: 8px 0 4px;
    }

    .fact-edit-form__field {
      width: 100%;
    }

    .fact-edit-form__actions {
      display: flex;
      gap: 8px;
      align-items: center;
      margin-top: 4px;
    }

    mat-spinner {
      display: inline-block;
      vertical-align: middle;
    }
  `],
})
export class FactEditFormComponent {
  readonly fact = input.required<ClinicalFactDto>();
  readonly saving = input<boolean>(false);

  readonly submitted = output<FactEditSubmitEvent>();
  readonly cancelled = output<void>();

  private readonly fb = inject(FormBuilder);

  readonly form = this.fb.nonNullable.group({
    name:  ['', [Validators.required, Validators.maxLength(255)]],
    value: ['', Validators.required],
  });

  ngOnInit(): void {
    const f = this.fact();
    this.form.setValue({ name: f.name ?? '', value: f.value });
  }

  onSubmit(): void {
    if (this.form.invalid) {
      this.form.markAllAsTouched();
      return;
    }

    const { name, value } = this.form.getRawValue();
    this.submitted.emit({
      factId: this.fact().factId,
      dto: { name, value },
      etag: this.fact().etag ?? null,
    });
  }
}
