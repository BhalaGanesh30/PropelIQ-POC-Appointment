import {
  ChangeDetectionStrategy,
  Component,
  OnInit,
  inject,
  signal,
} from '@angular/core';
import { ActivatedRoute } from '@angular/router';
import { MatTabsModule, MatTabChangeEvent } from '@angular/material/tabs';
import { MatIconModule } from '@angular/material/icon';
import { MatProgressSpinnerModule } from '@angular/material/progress-spinner';
import { MatButtonModule } from '@angular/material/button';
import { MatTooltipModule } from '@angular/material/tooltip';
import { DatePipe } from '@angular/common';

import { PatientProfileFacade, TabId } from '../patient-profile.facade';
import { ConflictAlertsFacade } from '../conflict-alerts.facade';
import { AiGatewayStatusFacade } from '../../../shared/facades/ai-gateway-status.facade';
import { AiFallbackBannerComponent } from '../../../shared/components/ai-fallback-banner/ai-fallback-banner.component';
import { ProfileSummaryTabComponent } from './tabs/profile-summary-tab.component';
import { ProfileTimelineTabComponent } from './tabs/profile-timeline-tab.component';
import { ProfileDocumentsTabComponent } from './tabs/profile-documents-tab.component';
import { ProfileInsuranceTabComponent } from './tabs/profile-insurance-tab.component';
import { ProfileCodingTabComponent } from './tabs/profile-coding-tab.component';
import { ProfileConflictsTabComponent } from './tabs/profile-conflicts-tab.component';

/** Tab definitions in render order. */
const TABS: { id: TabId; label: string; icon: string }[] = [
  { id: 'summary',   label: 'Clinical Summary', icon: 'summarize'          },
  { id: 'timeline',  label: 'Timeline',          icon: 'timeline'           },
  { id: 'documents', label: 'Documents',          icon: 'folder_open'       },
  { id: 'insurance', label: 'Insurance',          icon: 'health_and_safety' },
  { id: 'coding',    label: 'Coding',             icon: 'code'              },
  { id: 'conflicts', label: 'Conflicts',           icon: 'warning'           },
];

/** Index of the Conflicts tab in TABS — used for the tab-switch guard (AC-3). */
const CONFLICTS_TAB_INDEX = 5;

/**
 * 360° patient profile page (SCR-014 / UXR-107).
 *
 * Facade is provided at component level so each profile page instance gets
 * its own state (allows navigation between patients without stale data).
 */
@Component({
  selector: 'app-patient-profile',
  standalone: true,
  changeDetection: ChangeDetectionStrategy.OnPush,
  providers: [PatientProfileFacade, ConflictAlertsFacade],
  imports: [
    DatePipe,
    MatTabsModule,
    MatIconModule,
    MatProgressSpinnerModule,
    MatButtonModule,
    MatTooltipModule,
    AiFallbackBannerComponent,
    ProfileSummaryTabComponent,
    ProfileTimelineTabComponent,
    ProfileDocumentsTabComponent,
    ProfileInsuranceTabComponent,
    ProfileCodingTabComponent,
    ProfileConflictsTabComponent,
  ],
  templateUrl: './patient-profile.component.html',
  styleUrl: './patient-profile.component.scss',
})
export class PatientProfileComponent implements OnInit {
  protected readonly facade = inject(PatientProfileFacade);
  protected readonly conflictsFacade = inject(ConflictAlertsFacade);
  protected readonly aiStatusFacade = inject(AiGatewayStatusFacade);
  private readonly route = inject(ActivatedRoute);

  protected readonly tabs = TABS;
  /** Bound to [selectedIndex] to support the tab-switch guard (AC-3). */
  protected readonly selectedTabIndex = signal(0);

  ngOnInit(): void {
    const patientId = this.route.snapshot.paramMap.get('id') ?? '';
    this.facade.init(patientId);
  }

  protected onTabChange(event: MatTabChangeEvent): void {
    // Tab-switch guard: block navigation away from Conflicts tab while there
    // are unacknowledged Critical alerts (AC-3 / UXR-206).
    if (
      this.conflictsFacade.pendingCritical().length > 0 &&
      event.index !== CONFLICTS_TAB_INDEX
    ) {
      // Revert to Conflicts tab.
      this.selectedTabIndex.set(CONFLICTS_TAB_INDEX);
      return;
    }

    this.selectedTabIndex.set(event.index);
    const tab = TABS[event.index];
    if (tab) {
      this.facade.activateTab(tab.id);
    }
  }
}
