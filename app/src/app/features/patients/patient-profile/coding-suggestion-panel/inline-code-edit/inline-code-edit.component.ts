import {
  ChangeDetectionStrategy,
  Component,
  OnInit,
  input,
  output,
} from '@angular/core';
import {
  FormBuilder,
  FormGroup,
  ReactiveFormsModule,
  Validators,
} from '@angular/forms';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatInputModule } from '@angular/material/input';
import { MatButtonModule } from '@angular/material/button';
import { MatIconModule } from '@angular/material/icon';

/**
 * Inline code-edit form rendered inside a suggestion card on "Modify" action (US_051 / AC-2).
 *
 * - Pre-populated with the current `code` and `description` inputs.
 * - Amber outlined border while active (design token: `#ff6f00`).
 * - Save button disabled while the form is invalid.
 * - Emits `saved` with `{ code, description }` on save; `cancelled` on cancel.
 * - Keyboard accessible: all fields and buttons reachable via Tab (UXR-202).
 */
@Component({
  selector: 'app-inline-code-edit',
  standalone: true,
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [
    ReactiveFormsModule,
    MatFormFieldModule,
    MatInputModule,
    MatButtonModule,
    MatIconModule,
  ],
  template: `
    <form
      [formGroup]="form"
      class="inline-edit"
      (ngSubmit)="onSave()"
      aria-label="Edit coding suggestion"
    >
      <mat-form-field appearance="outline" class="inline-edit__field">
        <mat-label>Code</mat-label>
        <input
          matInput
          formControlName="code"
          maxlength="20"
          autocomplete="off"
          aria-required="true"
          [attr.aria-label]="'Code for ' + currentDescription()"
        />
        @if (form.get('code')?.hasError('required') && form.get('code')?.touched) {
          <mat-error>Code is required</mat-error>
        }
        @if (form.get('code')?.hasError('maxlength')) {
          <mat-error>Code must be 20 characters or fewer</mat-error>
        }
      </mat-form-field>

      <mat-form-field appearance="outline" class="inline-edit__field">
        <mat-label>Description</mat-label>
        <input
          matInput
          formControlName="description"
          autocomplete="off"
          aria-required="true"
        />
        @if (form.get('description')?.hasError('required') && form.get('description')?.touched) {
          <mat-error>Description is required</mat-error>
        }
      </mat-form-field>

      <div class="inline-edit__actions">
        <button
          mat-flat-button
          type="submit"
          class="inline-edit__save"
          [disabled]="form.invalid"
          aria-label="Save modification"
        >
          <mat-icon aria-hidden="true">check</mat-icon>
          Save
        </button>
        <button
          mat-stroked-button
          type="button"
          (click)="cancelled.emit()"
          aria-label="Cancel modification"
        >
          Cancel
        </button>
      </div>
    </form>
  `,
  styles: [`
    :host { display: block; }

    .inline-edit {
      display: flex;
      flex-direction: column;
      gap: 4px;
      padding: 12px;
      border: 2px solid #ff6f00;
      border-radius: 8px;
      background: #fff8e1;
    }

    .inline-edit__field {
      width: 100%;
    }

    .inline-edit__actions {
      display: flex;
      gap: 8px;
      justify-content: flex-end;
      margin-top: 4px;
    }

    .inline-edit__save {
      background-color: #e65100;
      color: #fff;
    }

    .inline-edit__save[disabled] {
      background-color: #e0e0e0;
      color: #9e9e9e;
    }
  `],
})
export class InlineCodeEditComponent implements OnInit {
  readonly currentCode = input.required<string>();
  readonly currentDescription = input.required<string>();

  /** Emits `{ code, description }` when the clinician saves the modification (AC-2). */
  readonly saved = output<{ code: string; description: string }>();
  /** Emits when the clinician cancels without saving. */
  readonly cancelled = output<void>();

  readonly form: FormGroup;

  constructor(private readonly fb: FormBuilder) {
    this.form = this.fb.group({
      code: ['', [Validators.required, Validators.maxLength(20)]],
      description: ['', Validators.required],
    });
  }

  ngOnInit(): void {
    this.form.patchValue({
      code: this.currentCode(),
      description: this.currentDescription(),
    });
  }

  protected onSave(): void {
    if (this.form.invalid) {
      return;
    }
    const { code, description } = this.form.getRawValue() as {
      code: string;
      description: string;
    };
    this.saved.emit({ code, description });
  }
}
