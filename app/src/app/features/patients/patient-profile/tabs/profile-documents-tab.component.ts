import { ChangeDetectionStrategy, Component, input } from '@angular/core';
import { RouterModule } from '@angular/router';
import { MatButtonModule } from '@angular/material/button';
import { MatIconModule } from '@angular/material/icon';

/** Documents tab — links to the patient-scoped document library. */
@Component({
  selector: 'app-profile-documents-tab',
  standalone: true,
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [RouterModule, MatButtonModule, MatIconModule],
  template: `
    <div class="docs-stub">
      <mat-icon class="docs-stub__icon" aria-hidden="true">folder_open</mat-icon>
      <p class="docs-stub__message">
        View and manage all documents for this patient in the Document Library.
      </p>
      <a
        mat-raised-button
        color="primary"
        [routerLink]="['/documents/library']"
        [queryParams]="{ patientId: patientId() }"
        aria-label="Go to document library"
      >
        <mat-icon aria-hidden="true">open_in_new</mat-icon>
        Open Document Library
      </a>
    </div>
  `,
  styles: [`
    :host { display: block; padding: 8px 0; }
    .docs-stub {
      display: flex; flex-direction: column; align-items: center;
      gap: 16px; padding: 48px 24px; text-align: center;
    }
    .docs-stub__icon { font-size: 48px; width: 48px; height: 48px; color: var(--color-neutral-400, #bdbdbd); }
    .docs-stub__message { font-size: 14px; color: var(--color-neutral-600, #757575); max-width: 300px; margin: 0; }
  `],
})
export class ProfileDocumentsTabComponent {
  readonly patientId = input.required<string>();
}
