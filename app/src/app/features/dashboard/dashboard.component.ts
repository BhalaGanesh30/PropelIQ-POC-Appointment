import { ChangeDetectionStrategy, Component, computed, inject } from '@angular/core';
import { RouterLink } from '@angular/router';
import { MatIconModule } from '@angular/material/icon';
import { TokenStorageService } from '../../core/services/token-storage.service';

interface QuickAction {
  readonly label: string;
  readonly description: string;
  readonly icon: string;
  readonly route: string;
  readonly colorStart: string;
  readonly colorEnd: string;
}

@Component({
  selector: 'app-dashboard',
  standalone: true,
  imports: [RouterLink, MatIconModule],
  template: `
    <div class="dashboard">
      <header class="dashboard-header">
        <h1 class="greeting">Welcome back, {{ firstName() }}!</h1>
        <p class="subtitle">Use the sidebar to navigate, or pick a quick action below.</p>
      </header>

      <section class="quick-actions" aria-label="Quick actions">
        @for (action of quickActions(); track action.route) {
          <a
            class="action-card"
            [routerLink]="action.route"
            [attr.aria-label]="action.label"
          >
            <div
              class="action-icon"
              [style.background]="'linear-gradient(135deg, ' + action.colorStart + ', ' + action.colorEnd + ')'"
              aria-hidden="true"
            >
              <mat-icon>{{ action.icon }}</mat-icon>
            </div>
            <div class="action-body">
              <span class="action-title">{{ action.label }}</span>
              <span class="action-description">{{ action.description }}</span>
            </div>
            <mat-icon class="action-chevron" aria-hidden="true">chevron_right</mat-icon>
          </a>
        }
      </section>
    </div>
  `,
  styles: [
    `
      .dashboard {
        max-width: 720px;
      }

      .dashboard-header {
        margin-bottom: 32px;
      }

      .greeting {
        font-size: 26px;
        font-weight: 700;
        color: var(--color-neutral-900, #111827);
        margin: 0 0 6px;
      }

      .subtitle {
        font-size: 14px;
        color: var(--color-neutral-500, #6b7280);
        margin: 0;
      }

      .quick-actions {
        display: flex;
        flex-direction: column;
        gap: 12px;
      }

      .action-card {
        display: flex;
        align-items: center;
        gap: 16px;
        padding: 18px 20px;
        border-radius: 12px;
        text-decoration: none;
        color: inherit;
        background: #fff;
        border: 1px solid var(--color-neutral-200, #e5e7eb);
        box-shadow: 0 1px 3px rgba(0, 0, 0, 0.06);
        transition:
          box-shadow 0.2s ease,
          transform 0.15s ease;

        &:hover {
          box-shadow: 0 6px 20px rgba(0, 0, 0, 0.1);
          transform: translateY(-2px);
        }

        &:focus-visible {
          outline: 2px solid var(--color-primary-500, #1976d2);
          outline-offset: 3px;
        }
      }

      .action-icon {
        width: 52px;
        height: 52px;
        border-radius: 12px;
        display: flex;
        align-items: center;
        justify-content: center;
        flex-shrink: 0;

        mat-icon {
          color: #fff;
          font-size: 24px;
          width: 24px;
          height: 24px;
        }
      }

      .action-body {
        flex: 1;
        display: flex;
        flex-direction: column;
        gap: 3px;
        min-width: 0;
      }

      .action-title {
        font-size: 15px;
        font-weight: 600;
        color: var(--color-neutral-900, #111827);
      }

      .action-description {
        font-size: 13px;
        color: var(--color-neutral-500, #6b7280);
      }

      .action-chevron {
        color: var(--color-neutral-400, #9ca3af);
        flex-shrink: 0;
      }
    `,
  ],
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class DashboardComponent {
  private readonly tokenStorage = inject(TokenStorageService);

  protected readonly firstName = computed(() => {
    const decoded = this.tokenStorage.getDecodedToken();
    return (decoded?.['given_name'] as string | undefined) || 'there';
  });

  /** Role-based quick actions derived from JWT role claim (US_015 AC-2). */
  protected readonly quickActions = computed<readonly QuickAction[]>(() => {
    const decoded = this.tokenStorage.getDecodedToken();
    const role = (decoded?.['role'] as string | undefined) || 'Patient';

    // Patient: personal appointment management
    if (role === 'Patient') {
      return [
        {
          label: 'Find an Appointment',
          description: 'Search available slots and book with a provider.',
          icon: 'calendar_today',
          route: '/scheduling/search',
          colorStart: '#1976d2',
          colorEnd: '#1565c0',
        },
        {
          label: 'My Appointments',
          description: 'View, reschedule, or cancel upcoming visits.',
          icon: 'event_note',
          route: '/appointments',
          colorStart: '#0288d1',
          colorEnd: '#01579b',
        },
        {
          label: 'My Waitlist',
          description: 'Check your position and claim earlier slots.',
          icon: 'queue',
          route: '/waitlist',
          colorStart: '#00897b',
          colorEnd: '#00695c',
        },
      ];
    }

    // Clinician: clinical intelligence features
    if (role === 'Clinician') {
      return [
        {
          label: 'Find a Patient',
          description: 'Look up patient profiles and clinical history.',
          icon: 'person_search',
          route: '/patients',
          colorStart: '#d32f2f',
          colorEnd: '#c62828',
        },
        {
          label: 'Coding Review',
          description: 'Review AI-suggested ICD-10 and CPT codes.',
          icon: 'assignment_returned',
          route: '/coding/search',
          colorStart: '#7b1fa2',
          colorEnd: '#6a1b9a',
        },
        {
          label: 'Document Library',
          description: 'Access and manage patient documents.',
          icon: 'library_books',
          route: '/documents/library',
          colorStart: '#0097a7',
          colorEnd: '#00838f',
        },
      ];
    }

    // Staff/Admin: operational features
    if (role === 'Staff' || role === 'Admin') {
      return [
        {
          label: 'Queue Dashboard',
          description: 'Monitor real-time queue and AI risk scores.',
          icon: 'dashboard',
          route: '/staff/queue',
          colorStart: '#f57c00',
          colorEnd: '#e65100',
        },
        {
          label: 'Daily Schedule',
          description: 'View and manage appointment calendar.',
          icon: 'event_repeat',
          route: '/staff/schedule',
          colorStart: '#1976d2',
          colorEnd: '#1565c0',
        },
        {
          label: 'Book for Patient',
          description: 'Staff-assisted booking workflow.',
          icon: 'add_event',
          route: '/staff/booking',
          colorStart: '#00897b',
          colorEnd: '#00695c',
        },
      ];
    }

    // Fallback (should not reach here)
    return [];
  });
}
