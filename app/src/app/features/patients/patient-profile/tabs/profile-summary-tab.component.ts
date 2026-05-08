import {
  ChangeDetectionStrategy,
  Component,
  computed,
  inject,
} from '@angular/core';
import { MatProgressSpinnerModule } from '@angular/material/progress-spinner';
import { MatButtonModule } from '@angular/material/button';
import { MatIconModule } from '@angular/material/icon';
import { MatDividerModule } from '@angular/material/divider';

import { PatientProfileFacade } from '../../patient-profile.facade';
import { FactListComponent } from '../fact-list/fact-list.component';
import { PatientProfileSkeletonComponent } from '../shared/patient-profile-skeleton.component';
import { PartialDataWarningComponent } from '../shared/partial-data-warning.component';
import type { ClinicalFactDto } from '../../../../shared/models/clinical-fact.model';

/**
 * Summary tab — shows medications, allergies, and diagnoses grouped by factType.
 * SCR-014 AC-1, AC-2, AIR-004.
 */
@Component({
  selector: 'app-profile-summary-tab',
  standalone: true,
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [
    MatProgressSpinnerModule,
    MatButtonModule,
    MatIconModule,
    MatDividerModule,
    FactListComponent,
    PatientProfileSkeletonComponent,
    PartialDataWarningComponent,
  ],
  template: `
    @if (state().loading) {
      <app-patient-profile-skeleton />
    } @else if (state().error) {
      <div class="tab-error" role="alert">
        <mat-icon aria-hidden="true">error_outline</mat-icon>
        <p>{{ state().error }}</p>
        <button mat-stroked-button type="button" (click)="facade.reloadTab('summary')">
          Retry
        </button>
      </div>
    } @else {
      @if (state().partialSources.length > 0) {
        <app-partial-data-warning [sources]="state().partialSources" />
      }

      <!-- Medications -->
      <section class="fact-section" aria-labelledby="meds-heading">
        <h3 id="meds-heading" class="fact-section__title">
          <mat-icon aria-hidden="true">medication</mat-icon>
          Medications
          <span class="fact-section__count">{{ medications().length }}</span>
        </h3>
        <mat-divider />
        <app-fact-list
          [facts]="medications()"
          emptyMessage="No medications extracted. Upload documents to populate this profile."
          emptyIcon="medication"
          (factUpdated)="facade.updateFact($event)"
        />
      </section>

      <!-- Allergies -->
      <section class="fact-section" aria-labelledby="allergies-heading">
        <h3 id="allergies-heading" class="fact-section__title">
          <mat-icon aria-hidden="true">warning_amber</mat-icon>
          Allergies
          <span class="fact-section__count">{{ allergies().length }}</span>
        </h3>
        <mat-divider />
        <app-fact-list
          [facts]="allergies()"
          emptyMessage="No allergies extracted."
          emptyIcon="warning_amber"
          (factUpdated)="facade.updateFact($event)"
        />
      </section>

      <!-- Diagnoses -->
      <section class="fact-section" aria-labelledby="diagnoses-heading">
        <h3 id="diagnoses-heading" class="fact-section__title">
          <mat-icon aria-hidden="true">local_hospital</mat-icon>
          Diagnoses
          <span class="fact-section__count">{{ diagnoses().length }}</span>
        </h3>
        <mat-divider />
        <app-fact-list
          [facts]="diagnoses()"
          emptyMessage="No diagnoses extracted."
          emptyIcon="local_hospital"
          (factUpdated)="facade.updateFact($event)"
        />
      </section>
    }
  `,
  styles: [`
    :host { display: flex; flex-direction: column; gap: 24px; padding: 8px 0; }

    .tab-error {
      display: flex; flex-direction: column; align-items: center;
      gap: 12px; padding: 32px; color: var(--color-neutral-700, #616161);
      mat-icon { font-size: 36px; width: 36px; height: 36px; color: #c62828; }
    }

    .fact-section { display: flex; flex-direction: column; gap: 12px; }

    .fact-section__title {
      display: flex; align-items: center; gap: 8px;
      font-size: 14px; font-weight: 600;
      color: var(--color-neutral-800, #424242);
      margin: 0;
      mat-icon { font-size: 18px; width: 18px; height: 18px; color: var(--color-neutral-600, #757575); }
    }

    .fact-section__count {
      margin-left: auto;
      font-size: 12px; font-weight: 400;
      background: var(--color-neutral-200, #e0e0e0);
      color: var(--color-neutral-700, #616161);
      border-radius: 12px; padding: 1px 8px;
    }
  `],
})
export class ProfileSummaryTabComponent {
  protected readonly facade = inject(PatientProfileFacade);
  protected readonly state = this.facade.tabState('summary');

  protected readonly medications = computed(() =>
    this._byType('medication'),
  );
  protected readonly allergies = computed(() =>
    this._byType('allergy'),
  );
  protected readonly diagnoses = computed(() =>
    this._byType('diagnosis'),
  );

  private _byType(type: string): ClinicalFactDto[] {
    return this.state().facts.filter((f) => f.factType === type);
  }
}
