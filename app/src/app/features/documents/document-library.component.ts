import {
  ChangeDetectionStrategy,
  Component,
  DestroyRef,
  OnInit,
  computed,
  inject,
  signal,
} from '@angular/core';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { DatePipe } from '@angular/common';
import { FormsModule, ReactiveFormsModule, FormGroup, FormControl } from '@angular/forms';
import { RouterLink, ActivatedRoute } from '@angular/router';

import { MatButtonModule } from '@angular/material/button';
import { MatDatepickerModule } from '@angular/material/datepicker';
import {
  MAT_DIALOG_DATA,
  MatDialog,
  MatDialogModule,
  MatDialogRef,
} from '@angular/material/dialog';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatIconModule } from '@angular/material/icon';
import { MatInputModule } from '@angular/material/input';
import { MatMenuModule } from '@angular/material/menu';
import { MatPaginatorModule, PageEvent } from '@angular/material/paginator';
import { MatProgressSpinnerModule } from '@angular/material/progress-spinner';
import { MatSelectModule } from '@angular/material/select';
import { MatSnackBar, MatSnackBarModule } from '@angular/material/snack-bar';
import { MatTableModule } from '@angular/material/table';
import { MatTooltipModule } from '@angular/material/tooltip';

import { TokenStorageService } from '../../core/services/token-storage.service';
import {
  DocumentLibraryService,
} from './document-library.service';
import {
  DocumentCategory,
  DOCUMENT_CATEGORY_LABELS,
} from '../../shared/models/document-category.enum';
import type { DocumentListItem } from '../../shared/models/document-list-item.model';
import type { DocumentListFilter } from '../../shared/models/document-list-filter.model';

// ──────────────────────────────────────────────────────────────────────────────
// Rename dialog
// ──────────────────────────────────────────────────────────────────────────────

export interface RenameDialogData {
  currentName: string;
}

export interface RenameDialogResult {
  displayName: string;
}

/**
 * Rename dialog for DocumentLibraryComponent (AC-2, UXR-206).
 * Pre-fills with the current display name; validates non-empty input.
 * Focus is automatically returned to the trigger element on close by MatDialog.
 */
@Component({
  selector: 'app-rename-document-dialog',
  standalone: true,
  imports: [
    FormsModule,
    MatButtonModule,
    MatDialogModule,
    MatFormFieldModule,
    MatInputModule,
  ],
  template: `
    <h2 mat-dialog-title>Rename Document</h2>

    <mat-dialog-content>
      <mat-form-field appearance="outline" class="full-width">
        <mat-label>Display Name</mat-label>
        <input
          matInput
          [ngModel]="displayName()"
          (ngModelChange)="displayName.set($event)"
          maxlength="255"
          placeholder="Enter a display name"
          required
          cdkFocusInitial
          aria-label="New display name for the document"
        />
        <mat-hint>Original filename is preserved regardless of this name.</mat-hint>
      </mat-form-field>
    </mat-dialog-content>

    <mat-dialog-actions align="end">
      <button mat-button mat-dialog-close type="button">Cancel</button>
      <button
        mat-raised-button
        color="primary"
        type="button"
        [disabled]="!displayName().trim()"
        (click)="submit()"
      >
        Rename
      </button>
    </mat-dialog-actions>
  `,
  styles: `
    .full-width { width: 100%; }
    mat-dialog-content { padding-top: 8px !important; }
  `,
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class RenameDocumentDialogComponent {
  readonly data = inject<RenameDialogData>(MAT_DIALOG_DATA);
  readonly dialogRef = inject(MatDialogRef<RenameDocumentDialogComponent>);

  readonly displayName = signal(this.data.currentName);

  submit(): void {
    const trimmed = this.displayName().trim();
    if (!trimmed) return;
    const result: RenameDialogResult = { displayName: trimmed };
    this.dialogRef.close(result);
  }
}

// ──────────────────────────────────────────────────────────────────────────────
// Delete-confirm dialog
// ──────────────────────────────────────────────────────────────────────────────

export interface DeleteDialogData {
  displayName: string;
}

/**
 * Destructive-action confirmation dialog for soft-delete (AC-3, UXR-111).
 * Returns `true` when the user confirms deletion, closes without value on cancel.
 * Focus is trapped inside the dialog and returned to trigger on close (UXR-206).
 */
@Component({
  selector: 'app-delete-document-dialog',
  standalone: true,
  imports: [MatButtonModule, MatDialogModule, MatIconModule],
  template: `
    <h2 mat-dialog-title>
      <mat-icon color="warn" aria-hidden="true">warning</mat-icon>
      Delete Document
    </h2>

    <mat-dialog-content>
      <p>
        Are you sure you want to delete
        <strong>{{ data.displayName }}</strong>?
      </p>
      <p class="warning-text">
        This document will be moved to trash. You can restore it from the trash view.
      </p>
    </mat-dialog-content>

    <mat-dialog-actions align="end">
      <button mat-button mat-dialog-close type="button">Cancel</button>
      <button
        mat-raised-button
        color="warn"
        type="button"
        [mat-dialog-close]="true"
        cdkFocusInitial
      >
        Delete
      </button>
    </mat-dialog-actions>
  `,
  styles: `
    h2 mat-icon { margin-right: 8px; vertical-align: middle; }
    .warning-text {
      color: rgba(0, 0, 0, 0.54);
      font-size: 0.875rem;
      margin-top: 8px;
    }
  `,
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class DeleteDocumentDialogComponent {
  readonly data = inject<DeleteDialogData>(MAT_DIALOG_DATA);
}

// ──────────────────────────────────────────────────────────────────────────────
// Document Library component
// ──────────────────────────────────────────────────────────────────────────────

/** Category filter option for the dropdown. */
interface CategoryOption {
  value: DocumentCategory;
  label: string;
}

/** Page size for the document list. */
const PAGE_SIZE = 25;

/**
 * Document Library Screen (EP-006 US_043 SCR-012).
 *
 * AC-1: Category assignment via inline mat-select; saved immediately.
 * AC-2: Rename via dialog with optimistic UI update; reverts on error.
 * AC-3: Soft-delete via destructive confirm dialog; undo snackbar for 3 s.
 * AC-4: Admin-only trash toggle; shows soft-deleted docs with restore action.
 *
 * Edge Case 1: Categorization allowed even when extractionStatus ≠ Completed.
 * Edge Case 2: No hard-delete action exposed anywhere.
 *
 * UXR-111: Destructive confirm dialog before soft-delete.
 * UXR-201: WCAG AA contrast via design token colours.
 * UXR-202: Full keyboard navigation; mat-menu/mat-dialog keyboard-accessible.
 * UXR-206: Focus trapped in dialogs; MatDialog restores focus to trigger on close.
 * UXR-301: Responsive — table on desktop, card layout on mobile (375 px).
 */
@Component({
  selector: 'app-document-library',
  standalone: true,
  imports: [
    DatePipe,
    FormsModule,
    ReactiveFormsModule,
    RouterLink,
    MatButtonModule,
    MatDatepickerModule,
    MatDialogModule,
    MatFormFieldModule,
    MatIconModule,
    MatInputModule,
    MatMenuModule,
    MatPaginatorModule,
    MatProgressSpinnerModule,
    MatSelectModule,
    MatSnackBarModule,
    MatTableModule,
    MatTooltipModule,
  ],
  templateUrl: './document-library.component.html',
  styleUrls: ['./document-library.component.scss'],
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class DocumentLibraryComponent implements OnInit {
  private readonly route = inject(ActivatedRoute);
  private readonly libraryService = inject(DocumentLibraryService);
  private readonly dialog = inject(MatDialog);
  private readonly snackBar = inject(MatSnackBar);
  private readonly tokenStorage = inject(TokenStorageService);
  private readonly destroyRef = inject(DestroyRef);

  // ── State signals ──────────────────────────────────────────────────────────
  readonly documents = signal<DocumentListItem[]>([]);
  readonly totalCount = signal(0);
  readonly isLoading = signal(false);
  readonly loadError = signal<string | null>(null);
  readonly currentPage = signal(1);
  readonly pageSize = signal(PAGE_SIZE);
  readonly showTrash = signal(false);
  readonly editingCategoryId = signal<string | null>(null);

  // ── Filter state ───────────────────────────────────────────────────────────
  filterCategory: DocumentCategory | null = null;
  filterStatus: string | null = null;

  readonly dateRangeGroup = new FormGroup({
    start: new FormControl<Date | null>(null),
    end: new FormControl<Date | null>(null),
  });

  // ── Role / auth ────────────────────────────────────────────────────────────
  readonly isAdmin = computed(() => this.tokenStorage.getUserRole() === 'Admin');

  // ── Table columns — computed from trash toggle ────────────────────────────
  readonly displayedColumns = computed<string[]>(() =>
    this.showTrash()
      ? ['displayName', 'category', 'deletedAt', 'actions']
      : ['displayName', 'category', 'uploadedAt', 'extractionStatus', 'actions'],
  );

  // ── Category options ───────────────────────────────────────────────────────
  readonly categoryOptions: CategoryOption[] = Object.values(DocumentCategory).map(
    (v) => ({ value: v, label: DOCUMENT_CATEGORY_LABELS[v] }),
  );

  private patientId = 'current';

  ngOnInit(): void {
    // Initialize trash mode when navigated to /documents/trash (AC-4)
    const trashView = this.route.snapshot.data?.['trashView'] === true;
    if (trashView) {
      this.showTrash.set(true);
    }

    this.route.queryParamMap
      .pipe(takeUntilDestroyed(this.destroyRef))
      .subscribe((params) => {
        this.patientId = params.get('patientId') ?? 'current';
        this.loadDocuments();
      });
  }

  // ── Data loading ───────────────────────────────────────────────────────────

  loadDocuments(): void {
    this.isLoading.set(true);
    this.loadError.set(null);

    const filter: DocumentListFilter = {
      category: this.filterCategory,
      dateFrom: this.dateRangeStart
        ? this.formatDate(this.dateRangeStart)
        : null,
      dateTo: this.dateRangeEnd ? this.formatDate(this.dateRangeEnd) : null,
      status: this.filterStatus,
      includeDeleted: this.showTrash(),
    };

    this.libraryService
      .listDocuments(this.patientId, filter, this.currentPage(), this.pageSize())
      .pipe(takeUntilDestroyed(this.destroyRef))
      .subscribe({
        next: (response) => {
          this.documents.set(response.items);
          this.totalCount.set(response.totalCount);
          this.isLoading.set(false);
        },
        error: () => {
          this.loadError.set('Failed to load documents. Please try again.');
          this.isLoading.set(false);
        },
      });
  }

  // ── Filter handlers ────────────────────────────────────────────────────────

  onFilterChange(): void {
    this.currentPage.set(1);
    this.loadDocuments();
  }

  onDateRangeClose(): void {
    if (this.dateRangeStart || this.dateRangeEnd) {
      this.onFilterChange();
    }
  }

  clearDateRange(): void {
    this.dateRangeGroup.reset();
    this.onFilterChange();
  }

  // ── Trash toggle (AC-4, Admin only) ───────────────────────────────────────

  toggleTrash(): void {
    this.showTrash.update((v) => !v);
    this.currentPage.set(1);
    this.editingCategoryId.set(null);
    this.loadDocuments();
  }

  // ── Pagination ─────────────────────────────────────────────────────────────

  onPageChange(event: PageEvent): void {
    this.currentPage.set(event.pageIndex + 1);
    this.pageSize.set(event.pageSize);
    this.loadDocuments();
  }

  // ── Category inline edit (AC-1, Edge Case 1) ──────────────────────────────

  getCategoryLabel(category: DocumentCategory | null): string {
    return category ? (DOCUMENT_CATEGORY_LABELS[category] ?? category) : 'Uncategorized';
  }

  startCategoryEdit(documentId: string): void {
    this.editingCategoryId.set(documentId);
  }

  cancelCategoryEdit(): void {
    this.editingCategoryId.set(null);
  }

  onCategorySelected(row: DocumentListItem, category: DocumentCategory): void {
    if (row.category === category) {
      this.editingCategoryId.set(null);
      return;
    }

    const previousCategory = row.category;
    this.editingCategoryId.set(null);

    // Optimistic update
    this.documents.update((docs) =>
      docs.map((d) => (d.documentId === row.documentId ? { ...d, category } : d)),
    );

    this.libraryService.categorize(row.documentId, category).subscribe({
      error: () => {
        // Revert on failure
        this.documents.update((docs) =>
          docs.map((d) =>
            d.documentId === row.documentId
              ? { ...d, category: previousCategory }
              : d,
          ),
        );
        this.snackBar.open('Failed to update category. Please try again.', 'Dismiss', {
          duration: 4000,
        });
      },
    });
  }

  // ── Rename (AC-2) ─────────────────────────────────────────────────────────

  onRename(row: DocumentListItem): void {
    const dialogRef = this.dialog.open<
      RenameDocumentDialogComponent,
      RenameDialogData,
      RenameDialogResult
    >(RenameDocumentDialogComponent, {
      data: { currentName: row.displayName },
      width: '420px',
      autoFocus: true,
      restoreFocus: true, // UXR-206: return focus to trigger on close
    });

    dialogRef.afterClosed().subscribe((result) => {
      if (!result?.displayName) return;

      const previousName = row.displayName;

      // Optimistic update (AC-2)
      this.documents.update((docs) =>
        docs.map((d) =>
          d.documentId === row.documentId
            ? { ...d, displayName: result.displayName }
            : d,
        ),
      );

      this.libraryService.rename(row.documentId, result.displayName).subscribe({
        next: () => {
          this.snackBar.open(
            `Document renamed to "${result.displayName}"`,
            'Dismiss',
            { duration: 3000 },
          );
        },
        error: () => {
          // Revert on failure
          this.documents.update((docs) =>
            docs.map((d) =>
              d.documentId === row.documentId
                ? { ...d, displayName: previousName }
                : d,
            ),
          );
          this.snackBar.open(
            'Failed to rename document. Please try again.',
            'Dismiss',
            { duration: 4000 },
          );
        },
      });
    });
  }

  // ── Soft-delete (AC-3, UXR-111) ───────────────────────────────────────────

  onDelete(row: DocumentListItem): void {
    const dialogRef = this.dialog.open<
      DeleteDocumentDialogComponent,
      DeleteDialogData,
      boolean
    >(DeleteDocumentDialogComponent, {
      data: { displayName: row.displayName },
      autoFocus: false,
      restoreFocus: true, // UXR-206
    });

    dialogRef.afterClosed().subscribe((confirmed) => {
      if (!confirmed) return;

      this.libraryService.softDelete(row.documentId).subscribe({
        next: () => {
          // Remove from active list (AC-3)
          this.documents.update((docs) =>
            docs.filter((d) => d.documentId !== row.documentId),
          );
          this.totalCount.update((c) => Math.max(0, c - 1));

          // Undo snackbar — 3-second window
          const snackRef = this.snackBar.open(
            `"${row.displayName}" moved to trash`,
            'Undo',
            { duration: 3000 },
          );

          snackRef.onAction().subscribe(() => {
            this.libraryService.restore(row.documentId).subscribe({
              next: () => this.loadDocuments(),
              error: () => {
                this.snackBar.open(
                  'Failed to restore document.',
                  'Dismiss',
                  { duration: 4000 },
                );
              },
            });
          });
        },
        error: () => {
          this.snackBar.open(
            'Failed to delete document. Please try again.',
            'Dismiss',
            { duration: 4000 },
          );
        },
      });
    });
  }

  // ── Restore (AC-4) ────────────────────────────────────────────────────────

  onRestore(row: DocumentListItem): void {
    this.libraryService.restore(row.documentId).subscribe({
      next: () => {
        this.documents.update((docs) =>
          docs.filter((d) => d.documentId !== row.documentId),
        );
        this.totalCount.update((c) => Math.max(0, c - 1));
        this.snackBar.open(
          `"${row.displayName}" restored successfully`,
          'Dismiss',
          { duration: 3000 },
        );
      },
      error: () => {
        this.snackBar.open(
          'Failed to restore document. Please try again.',
          'Dismiss',
          { duration: 4000 },
        );
      },
    });
  }

  // ── Helpers ────────────────────────────────────────────────────────────────

  private get dateRangeStart(): Date | null {
    return this.dateRangeGroup.controls.start.value;
  }

  private get dateRangeEnd(): Date | null {
    return this.dateRangeGroup.controls.end.value;
  }

  private formatDate(date: Date): string {
    return date.toISOString().split('T')[0];
  }
}
