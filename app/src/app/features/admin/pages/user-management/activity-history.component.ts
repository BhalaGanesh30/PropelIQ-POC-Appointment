import { Component, inject, input, OnInit, signal } from '@angular/core';
import { MatListModule } from '@angular/material/list';
import { MatIconModule } from '@angular/material/icon';
import { MatButtonModule } from '@angular/material/button';
import { MatProgressSpinnerModule } from '@angular/material/progress-spinner';
import { DatePipe } from '@angular/common';
import { UserApiService } from './user-api.service';
import { UserActivityEntry } from './models/user.models';

@Component({
  selector: 'app-activity-history',
  standalone: true,
  imports: [MatListModule, MatIconModule, MatButtonModule, MatProgressSpinnerModule, DatePipe],
  templateUrl: './activity-history.component.html',
})
export class ActivityHistoryComponent implements OnInit {
  readonly userId = input.required<string>();

  private readonly api = inject(UserApiService);

  readonly entries = signal<UserActivityEntry[]>([]);
  readonly loading = signal(false);
  readonly hasMore = signal(true);

  private page = 1;
  private readonly pageSize = 25;

  ngOnInit(): void {
    this.loadHistory();
  }

  loadHistory(): void {
    this.loading.set(true);
    this.api.getActivityHistory(this.userId(), this.page, this.pageSize).subscribe({
      next: (data) => {
        this.entries.update((existing) => [...existing, ...data]);
        this.hasMore.set(data.length === this.pageSize);
        this.loading.set(false);
      },
      error: () => this.loading.set(false),
    });
  }

  loadMore(): void {
    this.page += 1;
    this.loadHistory();
  }

  getEventIcon(eventType: string): string {
    switch (eventType) {
      case 'Login':
        return 'login';
      case 'RoleChange':
        return 'swap_horiz';
      case 'StatusChange':
        return 'toggle_on';
      default:
        return 'history';
    }
  }
}
