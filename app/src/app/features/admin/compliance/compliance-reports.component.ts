import {
  ChangeDetectionStrategy,
  Component,
  ViewChild,
} from '@angular/core';
import { MatTabsModule } from '@angular/material/tabs';
import { ComplianceReportConfigComponent } from './compliance-report-config.component';
import { ComplianceReportListComponent } from './compliance-report-list.component';
import { DistributionListComponent } from './distribution-list.component';
import { ScheduleConfigComponent } from './schedule-config.component';

/**
 * SCR-022 Compliance Reports page container (US_058).
 *
 * Route:  /admin/compliance-reports
 * Guard:  roleGuard [Admin]
 * Layout: single-column; three tabs — Generate & Reports / Schedule / Distribution.
 *
 * AC-1: Schedule tab → ScheduleConfigComponent
 * AC-2: Reports tab → ComplianceReportListComponent (PDF download)
 * AC-3: Distribution tab → DistributionListComponent
 * AC-4: Reports tab → ComplianceReportConfigComponent (on-demand generation)
 */
@Component({
  selector: 'app-compliance-reports',
  standalone: true,
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [
    MatTabsModule,
    ComplianceReportConfigComponent,
    ComplianceReportListComponent,
    ScheduleConfigComponent,
    DistributionListComponent,
  ],
  templateUrl: './compliance-reports.component.html',
  styleUrl: './compliance-reports.component.scss',
})
export class ComplianceReportsComponent {
  @ViewChild(ComplianceReportListComponent)
  private reportList!: ComplianceReportListComponent;

  /** Called by ComplianceReportConfigComponent when generation finishes. */
  onReportGenerated(): void {
    this.reportList?.loadReports();
  }
}
