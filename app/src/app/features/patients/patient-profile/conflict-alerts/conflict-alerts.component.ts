import {
  ChangeDetectionStrategy,
  Component,
  OnInit,
  effect,
  inject,
  input,
} from '@angular/core';
import { MatButtonModule } from '@angular/material/button';
import { MatDialog } from '@angular/material/dialog';
import { MatExpansionModule } from '@angular/material/expansion';
import { MatIconModule } from '@angular/material/icon';

import { ConflictAlertsFacade } from '../../conflict-alerts.facade';
import { ConflictAlertCardComponent } from './conflict-alert-card.component';
import { ConflictAlertsSkeletonComponent } from './conflict-alerts-skeleton.component';
import { ConflictEmptyStateComponent } from './conflict-empty-state.component';
import { RulesStaleWarningComponent } from './rules-stale-warning.component';
import {
  ConflictAcknowledgeDialogComponent,
  type ConflictAcknowledgeDialogData,
  type ConflictAcknowledgeDialogResult,
} from './conflict-acknowledge-dialog.component';

/**
 * Conflict alerts list component (SCR-016).
 *
 * Responsibilities:
 * - Loads data on init via facade (lazy per-tab loading).
 * - Renders alerts sorted Critical → High → Moderate → Low (AC-1).
 * - Opens mandatory acknowledgment dialog for each pending Critical alert on load (AC-3).
 * - Delegates all state to ConflictAlertsFacade (provided by ProfileConflictsTabComponent).
 * - Emits aria-live announcements for screen readers (UXR-203).
 */
@Component({
  selector: 'app-conflict-alerts',
  standalone: true,
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [
    MatButtonModule,
    MatExpansionModule,
    MatIconModule,
    ConflictAlertCardComponent,
    ConflictAlertsSkeletonComponent,
    ConflictEmptyStateComponent,
    RulesStaleWarningComponent,
  ],
  templateUrl: './conflict-alerts.component.html',
  styleUrl: './conflict-alerts.component.scss',
})
export class ConflictAlertsComponent implements OnInit {
  readonly patientId = input.required<string>();

  protected readonly facade = inject(ConflictAlertsFacade);
  private readonly dialog = inject(MatDialog);

  /** Guards against opening the critical dialog more than once per load. */
  private criticalDialogsOpened = false;

  constructor() {
    // React when data finishes loading to open mandatory Critical dialogs (AC-3).
    effect(() => {
      const loaded = this.facade.loaded();
      const pending = this.facade.pendingCritical();
      if (loaded && pending.length > 0 && !this.criticalDialogsOpened) {
        this.criticalDialogsOpened = true;
        this._openCriticalDialogsInSequence(0);
      }
    });
  }

  ngOnInit(): void {
    this.facade.loadConflicts(this.patientId());
  }

  protected onAcknowledge(conflictId: string): void {
    this.facade.acknowledge(conflictId);
  }

  protected retry(): void {
    this.criticalDialogsOpened = false;
    this.facade.reload(this.patientId());
  }

  /**
   * Opens acknowledgment dialogs for pending Critical conflicts in sequence.
   * Each dialog must be resolved before the next one opens.
   * disableClose=true prevents the clinician from dismissing without confirming (AC-3).
   */
  private _openCriticalDialogsInSequence(index: number): void {
    const pending = this.facade.pendingCritical();
    if (index >= pending.length) return;

    const alert = pending[index];
    const data: ConflictAcknowledgeDialogData = {
      conflictId: alert.conflictId,
      severity: alert.severity,
      description: alert.description,
      drugA: alert.drugA,
      drugB: alert.drugB,
    };

    const ref = this.dialog.open<
      ConflictAcknowledgeDialogComponent,
      ConflictAcknowledgeDialogData,
      ConflictAcknowledgeDialogResult
    >(ConflictAcknowledgeDialogComponent, {
      data,
      width: '480px',
      disableClose: true,
      autoFocus: 'dialog',
      restoreFocus: true,
    });

    ref.afterClosed().subscribe((confirmed) => {
      if (confirmed) {
        this.facade.acknowledge(alert.conflictId);
      }
      this._openCriticalDialogsInSequence(index + 1);
    });
  }
}
