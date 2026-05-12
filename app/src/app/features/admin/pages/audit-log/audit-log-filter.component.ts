import { Component, output, signal, ChangeDetectionStrategy } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatSelectModule } from '@angular/material/select';
import { MatInputModule } from '@angular/material/input';
import { MatDatepickerModule } from '@angular/material/datepicker';
import { MatNativeDateModule } from '@angular/material/core';
import { MatChipsModule } from '@angular/material/chips';
import { MatButtonModule } from '@angular/material/button';
import { MatIconModule } from '@angular/material/icon';
import { ActiveFilter, AuditLogQueryParams, EVENT_TYPES } from './models/audit-log.models';

/**
 * Filter bar for the Audit Log Viewer (SCR-021, US_056 task_003).
 *
 * Controls: event type (select), actor user ID (text), date range (two date pickers),
 * resource/entity ID (text). Applied filters render as removable chips above the table
 * (SCR-021 Validation state).
 *
 * UXR-201: All form labels provide sufficient contrast context.
 * UXR-202: All inputs are keyboard-navigable via Angular Material.
 */
@Component({
  selector: 'app-audit-log-filter',
  standalone: true,
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [
    FormsModule,
    MatFormFieldModule,
    MatSelectModule,
    MatInputModule,
    MatDatepickerModule,
    MatNativeDateModule,
    MatChipsModule,
    MatButtonModule,
    MatIconModule,
  ],
  templateUrl: './audit-log-filter.component.html',
  styleUrl: './audit-log-filter.component.scss',
})
export class AuditLogFilterComponent {
  /** Emits whenever the user applies or clears filters. */
  readonly filtersChanged = output<AuditLogQueryParams>();

  readonly eventTypes = EVENT_TYPES;

  // ── Form field signals ────────────────────────────────────────────────────
  readonly selectedEventType = signal<string>('');
  readonly actorUserId       = signal<string>('');
  readonly fromDate          = signal<Date | null>(null);
  readonly toDate            = signal<Date | null>(null);
  readonly resourceId        = signal<string>('');

  /** SCR-021 Validation state: active filter chips. */
  readonly activeFilters = signal<ActiveFilter[]>([]);

  /** Build params + chip list, then emit. */
  applyFilters(): void {
    const chips: ActiveFilter[] = [];
    const params: AuditLogQueryParams = { page: 0, pageSize: 25 };

    if (this.selectedEventType()) {
      params.eventType = this.selectedEventType();
      chips.push({ key: 'eventType', label: `Type: ${this.selectedEventType()}` });
    }
    if (this.actorUserId()) {
      params.actorUserId = this.actorUserId();
      chips.push({ key: 'actorUserId', label: `Actor: ${this.actorUserId()}` });
    }
    if (this.fromDate()) {
      params.from = this.fromDate()!.toISOString();
      chips.push({ key: 'from', label: `From: ${this.fromDate()!.toLocaleDateString()}` });
    }
    if (this.toDate()) {
      params.to = this.toDate()!.toISOString();
      chips.push({ key: 'to', label: `To: ${this.toDate()!.toLocaleDateString()}` });
    }
    if (this.resourceId()) {
      params.entityId = this.resourceId();
      chips.push({ key: 'entityId', label: `Resource: ${this.resourceId()}` });
    }

    this.activeFilters.set(chips);
    this.filtersChanged.emit(params);
  }

  /** Remove a single active filter chip and re-query. */
  removeFilter(key: ActiveFilter['key']): void {
    switch (key) {
      case 'eventType':  this.selectedEventType.set(''); break;
      case 'actorUserId': this.actorUserId.set('');       break;
      case 'from':       this.fromDate.set(null);          break;
      case 'to':         this.toDate.set(null);            break;
      case 'entityId':   this.resourceId.set('');          break;
    }
    this.applyFilters();
  }

  /** Reset all filters. */
  clearAll(): void {
    this.selectedEventType.set('');
    this.actorUserId.set('');
    this.fromDate.set(null);
    this.toDate.set(null);
    this.resourceId.set('');
    this.activeFilters.set([]);
    this.filtersChanged.emit({ page: 0, pageSize: 25 });
  }
}
