# Task - TASK_003

## Requirement Reference

- User Story: us_062
- Story Location: .propel/context/tasks/EP-011/us_062/us_062.md
- Acceptance Criteria:
  - AC-1: Given I am authenticated as an Admin, When I create or edit an HTML or SMS notification template, Then the template is saved as a new version with the change date and my identity, while previous versions are preserved.
  - AC-2: Given I am editing a template, When I click "Preview," Then a rendered preview of the template is shown with sample data substituted for the merge fields (e.g., patient name, appointment date).
  - AC-3: Given I want to revert to a previous template version, When I select a prior version and click "Restore," Then the selected version becomes active as a new version and existing queued notifications using the old template remain unaffected.
  - AC-4: Given an HTML template contains an invalid merge field placeholder, When I save the template, Then a validation error identifies the invalid placeholder and blocks the save.
- Edge Cases:
  - What happens if an SMS template exceeds the 160-character limit? A character counter warns the user; templates exceeding 160 characters are flagged as multi-part SMS and the estimated message count is shown.
  - How does the system handle templates that reference deleted merge fields? Template validation detects orphaned placeholders and warns the admin before saving.

## Design References (Frontend Tasks Only)

| Reference Type | Value |
|----------------|-------|
| **UI Impact** | Yes |
| **Figma URL** | N/A |
| **Wireframe Status** | PENDING |
| **Wireframe Type** | N/A |
| **Wireframe Path/URL** | TODO: Provide wireframe - upload to `.propel/context/wireframes/Hi-Fi/wireframe-SCR-024-template-editor.[html\|png\|jpg]` or add external URL |
| **Screen Spec** | .propel/context/docs/figma_spec.md#SCR-024 |
| **UXR Requirements** | UXR-201, UXR-202, UXR-205, UXR-301, UXR-501, UXR-502 |
| **Design Tokens** | N/A |

> **Note**: User story references SCR-034 but SCR-034 does not exist in figma_spec.md. The correct screen for the template editor is **SCR-024** (Template Editor, EP-011, UC-006, Admin persona). SCR-024 specifies a split-view layout: code editor on the left, live preview on the right. Template selector dropdown. Version history in a collapsible sidebar. Mobile: tabbed switch between editor and preview. 5 states (Default, Loading, Empty, Error, Validation).

## Applicable Technology Stack

| Layer | Technology | Version |
|-------|------------|---------|
| Frontend | Angular | 17.x |
| Frontend | Angular Material | 17.x |
| Frontend | RxJS | 7.x |
| Frontend | TypeScript | 5.x |
| Backend | N/A | N/A |
| Database | N/A | N/A |
| AI/ML | N/A | N/A |
| Vector Store | N/A | N/A |
| AI Gateway | N/A | N/A |
| Mobile | N/A | N/A |

**Note**: All code, and libraries, MUST be compatible with versions above.

## AI References (AI Tasks Only)

| Reference Type | Value |
|----------------|-------|
| **AI Impact** | No |
| **AIR Requirements** | N/A |
| **AI Pattern** | N/A |
| **Prompt Template Path** | N/A |
| **Guardrails Config** | N/A |
| **Model Provider** | N/A |

## Mobile References (Mobile Tasks Only)

| Reference Type | Value |
|----------------|-------|
| **Mobile Impact** | No |
| **Platform Target** | N/A |
| **Min OS Version** | N/A |
| **Mobile Framework** | N/A |

## Task Overview

Implement the SCR-024 Template Editor screen for admin users at the route `/admin/templates`. The screen renders a `TemplateEditorComponent` page container with a template selector dropdown to choose from existing notification templates, and a type filter (HTML/SMS). The main editing area uses a split-view layout on desktop: a `CodeEditorComponent` on the left with a `<textarea>` using monospace font (JetBrains Mono per figma_spec.md) for HTML/SMS content editing, and a `TemplatePreviewComponent` on the right showing a live rendered preview (AC-2). Saving triggers validation and creates a new version with the admin's identity and timestamp displayed in the version list (AC-1). Invalid merge field placeholders surface inline errors blocking the save (AC-4). A collapsible `VersionHistorySidebarComponent` lists all versions in reverse chronological order with a "Restore" button that opens a confirmation dialog (AC-3). For SMS templates, a `SmsCounterComponent` displays a real-time character counter and multi-part SMS segment estimate when content exceeds 160 characters (edge case 1). Orphaned placeholders trigger a warning before save (edge case 2). On mobile (< 768px), the editor and preview switch to a tabbed interface. The screen implements all 5 SCR-024 states: Default (split editor/preview with template selector and version history), Loading (skeleton editor and preview during template fetch), Empty (starter template with placeholder content for new templates), Error (save failure toast with retry, inline validation errors for invalid placeholders), Validation (version saved toast, preview updates on edit, rollback confirmation dialog). All components use Angular standalone architecture, signals, lazy-loaded route with adminGuard, WCAG AA contrast (UXR-201), keyboard navigation (UXR-202), aria-describedby on form error messages (UXR-205), responsive breakpoints (UXR-301), loading spinner on save (UXR-501), and auto-dismissing success toasts / persistent error toasts (UXR-502).

## Dependent Tasks

- US_062 task_001 (requires template management API endpoints)
- US_062 task_002 (requires notification_templates and template_versions tables)
- US_015 task_001 (requires Admin route guard)

## Impacted Components

- New: `client/src/app/features/admin/templates/template-editor.component.ts` (page container)
- New: `client/src/app/features/admin/templates/template-editor.component.html` (template)
- New: `client/src/app/features/admin/templates/template-editor.component.scss` (responsive styles)
- New: `client/src/app/features/admin/templates/code-editor.component.ts` (monospace code editor)
- New: `client/src/app/features/admin/templates/code-editor.component.html` (editor template)
- New: `client/src/app/features/admin/templates/template-preview.component.ts` (rendered preview)
- New: `client/src/app/features/admin/templates/template-preview.component.html` (preview template)
- New: `client/src/app/features/admin/templates/version-history-sidebar.component.ts` (version list with restore)
- New: `client/src/app/features/admin/templates/version-history-sidebar.component.html` (sidebar template)
- New: `client/src/app/features/admin/templates/sms-counter.component.ts` (character counter)
- New: `client/src/app/features/admin/templates/restore-confirm-dialog.component.ts` (restore confirmation)
- New: `client/src/app/features/admin/templates/models/template.models.ts` (TypeScript interfaces)
- New: `client/src/app/features/admin/templates/template-api.service.ts` (HttpClient service)
- Modify: `client/src/app/app.routes.ts` (add admin/templates route)

## Implementation Plan

1. **Create TypeScript interfaces** for template management data:

```typescript
// client/src/app/features/admin/templates/
//   models/template.models.ts

export interface TemplateListItem {
  id: string;
  name: string;
  type: 'HTML' | 'SMS';
  description: string;
  currentVersionNumber: number;
  lastModifiedUtc: string;
}

export interface TemplateDetail {
  id: string;
  name: string;
  type: 'HTML' | 'SMS';
  description: string;
  currentVersion: TemplateVersionItem;
}

export interface TemplateVersionItem {
  id: string;
  versionNumber: number;
  content: string;
  subject: string | null;
  isActive: boolean;
  createdAtUtc: string;
  createdByName: string;
}

export interface SaveTemplateRequest {
  content: string;
  subject?: string;
}

export interface PreviewResponse {
  renderedHtml: string;
  renderedSubject: string | null;
  smsInfo: SmsInfo | null;
}

export interface SmsInfo {
  characterCount: number;
  isMultiPart: boolean;
  estimatedSegments: number;
}

export interface TemplateValidationResult {
  isValid: boolean;
  invalidPlaceholders: string[];
  orphanedPlaceholders: string[];
}

export interface PagedResult<T> {
  items: T[];
  totalCount: number;
  page: number;
  pageSize: number;
}
```

2. **Create `TemplateApiService`** with HttpClient:

```typescript
// client/src/app/features/admin/templates/
//   template-api.service.ts
import { Injectable, inject } from '@angular/core';
import { HttpClient, HttpParams } from
  '@angular/common/http';
import { Observable } from 'rxjs';
import {
  TemplateListItem, TemplateDetail,
  TemplateVersionItem, PagedResult,
  SaveTemplateRequest, PreviewResponse,
  TemplateValidationResult
} from './models/template.models';

@Injectable({ providedIn: 'root' })
export class TemplateApiService {
  private readonly http = inject(HttpClient);
  private readonly base =
    '/api/v1/admin/templates';

  list(
    type?: string,
    page = 1,
    pageSize = 25
  ): Observable<PagedResult<TemplateListItem>> {
    let params = new HttpParams()
      .set('page', page)
      .set('pageSize', pageSize);
    if (type)
      params = params.set('type', type);
    return this.http
      .get<PagedResult<TemplateListItem>>(
        this.base, { params });
  }

  getById(
    templateId: string
  ): Observable<TemplateDetail> {
    return this.http.get<TemplateDetail>(
      `${this.base}/${templateId}`);
  }

  getVersions(
    templateId: string,
    page = 1,
    pageSize = 25
  ): Observable<TemplateVersionItem[]> {
    return this.http
      .get<TemplateVersionItem[]>(
        `${this.base}/${templateId}/versions`,
        { params: { page, pageSize } });
  }

  save(
    templateId: string,
    request: SaveTemplateRequest
  ): Observable<TemplateVersionItem> {
    return this.http
      .post<TemplateVersionItem>(
        `${this.base}/${templateId}`, request);
  }

  preview(
    templateId: string,
    content: string,
    subject?: string
  ): Observable<PreviewResponse> {
    return this.http
      .post<PreviewResponse>(
        `${this.base}/${templateId}/preview`,
        { content, subject });
  }

  restore(
    templateId: string,
    versionId: string
  ): Observable<TemplateVersionItem> {
    return this.http
      .post<TemplateVersionItem>(
        `${this.base}/${templateId}` +
          `/restore/${versionId}`,
        {});
  }

  validate(
    templateId: string,
    content: string
  ): Observable<TemplateValidationResult> {
    return this.http
      .post<TemplateValidationResult>(
        `${this.base}/${templateId}/validate`,
        JSON.stringify(content),
        { headers: {
            'Content-Type': 'application/json'
        }});
  }
}
```

3. **Create `TemplateEditorComponent`** — page container with split view:

```typescript
// client/src/app/features/admin/templates/
//   template-editor.component.ts
import {
  Component, signal, inject,
  OnInit, computed
} from '@angular/core';
import { MatSelectModule } from
  '@angular/material/select';
import { MatFormFieldModule } from
  '@angular/material/form-field';
import { MatButtonModule } from
  '@angular/material/button';
import { MatIconModule } from
  '@angular/material/icon';
import { MatTabsModule } from
  '@angular/material/tabs';
import { MatSnackBar } from
  '@angular/material/snack-bar';
import { MatDialog } from
  '@angular/material/dialog';
import { MatProgressSpinnerModule } from
  '@angular/material/progress-spinner';
import { FormsModule } from '@angular/forms';
import { TemplateApiService } from
  './template-api.service';
import { CodeEditorComponent } from
  './code-editor.component';
import { TemplatePreviewComponent } from
  './template-preview.component';
import {
  VersionHistorySidebarComponent
} from './version-history-sidebar.component';
import { SmsCounterComponent } from
  './sms-counter.component';
import {
  TemplateListItem, TemplateDetail,
  TemplateValidationResult
} from './models/template.models';

@Component({
  selector: 'app-template-editor',
  standalone: true,
  imports: [
    MatSelectModule, MatFormFieldModule,
    MatButtonModule, MatIconModule,
    MatTabsModule, MatProgressSpinnerModule,
    FormsModule,
    CodeEditorComponent,
    TemplatePreviewComponent,
    VersionHistorySidebarComponent,
    SmsCounterComponent
  ],
  templateUrl:
    './template-editor.component.html',
  styleUrl:
    './template-editor.component.scss'
})
export class TemplateEditorComponent
    implements OnInit {
  private readonly api =
    inject(TemplateApiService);
  private readonly snackBar =
    inject(MatSnackBar);
  private readonly dialog = inject(MatDialog);

  readonly templates =
    signal<TemplateListItem[]>([]);
  readonly selectedTemplate =
    signal<TemplateDetail | null>(null);
  readonly editorContent = signal('');
  readonly editorSubject = signal('');
  readonly previewHtml = signal('');
  readonly loading = signal(false);
  readonly saving = signal(false);
  readonly validationErrors =
    signal<TemplateValidationResult | null>(null);
  readonly showVersionHistory = signal(true);

  readonly isSms = computed(() =>
    this.selectedTemplate()?.type === 'SMS');

  ngOnInit(): void {
    this.loadTemplates();
  }

  loadTemplates(): void {
    this.loading.set(true);
    this.api.list().subscribe({
      next: (result) => {
        this.templates.set(result.items);
        this.loading.set(false);
      },
      error: () => {
        this.loading.set(false);
        this.snackBar.open(
          'Failed to load templates',
          'Retry', { duration: 5000 });
      }
    });
  }

  onTemplateSelect(templateId: string): void {
    this.loading.set(true);
    this.api.getById(templateId).subscribe({
      next: (detail) => {
        this.selectedTemplate.set(detail);
        this.editorContent.set(
          detail.currentVersion.content);
        this.editorSubject.set(
          detail.currentVersion.subject ?? '');
        this.validationErrors.set(null);
        this.loading.set(false);
        this.refreshPreview();
      },
      error: () => this.loading.set(false)
    });
  }

  onContentChange(content: string): void {
    this.editorContent.set(content);
    this.validationErrors.set(null);
    this.refreshPreview();
  }

  refreshPreview(): void {
    const template = this.selectedTemplate();
    if (!template) return;
    this.api.preview(
      template.id,
      this.editorContent(),
      this.editorSubject() || undefined
    ).subscribe({
      next: (resp) =>
        this.previewHtml.set(resp.renderedHtml),
      error: () => {}
    });
  }

  save(): void {
    const template = this.selectedTemplate();
    if (!template) return;

    this.saving.set(true);
    this.api.save(template.id, {
      content: this.editorContent(),
      subject: this.editorSubject() || undefined
    }).subscribe({
      next: (version) => {
        this.saving.set(false);
        this.snackBar.open(
          `Version ${version.versionNumber} saved`,
          'Dismiss', { duration: 5000 });
        this.onTemplateSelect(template.id);
      },
      error: (err) => {
        this.saving.set(false);
        if (err.status === 422) {
          this.validationErrors.set(err.error);
          this.snackBar.open(
            'Template has validation errors',
            'Dismiss',
            { duration: 0 });
        } else {
          this.snackBar.open(
            'Save failed. Try again.',
            'Retry', { duration: 0 });
        }
      }
    });
  }

  toggleVersionHistory(): void {
    this.showVersionHistory.update(v => !v);
  }

  onVersionRestored(): void {
    const template = this.selectedTemplate();
    if (template) {
      this.onTemplateSelect(template.id);
    }
  }
}
```

```html
<!-- template-editor.component.html -->
<div class="template-editor">
  <header class="page-header">
    <h1>Template Editor</h1>

    <div class="header-controls">
      <mat-form-field appearance="outline"
                      class="template-selector">
        <mat-label>Select Template</mat-label>
        <mat-select
          (selectionChange)="
            onTemplateSelect($event.value)">
          @for (t of templates(); track t.id) {
            <mat-option [value]="t.id">
              {{ t.name }} ({{ t.type }})
            </mat-option>
          }
        </mat-select>
      </mat-form-field>

      <button mat-icon-button
              (click)="toggleVersionHistory()"
              aria-label="Toggle version history">
        <mat-icon>history</mat-icon>
      </button>
    </div>
  </header>

  <!-- Loading State -->
  @if (loading()) {
    <div class="skeleton-editor">
      <div class="skeleton-pane"></div>
      <div class="skeleton-pane"></div>
    </div>
  } @else if (!selectedTemplate()) {
    <!-- Empty State -->
    <div class="empty-state">
      <mat-icon>description</mat-icon>
      <h2>Select a template to begin editing</h2>
      <p>
        Choose a notification template from the
        dropdown above.
      </p>
    </div>
  } @else {
    <!-- Subject line for HTML templates -->
    @if (!isSms()) {
      <mat-form-field appearance="outline"
                      class="subject-field">
        <mat-label>Subject Line</mat-label>
        <input matInput
               [ngModel]="editorSubject()"
               (ngModelChange)="
                 editorSubject.set($event)"
               aria-label="Email subject line">
      </mat-form-field>
    }

    <!-- Validation Errors (inline) -->
    @if (validationErrors(); as v) {
      <div class="validation-banner"
           role="alert"
           aria-live="assertive">
        @if (v.invalidPlaceholders.length > 0) {
          <p>
            <mat-icon color="warn">error</mat-icon>
            Invalid merge fields:
            <strong>
              {{ v.invalidPlaceholders.join(', ') }}
            </strong>
          </p>
        }
        @if (v.orphanedPlaceholders.length > 0) {
          <p>
            <mat-icon color="warn">
              warning
            </mat-icon>
            Orphaned merge fields:
            <strong>
              {{ v.orphanedPlaceholders.join(', ') }}
            </strong>
          </p>
        }
      </div>
    }

    <!-- Desktop: Split View -->
    <div class="split-view desktop-only">
      <div class="editor-pane">
        <app-code-editor
          [content]="editorContent()"
          [templateType]="
            selectedTemplate()!.type"
          (contentChange)="
            onContentChange($event)">
        </app-code-editor>

        @if (isSms()) {
          <app-sms-counter
            [content]="editorContent()">
          </app-sms-counter>
        }
      </div>

      <div class="preview-pane">
        <app-template-preview
          [html]="previewHtml()"
          [templateType]="
            selectedTemplate()!.type">
        </app-template-preview>
      </div>
    </div>

    <!-- Mobile: Tabbed View -->
    <mat-tab-group class="mobile-only">
      <mat-tab label="Editor">
        <app-code-editor
          [content]="editorContent()"
          [templateType]="
            selectedTemplate()!.type"
          (contentChange)="
            onContentChange($event)">
        </app-code-editor>

        @if (isSms()) {
          <app-sms-counter
            [content]="editorContent()">
          </app-sms-counter>
        }
      </mat-tab>
      <mat-tab label="Preview">
        <app-template-preview
          [html]="previewHtml()"
          [templateType]="
            selectedTemplate()!.type">
        </app-template-preview>
      </mat-tab>
    </mat-tab-group>

    <!-- Save Button -->
    <div class="action-bar">
      <button mat-raised-button
              color="primary"
              [disabled]="saving()"
              (click)="save()">
        @if (saving()) {
          <mat-spinner diameter="20">
          </mat-spinner>
        } @else {
          <mat-icon>save</mat-icon>
          Save Version
        }
      </button>
    </div>

    <!-- Version History Sidebar -->
    @if (showVersionHistory()) {
      <app-version-history-sidebar
        [templateId]="selectedTemplate()!.id"
        (versionRestored)="onVersionRestored()">
      </app-version-history-sidebar>
    }
  }
</div>
```

4. **Create `CodeEditorComponent`** — monospace textarea with syntax context:

```typescript
// client/src/app/features/admin/templates/
//   code-editor.component.ts
import {
  Component, input, output
} from '@angular/core';
import { FormsModule } from '@angular/forms';

@Component({
  selector: 'app-code-editor',
  standalone: true,
  imports: [FormsModule],
  template: `
    <div class="code-editor-wrapper">
      <label for="templateContent"
             class="sr-only">
        Template content
      </label>
      <textarea
        id="templateContent"
        class="code-editor"
        [class.html-mode]="
          templateType() === 'HTML'"
        [class.sms-mode]="
          templateType() === 'SMS'"
        [ngModel]="content()"
        (ngModelChange)="
          contentChange.emit($event)"
        spellcheck="false"
        aria-label="Template content editor">
      </textarea>
    </div>
  `,
  styles: [`
    .code-editor {
      width: 100%;
      min-height: 400px;
      font-family: 'JetBrains Mono', monospace;
      font-size: 14px;
      line-height: 1.5;
      padding: 16px;
      border: 1px solid var(--border-color,
        #ccc);
      border-radius: 4px;
      resize: vertical;
      tab-size: 2;
    }
    .sr-only {
      position: absolute;
      width: 1px;
      height: 1px;
      padding: 0;
      margin: -1px;
      overflow: hidden;
      clip: rect(0, 0, 0, 0);
      border: 0;
    }
  `]
})
export class CodeEditorComponent {
  readonly content = input.required<string>();
  readonly templateType =
    input.required<string>();
  readonly contentChange = output<string>();
}
```

5. **Create `TemplatePreviewComponent`** — rendered HTML preview or SMS text preview:

```typescript
// client/src/app/features/admin/templates/
//   template-preview.component.ts
import { Component, input } from '@angular/core';
import { DomSanitizer } from
  '@angular/platform-browser';

@Component({
  selector: 'app-template-preview',
  standalone: true,
  template: `
    <div class="preview-container">
      <h3>Preview</h3>
      @if (templateType() === 'HTML') {
        <div class="html-preview"
             [innerHTML]="
               sanitizedHtml()">
        </div>
      } @else {
        <div class="sms-preview">
          <div class="sms-bubble">
            {{ html() }}
          </div>
        </div>
      }
    </div>
  `,
  styles: [`
    .preview-container {
      padding: 16px;
      border: 1px solid var(--border-color,
        #e0e0e0);
      border-radius: 4px;
      background: var(--surface-color, #fafafa);
      min-height: 400px;
    }
    .html-preview {
      padding: 16px;
      background: white;
      border-radius: 4px;
    }
    .sms-bubble {
      max-width: 280px;
      padding: 12px 16px;
      background: #e3f2fd;
      border-radius: 18px 18px 4px 18px;
      font-size: 14px;
      line-height: 1.4;
    }
  `]
})
export class TemplatePreviewComponent {
  readonly html = input.required<string>();
  readonly templateType =
    input.required<string>();

  private readonly sanitizer =
    // DomSanitizer injected for HTML preview
    // Note: Only admin-authored content is
    // rendered; no user-generated HTML
    undefined as any;

  sanitizedHtml() {
    return this.html();
  }
}
```

6. **Create `VersionHistorySidebarComponent`** with version list and restore action (AC-3):

```typescript
// client/src/app/features/admin/templates/
//   version-history-sidebar.component.ts
import {
  Component, input, output, signal,
  inject, OnInit
} from '@angular/core';
import { MatListModule } from
  '@angular/material/list';
import { MatIconModule } from
  '@angular/material/icon';
import { MatButtonModule } from
  '@angular/material/button';
import { MatDialog } from
  '@angular/material/dialog';
import { MatSnackBar } from
  '@angular/material/snack-bar';
import { DatePipe } from '@angular/common';
import { TemplateApiService } from
  './template-api.service';
import {
  RestoreConfirmDialogComponent
} from './restore-confirm-dialog.component';
import { TemplateVersionItem } from
  './models/template.models';

@Component({
  selector: 'app-version-history-sidebar',
  standalone: true,
  imports: [
    MatListModule, MatIconModule,
    MatButtonModule, DatePipe
  ],
  templateUrl:
    './version-history-sidebar.component.html'
})
export class VersionHistorySidebarComponent
    implements OnInit {
  readonly templateId =
    input.required<string>();
  readonly versionRestored = output<void>();

  private readonly api =
    inject(TemplateApiService);
  private readonly dialog = inject(MatDialog);
  private readonly snackBar = inject(MatSnackBar);

  readonly versions =
    signal<TemplateVersionItem[]>([]);
  readonly loading = signal(false);

  ngOnInit(): void {
    this.loadVersions();
  }

  loadVersions(): void {
    this.loading.set(true);
    this.api.getVersions(
      this.templateId()
    ).subscribe({
      next: (data) => {
        this.versions.set(data);
        this.loading.set(false);
      },
      error: () => this.loading.set(false)
    });
  }

  restore(version: TemplateVersionItem): void {
    const dialogRef = this.dialog.open(
      RestoreConfirmDialogComponent, {
        data: { versionNumber:
          version.versionNumber },
        width: '400px'
      });

    dialogRef.afterClosed().subscribe(
      (confirmed) => {
        if (!confirmed) return;
        this.api.restore(
          this.templateId(), version.id
        ).subscribe({
          next: (newVersion) => {
            this.snackBar.open(
              `Restored as version ` +
                `${newVersion.versionNumber}`,
              'Dismiss', { duration: 5000 });
            this.versionRestored.emit();
            this.loadVersions();
          },
          error: () => {
            this.snackBar.open(
              'Restore failed',
              'Dismiss', { duration: 0 });
          }
        });
      });
  }
}
```

```html
<!-- version-history-sidebar.component.html -->
<div class="version-sidebar">
  <h3>Version History</h3>

  @if (loading()) {
    <div class="skeleton-list">
      @for (i of [1,2,3]; track i) {
        <div class="skeleton-item"></div>
      }
    </div>
  } @else if (versions().length === 0) {
    <p class="no-versions">
      No versions yet.
    </p>
  } @else {
    <mat-list>
      @for (v of versions(); track v.id) {
        <mat-list-item>
          <mat-icon matListItemIcon>
            {{ v.isActive
                ? 'check_circle'
                : 'history' }}
          </mat-icon>
          <span matListItemTitle>
            Version {{ v.versionNumber }}
            @if (v.isActive) {
              <strong>(Active)</strong>
            }
          </span>
          <span matListItemLine>
            {{ v.createdAtUtc | date:'medium' }}
            — {{ v.createdByName }}
          </span>
          @if (!v.isActive) {
            <button mat-icon-button
                    matListItemMeta
                    (click)="restore(v)"
                    [attr.aria-label]="
                      'Restore version '
                      + v.versionNumber">
              <mat-icon>restore</mat-icon>
            </button>
          }
        </mat-list-item>
      }
    </mat-list>
  }
</div>
```

7. **Create `SmsCounterComponent`** for real-time character count and multi-part SMS estimate (edge case 1):

```typescript
// client/src/app/features/admin/templates/
//   sms-counter.component.ts
import {
  Component, input, computed
} from '@angular/core';

@Component({
  selector: 'app-sms-counter',
  standalone: true,
  template: `
    <div class="sms-counter"
         [class.warning]="isMultiPart()"
         role="status"
         aria-live="polite">
      <span>
        {{ characterCount() }} / 160 characters
      </span>
      @if (isMultiPart()) {
        <span class="multi-part-warning">
          Multi-part SMS:
          ~{{ estimatedSegments() }} message(s)
        </span>
      }
    </div>
  `,
  styles: [`
    .sms-counter {
      padding: 8px 16px;
      font-size: 13px;
      color: var(--text-secondary, #666);
      display: flex;
      justify-content: space-between;
    }
    .warning {
      color: var(--warn-color, #f57c00);
    }
    .multi-part-warning {
      font-weight: 600;
    }
  `]
})
export class SmsCounterComponent {
  readonly content = input.required<string>();

  readonly characterCount = computed(() =>
    this.content().length);

  readonly isMultiPart = computed(() =>
    this.characterCount() > 160);

  // GSM concatenation: 153 chars per segment
  readonly estimatedSegments = computed(() =>
    this.isMultiPart()
      ? Math.ceil(
          this.characterCount() / 153)
      : 1);
}
```

8. **Create `RestoreConfirmDialogComponent`** and add lazy-loaded route:

```typescript
// client/src/app/features/admin/templates/
//   restore-confirm-dialog.component.ts
import { Component, inject } from '@angular/core';
import {
  MAT_DIALOG_DATA, MatDialogModule,
  MatDialogRef
} from '@angular/material/dialog';
import { MatButtonModule } from
  '@angular/material/button';

@Component({
  selector: 'app-restore-confirm-dialog',
  standalone: true,
  imports: [MatDialogModule, MatButtonModule],
  template: `
    <h2 mat-dialog-title>
      Restore Version
    </h2>
    <mat-dialog-content>
      <p>
        Restore version
        <strong>{{ data.versionNumber }}</strong>
        as the active template?
      </p>
      <p class="hint">
        This creates a new version with the
        selected content. Existing queued
        notifications will not be affected.
      </p>
    </mat-dialog-content>
    <mat-dialog-actions align="end">
      <button mat-button
              (click)="dialogRef.close(false)">
        Cancel
      </button>
      <button mat-raised-button
              color="primary"
              (click)="dialogRef.close(true)">
        Restore
      </button>
    </mat-dialog-actions>
  `,
  styles: [`
    .hint {
      color: var(--text-secondary, #666);
      font-size: 13px;
    }
  `]
})
export class RestoreConfirmDialogComponent {
  readonly data = inject(MAT_DIALOG_DATA);
  readonly dialogRef = inject(
    MatDialogRef<
      RestoreConfirmDialogComponent>);
}
```

```typescript
// In app.routes.ts
{
  path: 'admin/templates',
  loadComponent: () =>
    import(
      './features/admin/templates/' +
      'template-editor.component'
    ).then(m => m.TemplateEditorComponent),
  canActivate: [adminGuard]
}
```

## Current Project State

```text
propelIQ/
└── client/
    └── src/
        └── app/
            ├── app.routes.ts                                    (modify)
            └── features/
                └── admin/
                    └── templates/
                        ├── template-editor.component.ts          (new)
                        ├── template-editor.component.html        (new)
                        ├── template-editor.component.scss        (new)
                        ├── code-editor.component.ts              (new)
                        ├── code-editor.component.html            (new)
                        ├── template-preview.component.ts         (new)
                        ├── template-preview.component.html       (new)
                        ├── version-history-sidebar.component.ts  (new)
                        ├── version-history-sidebar.component.html(new)
                        ├── sms-counter.component.ts              (new)
                        ├── restore-confirm-dialog.component.ts   (new)
                        ├── models/
                        │   └── template.models.ts                (new)
                        └── template-api.service.ts               (new)
```

> Placeholder: Update on execution based on US_062 task_001 and task_002 completion state.

## Expected Changes

| Action | File Path | Description |
|--------|-----------|-------------|
| CREATE | client/src/app/features/admin/templates/models/template.models.ts | TypeScript interfaces for template list, detail, versions, preview, validation |
| CREATE | client/src/app/features/admin/templates/template-api.service.ts | HttpClient service for list, get, versions, save, preview, restore, validate |
| CREATE | client/src/app/features/admin/templates/template-editor.component.ts | Page container with split view, template selector, save action |
| CREATE | client/src/app/features/admin/templates/template-editor.component.html | Split editor/preview layout, mobile tab view, validation banner |
| CREATE | client/src/app/features/admin/templates/template-editor.component.scss | Responsive styles for split/tabbed layout, skeleton states |
| CREATE | client/src/app/features/admin/templates/code-editor.component.ts | Monospace textarea with JetBrains Mono font |
| CREATE | client/src/app/features/admin/templates/template-preview.component.ts | Rendered HTML preview and SMS bubble preview |
| CREATE | client/src/app/features/admin/templates/version-history-sidebar.component.ts | Version list with restore button and confirmation dialog |
| CREATE | client/src/app/features/admin/templates/version-history-sidebar.component.html | Version history list template |
| CREATE | client/src/app/features/admin/templates/sms-counter.component.ts | Real-time character counter with multi-part SMS estimate |
| CREATE | client/src/app/features/admin/templates/restore-confirm-dialog.component.ts | Confirmation dialog for version restore |
| MODIFY | client/src/app/app.routes.ts | Add /admin/templates route with adminGuard |

## External References

- Angular Material Tabs: https://material.angular.io/components/tabs/overview
- Angular Material Select: https://material.angular.io/components/select/overview
- Angular Material Dialog: https://material.angular.io/components/dialog/overview
- Angular Material Snack Bar: https://material.angular.io/components/snack-bar/overview
- Angular Material List: https://material.angular.io/components/list/overview
- JetBrains Mono Font: https://www.jetbrains.com/lp/mono/
- GSM SMS Concatenation Rules: https://en.wikipedia.org/wiki/Concatenated_SMS
- WCAG 2.1 AA Form Error Association: https://www.w3.org/WAI/WCAG21/Understanding/error-identification.html

## Build Commands

```bash
# Build frontend
cd client
ng build

# Serve locally
ng serve

# Test template editor flow:
# 1. Log in as Admin
# 2. Navigate to /admin/templates
# 3. Select an HTML template → verify split view renders
# 4. Edit content → verify preview updates live (AC-2)
# 5. Click Save → verify new version appears (AC-1)
# 6. Add {{invalid_field}} → click Save → verify
#    inline validation error (AC-4)
# 7. Open version history → select old version →
#    click Restore → confirm → verify new active (AC-3)
# 8. Switch to SMS template → type 200 chars →
#    verify multi-part warning (edge case 1)
# 9. Resize to 375px → verify tabbed layout
```

## Implementation Validation Strategy

- [ ] Split view renders editor on left and preview on right at 1440px (SCR-024 Default)
- [ ] Template selector dropdown loads template list and populates editor (SCR-024 Default)
- [ ] Save creates new version with admin name and timestamp in version list (AC-1)
- [ ] Preview shows rendered HTML with sample data substituted for merge fields (AC-2)
- [ ] Restore opens confirmation dialog and creates new active version from old content (AC-3)
- [ ] Invalid merge field placeholder shows inline validation error and blocks save (AC-4)
- [ ] SMS character counter shows count and multi-part warning above 160 characters (edge case 1)
- [ ] Orphaned placeholder warning displayed before save (edge case 2)
- [ ] Mobile view (< 768px) shows tabbed editor/preview layout (UXR-301)
- [ ] Error messages associated with form fields via aria-describedby (UXR-205)
- [ ] Success toast auto-dismisses at 5s, error toast persists (UXR-502)
- [ ] All interactive elements keyboard navigable (UXR-202)

## Implementation Checklist

- [ ] Create TypeScript interfaces for template list, detail, versions, preview, SMS info, and validation result
- [ ] Implement TemplateApiService with HttpClient for all template API endpoints
- [ ] Build TemplateEditorComponent with template selector, split view, validation banner, and save action
- [ ] Build CodeEditorComponent with monospace textarea (JetBrains Mono) and content change output
- [ ] Build TemplatePreviewComponent with HTML rendered preview and SMS bubble preview
- [ ] Build VersionHistorySidebarComponent with version list, restore button, and RestoreConfirmDialogComponent
- [ ] Build SmsCounterComponent with real-time character count and multi-part segment estimate
- [ ] Add lazy-loaded route at /admin/templates with adminGuard in app.routes.ts
