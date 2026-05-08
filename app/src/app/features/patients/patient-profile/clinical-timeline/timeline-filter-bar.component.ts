import {
  ChangeDetectionStrategy,
  Component,
  OnInit,
  OnDestroy,
  inject,
  input,
  output,
} from '@angular/core';
import { FormBuilder, ReactiveFormsModule } from '@angular/forms';
import { MatChipsModule } from '@angular/material/chips';
import { MatDatepickerModule } from '@angular/material/datepicker';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatInputModule } from '@angular/material/input';
import { MatNativeDateModule } from '@angular/material/core';
import { MatIconModule } from '@angular/material/icon';
import { Subject, debounceTime, takeUntil } from 'rxjs';

import type { TimelineQueryParams } from '../../../../shared/models/timeline-event.model';

/** Category chip definition for the filter bar. */
interface CategoryChip {
  value: string | null; // null = "All"
  label: string;
  icon: string;
  ariaLabel: string;
}

const CATEGORY_CHIPS: CategoryChip[] = [
  { value: null,          label: 'All',         icon: 'list',             ariaLabel: 'Filter: All categories' },
  { value: 'medication',  label: 'Medications',  icon: 'medication',       ariaLabel: 'Filter by Medications'  },
  { value: 'diagnosis',   label: 'Diagnoses',    icon: 'local_hospital',   ariaLabel: 'Filter by Diagnoses'    },
  { value: 'allergy',     label: 'Allergies',    icon: 'warning_amber',    ariaLabel: 'Filter by Allergies'    },
  { value: 'document',    label: 'Documents',    icon: 'folder_open',      ariaLabel: 'Filter by Documents'    },
];

/**
 * Sticky filter bar for the clinical timeline (US_048 AC-2, AC-3).
 *
 * Emits `filterChange` with the merged params within 150 ms of any input change
 * (debounce prevents double API calls when both category and date change together).
 * Conforms to UXR-202 (keyboard navigation) and UXR-301/303 (responsive chips).
 */
@Component({
  selector: 'app-timeline-filter-bar',
  standalone: true,
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [
    ReactiveFormsModule,
    MatChipsModule,
    MatDatepickerModule,
    MatFormFieldModule,
    MatInputModule,
    MatNativeDateModule,
    MatIconModule,
  ],
  template: `
    <div class="filter-bar" role="search" aria-label="Timeline filters">

      <!-- Category chips (AC-2) -->
      <div class="filter-bar__chips" role="group" aria-label="Filter by category">
        @for (chip of chips; track chip.value) {
          <button
            class="filter-chip"
            type="button"
            [class.filter-chip--active]="selectedCategory === chip.value"
            [attr.aria-label]="chip.ariaLabel"
            [attr.aria-pressed]="selectedCategory === chip.value"
            (click)="selectCategory(chip.value)"
          >
            <mat-icon aria-hidden="true">{{ chip.icon }}</mat-icon>
            {{ chip.label }}
          </button>
        }
      </div>

      <!-- Date range picker (AC-3) -->
      <div class="filter-bar__date-range">
        <mat-form-field appearance="outline" class="date-field" subscriptSizing="dynamic">
          <mat-label>From</mat-label>
          <input
            matInput
            [matDatepicker]="pickerFrom"
            [formControl]="dateRange.controls.from"
            aria-label="Filter from date"
          />
          <mat-datepicker-toggle matIconSuffix [for]="pickerFrom" aria-label="Open from-date calendar" />
          <mat-datepicker #pickerFrom />
        </mat-form-field>

        <span class="date-separator" aria-hidden="true">–</span>

        <mat-form-field appearance="outline" class="date-field" subscriptSizing="dynamic">
          <mat-label>To</mat-label>
          <input
            matInput
            [matDatepicker]="pickerTo"
            [formControl]="dateRange.controls.to"
            aria-label="Filter to date"
          />
          <mat-datepicker-toggle matIconSuffix [for]="pickerTo" aria-label="Open to-date calendar" />
          <mat-datepicker #pickerTo />
        </mat-form-field>
      </div>
    </div>
  `,
  styles: [`
    :host { display: block; }

    .filter-bar {
      position: sticky;
      top: 0;
      z-index: 10;
      display: flex;
      flex-wrap: wrap;
      align-items: center;
      gap: 12px;
      padding: 10px 0 12px;
      background: #fafafa;
      border-bottom: 1px solid var(--color-neutral-200, #e0e0e0);
    }

    .filter-bar__chips {
      display: flex;
      gap: 8px;
      overflow-x: auto;
      -webkit-overflow-scrolling: touch;
      padding-bottom: 2px;
      scrollbar-width: none;
      &::-webkit-scrollbar { display: none; }
    }

    .filter-chip {
      display: inline-flex;
      align-items: center;
      gap: 4px;
      padding: 4px 12px;
      border: 1px solid var(--color-neutral-300, #bdbdbd);
      border-radius: 16px;
      background: #fff;
      font-size: 13px;
      font-weight: 500;
      color: var(--color-neutral-700, #616161);
      cursor: pointer;
      white-space: nowrap;
      transition: background 0.15s, color 0.15s, border-color 0.15s;

      mat-icon { font-size: 16px; width: 16px; height: 16px; }

      &:hover { background: var(--color-neutral-100, #f5f5f5); }

      &:focus-visible {
        outline: 2px solid #1976d2;
        outline-offset: 2px;
      }

      &.filter-chip--active {
        background: #1976d2;
        border-color: #1976d2;
        color: #fff;
      }
    }

    .filter-bar__date-range {
      display: flex;
      align-items: center;
      gap: 8px;
      flex-shrink: 0;
    }

    .date-field { width: 140px; }

    .date-separator {
      color: var(--color-neutral-400, #bdbdbd);
      font-weight: 600;
    }

    @media (max-width: 768px) {
      .filter-bar {
        flex-direction: column;
        align-items: flex-start;
      }
      .filter-bar__date-range { width: 100%; }
      .date-field { flex: 1; min-width: 0; }
    }
  `],
})
export class TimelineFilterBarComponent implements OnInit, OnDestroy {
  /** Initial filter state passed from parent (allows external reset). */
  readonly initialFilters = input<TimelineQueryParams>({});

  /** Emits merged filter params after debounce (AC-2 ≤500 ms; debounce 150 ms). */
  readonly filterChange = output<TimelineQueryParams>();

  protected readonly chips = CATEGORY_CHIPS;
  protected selectedCategory: string | null = null;

  private readonly fb = inject(FormBuilder);
  private readonly destroy$ = new Subject<void>();

  protected readonly dateRange = this.fb.nonNullable.group({
    from: this.fb.control<Date | null>(null),
    to:   this.fb.control<Date | null>(null),
  });

  ngOnInit(): void {
    const initial = this.initialFilters();
    this.selectedCategory = initial.category ?? null;

    // Subscribe to date range changes with debounce to avoid double API calls.
    this.dateRange.valueChanges
      .pipe(debounceTime(150), takeUntil(this.destroy$))
      .subscribe(() => this._emit());
  }

  ngOnDestroy(): void {
    this.destroy$.next();
    this.destroy$.complete();
  }

  protected selectCategory(value: string | null): void {
    this.selectedCategory = value;
    this._emit();
  }

  private _emit(): void {
    const { from, to } = this.dateRange.value;
    const params: TimelineQueryParams = {};
    if (this.selectedCategory) params.category = this.selectedCategory;
    if (from) params.dateFrom = from.toISOString().split('T')[0];
    if (to)   params.dateTo   = to.toISOString().split('T')[0];
    this.filterChange.emit(params);
  }
}
