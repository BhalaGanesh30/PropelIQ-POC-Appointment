import {
  ChangeDetectionStrategy,
  Component,
  NgZone,
  OnDestroy,
  OnInit,
  computed,
  inject,
  signal,
} from '@angular/core';
import { DatePipe } from '@angular/common';
import { interval, Subject, switchMap, takeUntil } from 'rxjs';
import * as signalR from '@microsoft/signalr';
import { MatButtonModule } from '@angular/material/button';
import { MatCardModule } from '@angular/material/card';
import { MatIconModule } from '@angular/material/icon';
import { MatProgressSpinnerModule } from '@angular/material/progress-spinner';
import { MatTableModule } from '@angular/material/table';
import { MatTooltipModule } from '@angular/material/tooltip';

import { TokenStorageService } from '../../core/services/token-storage.service';
import { RiskScoreApiService } from './risk-score-api.service';
import { RiskBadgeComponent } from './risk-badge.component';
import { HighRiskAlertBannerComponent } from './high-risk-alert-banner.component';
import {
  AppointmentRiskScore,
  RiskFeature,
  RiskLevel,
} from './models/risk-score.models';

/** UXR-106: Data refresh interval in milliseconds (15 s). */
const POLL_INTERVAL_MS = 15_000;
/** Hours ahead that constitute the "upcoming" high-risk alert window. */
const HIGH_RISK_ALERT_WINDOW_HOURS = 24;

/**
 * Queue Dashboard (US_028 task_003, AC-1, AC-3).
 *
 * Displays all non-cancelled appointments for the next 7 days alongside
 * their AI no-show risk scores in a Material data table.  A top-of-page
 * alert banner lists any High-risk appointments within the next 24 hours.
 *
 * Real-time updates arrive via two channels:
 *   1. Polling — re-fetches the full score list every 15 s (UXR-106).
 *   2. Push — listens to the SignalR session hub for `HighRiskAlert` events
 *      emitted by the server's HighRiskNotificationWorker; triggers an
 *      immediate poll on receipt so staff see the data without waiting.
 *
 * ChangeDetectionStrategy.OnPush: all state changes go through signals so
 * Angular's scheduler picks them up without manual `markForCheck()` calls.
 */
@Component({
  selector: 'app-queue-dashboard',
  standalone: true,
  imports: [
    DatePipe,
    MatButtonModule,
    MatCardModule,
    MatIconModule,
    MatProgressSpinnerModule,
    MatTableModule,
    MatTooltipModule,
    HighRiskAlertBannerComponent,
    RiskBadgeComponent,
  ],
  templateUrl: './queue-dashboard.component.html',
  styleUrl: './queue-dashboard.component.scss',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class QueueDashboardComponent implements OnInit, OnDestroy {
  private readonly riskApi = inject(RiskScoreApiService);
  private readonly tokenStorage = inject(TokenStorageService);
  private readonly ngZone = inject(NgZone);

  private readonly destroy$ = new Subject<void>();
  /** Fires immediately and then every POLL_INTERVAL_MS milliseconds. */
  private readonly poll$ = new Subject<void>();

  private hubConnection: signalR.HubConnection | null = null;

  // ── State signals ──────────────────────────────────────────────────────────
  readonly appointments = signal<AppointmentRiskScore[]>([]);
  readonly isLoading = signal(true);
  readonly errorMessage = signal<string | null>(null);

  /** AC-3: High-risk items scheduled within the next HIGH_RISK_ALERT_WINDOW_HOURS hours. */
  readonly highRiskAlerts = computed(() => {
    const cutoff = Date.now() + HIGH_RISK_ALERT_WINDOW_HOURS * 60 * 60 * 1000;
    return this.appointments().filter(
      (a) =>
        a.riskLevel === 'High' && new Date(a.appointmentDate).getTime() <= cutoff,
    );
  });

  // ── Table columns ──────────────────────────────────────────────────────────
  readonly displayedColumns = [
    'patientName',
    'appointmentDate',
    'appointmentType',
    'status',
    'risk',
  ] as const;

  ngOnInit(): void {
    this.startPolling();
    this.startSignalRConnection();
  }

  ngOnDestroy(): void {
    this.destroy$.next();
    this.destroy$.complete();
    this.stopSignalRConnection();
  }

  // ── Helpers consumed in the template ──────────────────────────────────────

  getRiskLevel(appointmentId: string): RiskLevel {
    return (
      this.appointments().find((a) => a.appointmentId === appointmentId)
        ?.riskLevel ?? 'Unknown'
    );
  }

  getRiskFeatures(appointmentId: string): RiskFeature[] {
    return (
      this.appointments().find((a) => a.appointmentId === appointmentId)
        ?.features ?? []
    );
  }

  /** Manually re-trigger a poll (e.g. from a "Refresh" button). */
  refresh(): void {
    this.poll$.next();
  }

  // ── Private ────────────────────────────────────────────────────────────────

  private startPolling(): void {
    // Emit once immediately, then every POLL_INTERVAL_MS milliseconds.
    // switchMap cancels an in-flight request if the interval fires again,
    // preventing race conditions (UXR-106 edge case).
    this.poll$
      .pipe(
        switchMap(() => {
          const { from, to } = this.buildDateWindow();
          return this.riskApi.getRiskScores(from, to);
        }),
        takeUntil(this.destroy$),
      )
      .subscribe({
        next: (data) => {
          this.appointments.set(data);
          this.isLoading.set(false);
          this.errorMessage.set(null);
        },
        error: () => {
          this.isLoading.set(false);
          this.errorMessage.set('Failed to load risk scores. Retrying…');
        },
      });

    // Periodic trigger every 15 s (UXR-106).
    interval(POLL_INTERVAL_MS)
      .pipe(takeUntil(this.destroy$))
      .subscribe(() => this.poll$.next());

    // Initial load.
    this.poll$.next();
  }

  /**
   * Open a SignalR connection to /hubs/session and listen for `HighRiskAlert`
   * push events from the server's HighRiskNotificationWorker.
   * On receipt: trigger an immediate poll so the table stays current.
   */
  private startSignalRConnection(): void {
    const token = this.tokenStorage.getAccessToken();
    if (!token) return;

    this.hubConnection = new signalR.HubConnectionBuilder()
      .withUrl('/hubs/session', {
        accessTokenFactory: () => this.tokenStorage.getAccessToken() ?? '',
      })
      .withAutomaticReconnect([0, 2000, 5000, 10000, 30000])
      .build();

    this.hubConnection.on('HighRiskAlert', () => {
      // Run back in Angular zone so signal writes trigger CD.
      this.ngZone.run(() => this.poll$.next());
    });

    this.hubConnection
      .start()
      .catch((err: unknown) =>
        console.warn('[QueueDashboard] SignalR connection failed:', err),
      );
  }

  private stopSignalRConnection(): void {
    this.hubConnection?.stop().catch(() => {});
    this.hubConnection = null;
  }

  /** Build a UTC date window: now → now + 7 days. */
  private buildDateWindow(): { from: string; to: string } {
    const now = new Date();
    const to = new Date(now);
    to.setDate(to.getDate() + 7);
    return {
      from: now.toISOString(),
      to: to.toISOString(),
    };
  }
}
