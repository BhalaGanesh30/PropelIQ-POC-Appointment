import { Component, inject, input, OnInit, output, signal } from '@angular/core';
import { DatePipe } from '@angular/common';
import { MatButtonModule } from '@angular/material/button';
import { MatDividerModule } from '@angular/material/divider';
import { MatIconModule } from '@angular/material/icon';
import { MatProgressSpinnerModule } from '@angular/material/progress-spinner';
import { ActivityHistoryComponent } from './activity-history.component';
import { UserApiService } from './user-api.service';
import { UserDetail } from './models/user.models';

@Component({
  selector: 'app-user-detail-panel',
  standalone: true,
  imports: [
    DatePipe,
    MatButtonModule,
    MatDividerModule,
    MatIconModule,
    MatProgressSpinnerModule,
    ActivityHistoryComponent,
  ],
  templateUrl: './user-detail-panel.component.html',
  styleUrl: './user-detail-panel.component.scss',
})
export class UserDetailPanelComponent implements OnInit {
  readonly userId = input.required<string>();
  readonly closed = output<void>();

  private readonly api = inject(UserApiService);

  readonly user = signal<UserDetail | null>(null);
  readonly loading = signal(false);
  readonly loadError = signal(false);

  ngOnInit(): void {
    this.loading.set(true);
    this.loadError.set(false);
    this.api.getById(this.userId()).subscribe({
      next: (detail) => {
        this.user.set(detail);
        this.loading.set(false);
      },
      error: () => {
        this.loadError.set(true);
        this.loading.set(false);
      },
    });
  }

  statusLabel(isActive: boolean): string {
    return isActive ? 'Active' : 'Inactive';
  }
}
