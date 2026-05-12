import { ChangeDetectionStrategy, Component, signal, ViewChild } from '@angular/core';
import { MatExpansionModule } from '@angular/material/expansion';
import { MatIconModule } from '@angular/material/icon';
import { MatListModule } from '@angular/material/list';
import { MatButtonModule } from '@angular/material/button';
import { MatSidenavModule } from '@angular/material/sidenav';
import { ConfigCategoryComponent } from './config-category.component';
import { ConfigHistoryComponent } from './config-history.component';
import { CONFIG_CATEGORIES, ConfigCategory } from './models/config.models';

/**
 * SCR-019 System Configuration page container (US_059, AC-1–AC-4, edge case 1).
 *
 * Route: /admin/config
 * Guard: roleGuard [Admin]
 *
 * Layout:
 * - Desktop (≥ 768px): mat-sidenav sidebar listing four categories + main content area.
 * - Mobile  (< 768px): mat-accordion; one panel per category.
 *
 * Child components:
 * - ConfigCategoryComponent: reactive form for the selected category.
 * - ConfigHistoryComponent:  version history table, shown on demand.
 *
 * All 5 SCR-019 states are implemented inside ConfigCategoryComponent /
 * ConfigHistoryComponent (loading, default, empty, error, validation).
 */
@Component({
  selector: 'app-system-config',
  standalone: true,
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [
    MatButtonModule,
    MatExpansionModule,
    MatIconModule,
    MatListModule,
    MatSidenavModule,
    ConfigCategoryComponent,
    ConfigHistoryComponent,
  ],
  templateUrl: './system-config.component.html',
  styleUrl: './system-config.component.scss',
})
export class SystemConfigComponent {
  readonly categories        = CONFIG_CATEGORIES;
  readonly selectedCategory  = signal<ConfigCategory>('SlotTemplates');
  readonly showHistory       = signal(false);
  readonly historyCategory   = signal<ConfigCategory | null>(null);

  /** Reference used to reload the form after a restore action. */
  @ViewChild(ConfigCategoryComponent)
  private categoryForm?: ConfigCategoryComponent;

  selectCategory(category: ConfigCategory): void {
    this.selectedCategory.set(category);
    this.showHistory.set(false);
  }

  toggleHistory(category: ConfigCategory): void {
    const isOpen = this.showHistory() && this.historyCategory() === category;
    this.historyCategory.set(category);
    this.showHistory.set(!isOpen);
  }

  /** Called by ConfigHistoryComponent (restored output) to reload the active form. */
  onRestored(): void {
    this.categoryForm?.loadConfig();
  }
}
