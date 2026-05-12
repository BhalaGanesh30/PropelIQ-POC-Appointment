import { Route } from '@angular/router';
import { roleGuard } from '../../core/guards/role.guard';

export default [
  {
    path: 'users',
    loadComponent: () =>
      import('./pages/user-management/user-management.component').then(
        (m) => m.UserManagementComponent,
      ),
    title: 'User Management — PropelIQ',
  },
  {
    path: 'audit-logs',
    loadComponent: () =>
      import('./pages/audit-log/audit-log-viewer.component').then(
        (m) => m.AuditLogViewerComponent,
      ),
    title: 'Audit Log Viewer — PropelIQ',
    canActivate: [roleGuard],
    data: { roles: ['Admin'] },
  },
  {
    // US_057/AC-4: Per-patient access log viewer for Staff and Admin.
    path: 'access-logs',
    loadComponent: () =>
      import('./access-log/access-log-viewer.component').then(
        (m) => m.AccessLogViewerComponent,
      ),
    title: 'Access Log — PropelIQ',
    canActivate: [roleGuard],
    data: { roles: ['Staff', 'Admin'] },
  },
  {
    // US_057/AC-5: Staff disclosure review queue for Staff and Admin.
    path: 'disclosure-requests',
    loadComponent: () =>
      import('./disclosure/disclosure-review.component').then(
        (m) => m.DisclosureReviewComponent,
      ),
    title: 'Disclosure Request Review — PropelIQ',
    canActivate: [roleGuard],
    data: { roles: ['Staff', 'Admin'] },
  },
  {
    // US_058: SCR-022 Compliance Reports — HIPAA report generation, scheduling,
    // and distribution list management. Admin only.
    path: 'compliance-reports',
    loadComponent: () =>
      import('./compliance/compliance-reports.component').then(
        (m) => m.ComplianceReportsComponent,
      ),
    title: 'Compliance Reports — PropelIQ',
    canActivate: [roleGuard],
    data: { roles: ['Admin'] },
  },
  {
    // US_059: SCR-019 System Configuration — versioned config management with
    // OCC, history, and rollback. Admin only.
    path: 'config',
    loadComponent: () =>
      import('./config/system-config.component').then(
        (m) => m.SystemConfigComponent,
      ),
    title: 'System Configuration — PropelIQ',
    canActivate: [roleGuard],
    data: { roles: ['Admin'] },
  },
  {
    // US_060: SCR-023 KPI Dashboard — operational metrics, time-series charts,
    // PDF/PNG export, and scheduled distribution. Admin only.
    path: 'kpi',
    loadComponent: () =>
      import('./kpi/kpi-dashboard.component').then(
        (m) => m.KpiDashboardComponent,
      ),
    title: 'KPI Dashboard — PropelIQ',
    canActivate: [roleGuard],
    data: { roles: ['Admin'] },
  },
] satisfies Route[];
