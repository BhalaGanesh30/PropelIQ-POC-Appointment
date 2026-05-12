import {
  ChangeDetectionStrategy,
  Component,
  computed,
  inject,
  OnInit,
  signal,
} from '@angular/core';
import { FormsModule } from '@angular/forms';
import { MatButtonModule } from '@angular/material/button';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatIconModule } from '@angular/material/icon';
import { MatInputModule } from '@angular/material/input';
import { MatProgressSpinnerModule } from '@angular/material/progress-spinner';
import { MatSelectModule } from '@angular/material/select';
import { MatSnackBar, MatSnackBarModule } from '@angular/material/snack-bar';
import { MatTabsModule } from '@angular/material/tabs';
import { MatTooltipModule } from '@angular/material/tooltip';
import { CodeEditorComponent } from './code-editor.component';
import { SmsCounterComponent } from './sms-counter.component';
import { TemplateApiService } from './template-api.service';
import { TemplatePreviewComponent } from './template-preview.component';
import { VersionHistorySidebarComponent } from './version-history-sidebar.component';
import { TemplateDetail, TemplateListItem, TemplateValidationResult } from './models/template.models';

/**
 * SCR-024 Template Editor page container (US_062, AC-1–AC-4, edge cases 1–2).
 *
 * Route: /admin/templates
 * Guard: roleGuard [Admin]
 *
 * Layout:
 * - Desktop (≥ 768px): split-view — code editor left, live preview right;
 *   version history collapsible sidebar on far right.
 * - Mobile (< 768px): `mat-tab-group` switches between Editor and Preview tabs;
 *   version history shown in a full-width block below.
 *
 * SCR-024 states:
 * - Default     : split editor / preview with template selector and version history.
 * - Loading     : skeleton panes during template fetch / save in progress.
 * - Empty       : "Select a template" message before any selection.
 * - Error       : persistent toast on save failure; inline validation banner.
 * - Validation  : version saved toast (5 s auto-dismiss); preview updates on edit.
 */
@Component({
  selector: 'app-template-editor',
  standalone: true,
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [
    FormsModule,
    MatButtonModule,
    MatFormFieldModule,
    MatIconModule,
    MatInputModule,
    MatProgressSpinnerModule,
    MatSelectModule,
    MatSnackBarModule,
    MatTabsModule,
    MatTooltipModule,
    CodeEditorComponent,
    SmsCounterComponent,
    TemplatePreviewComponent,
    VersionHistorySidebarComponent,
  ],
  templateUrl: './template-editor.component.html',
  styleUrl: './template-editor.component.scss',
})
export class TemplateEditorComponent implements OnInit {
  private readonly api = inject(TemplateApiService);
  private readonly snackBar = inject(MatSnackBar);

  /** Full template list for the selector dropdown. */
  readonly templates = signal<TemplateListItem[]>([]);
  /** Currently loaded template detail (includes current version content). */
  readonly selectedTemplate = signal<TemplateDetail | null>(null);
  /** Live content bound to the code editor textarea. */
  readonly editorContent = signal('');
  /** Subject line — relevant for HTML templates only. */
  readonly editorSubject = signal('');
  /** Rendered HTML/plain text returned by the preview endpoint (AC-2). */
  readonly previewHtml = signal('');

  readonly loadingTemplates = signal(false);
  readonly loadingDetail = signal(false);
  readonly saving = signal(false);
  readonly loadError = signal(false);

  /** Null when no validation errors; set on 422 responses (AC-4). */
  readonly validationErrors = signal<TemplateValidationResult | null>(null);
  readonly showVersionHistory = signal(true);

  readonly isSms = computed(() => this.selectedTemplate()?.type === 'SMS');

  readonly validationErrorId = 'template-validation-errors';

  ngOnInit(): void {
    this.loadTemplates();
  }

  // ── Template list ──────────────────────────────────────────────────────────

  loadTemplates(): void {
    this.loadingTemplates.set(true);
    this.loadError.set(false);
    this.api.list().subscribe({
      next: (result) => {
        this.templates.set(result.items);
        this.loadingTemplates.set(false);
      },
      error: () => {
        this.loadingTemplates.set(false);
        this.loadError.set(true);
        this.snackBar.open('Failed to load templates. Please retry.', 'Retry', {
          duration: 0,
        });
      },
    });
  }

  // ── Template selection ─────────────────────────────────────────────────────

  onTemplateSelect(templateId: string): void {
    this.loadingDetail.set(true);
    this.validationErrors.set(null);
    this.api.getById(templateId).subscribe({
      next: (detail) => {
        this.selectedTemplate.set(detail);
        this.editorContent.set(detail.currentVersion.content);
        this.editorSubject.set(detail.currentVersion.subject ?? '');
        this.loadingDetail.set(false);
        this.refreshPreview();
      },
      error: () => {
        this.loadingDetail.set(false);
        this.snackBar.open('Failed to load template detail.', 'Dismiss', { duration: 5000 });
      },
    });
  }

  // ── Content editing ────────────────────────────────────────────────────────

  onContentChange(content: string): void {
    this.editorContent.set(content);
    this.validationErrors.set(null);
    this.refreshPreview();
  }

  refreshPreview(): void {
    const template = this.selectedTemplate();
    if (!template) return;
    this.api
      .preview(template.id, this.editorContent(), this.editorSubject() || undefined)
      .subscribe({
        next: (resp) => this.previewHtml.set(resp.renderedHtml),
        error: () => {}, // preview failures are non-blocking
      });
  }

  // ── Save (AC-1, AC-4) ──────────────────────────────────────────────────────

  save(): void {
    const template = this.selectedTemplate();
    if (!template) return;

    this.saving.set(true);
    this.validationErrors.set(null);

    this.api
      .save(template.id, {
        content: this.editorContent(),
        subject: this.editorSubject() || undefined,
      })
      .subscribe({
        next: (version) => {
          this.saving.set(false);
          // UXR-502: success toast auto-dismisses at 5 s.
          this.snackBar.open(`Version ${version.versionNumber} saved successfully.`, 'Dismiss', {
            duration: 5000,
          });
          // Reload template detail to reflect new active version.
          this.onTemplateSelect(template.id);
        },
        error: (err) => {
          this.saving.set(false);
          if (err.status === 422) {
            // AC-4: surface validation errors inline.
            this.validationErrors.set(err.error);
            // UXR-502: error toast persists until dismissed.
            this.snackBar.open(
              'Template contains invalid merge-field placeholders. See errors below.',
              'Dismiss',
              { duration: 0 },
            );
          } else {
            this.snackBar.open('Save failed. Please try again.', 'Retry', { duration: 0 });
          }
        },
      });
  }

  // ── Version history ────────────────────────────────────────────────────────

  toggleVersionHistory(): void {
    this.showVersionHistory.update((v) => !v);
  }

  onVersionRestored(): void {
    const template = this.selectedTemplate();
    if (template) {
      this.onTemplateSelect(template.id);
    }
  }
}
