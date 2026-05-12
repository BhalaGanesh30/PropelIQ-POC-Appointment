import { ChangeDetectionStrategy, Component, inject, input } from '@angular/core';
import { DomSanitizer, SafeHtml } from '@angular/platform-browser';
import { computed } from '@angular/core';

/**
 * Rendered template preview (SCR-024, AC-2).
 *
 * - HTML templates: renders sanitized markup inside an iframe-style container
 *   using `[innerHTML]` with DomSanitizer. Only admin-authored content is
 *   rendered — no untrusted user input reaches this component.
 * - SMS templates: displays plain text inside a chat-bubble frame.
 *
 * Security note: `bypassSecurityTrustHtml` is used intentionally because the
 * content is always authored by an authenticated Admin — never from an
 * end-user submission. The backend also strips script tags via template
 * validation (AC-4) before content reaches the database.
 */
@Component({
  selector: 'app-template-preview',
  standalone: true,
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <div class="preview-container" aria-label="Template preview" role="region">
      <div class="preview-header">
        <span class="preview-label">Preview</span>
        @if (templateType() === 'SMS') {
          <span class="type-badge sms-badge">SMS</span>
        } @else {
          <span class="type-badge html-badge">HTML</span>
        }
      </div>

      @if (html()) {
        @if (templateType() === 'HTML') {
          <div class="html-preview" [innerHTML]="safeHtml()"></div>
        } @else {
          <div class="sms-preview">
            <div class="sms-bubble">{{ html() }}</div>
          </div>
        }
      } @else {
        <div class="preview-empty">
          <span>Start typing to see a preview</span>
        </div>
      }
    </div>
  `,
  styles: [
    `
      .preview-container {
        display: flex;
        flex-direction: column;
        height: 100%;
        border: 1px solid #e0e0e0;
        border-radius: 4px;
        background: #fafafa;
        overflow: hidden;
      }

      .preview-header {
        display: flex;
        align-items: center;
        gap: 8px;
        padding: 10px 16px;
        border-bottom: 1px solid #e0e0e0;
        background: #f5f5f5;
      }

      .preview-label {
        font-size: 13px;
        font-weight: 600;
        color: #444;
      }

      .type-badge {
        font-size: 11px;
        padding: 2px 8px;
        border-radius: 10px;
        font-weight: 600;
      }

      .html-badge {
        background: #e3f2fd;
        color: #1565c0;
      }

      .sms-badge {
        background: #e8f5e9;
        color: #2e7d32;
      }

      .html-preview {
        flex: 1;
        padding: 20px;
        background: white;
        overflow-y: auto;
        font-family: -apple-system, BlinkMacSystemFont, 'Segoe UI', Roboto, sans-serif;
        font-size: 14px;
        line-height: 1.6;
      }

      .sms-preview {
        flex: 1;
        display: flex;
        align-items: flex-start;
        padding: 20px;
        background: #f0f2f5;
      }

      .sms-bubble {
        max-width: 280px;
        padding: 12px 16px;
        background: #e3f2fd;
        border-radius: 18px 18px 4px 18px;
        font-size: 14px;
        line-height: 1.5;
        color: #212121;
        white-space: pre-wrap;
        word-break: break-word;
      }

      .preview-empty {
        flex: 1;
        display: flex;
        align-items: center;
        justify-content: center;
        color: #9e9e9e;
        font-size: 14px;
      }
    `,
  ],
})
export class TemplatePreviewComponent {
  readonly html = input.required<string>();
  readonly templateType = input.required<'HTML' | 'SMS'>();

  private readonly sanitizer = inject(DomSanitizer);

  /**
   * Bypass sanitization because this content is exclusively admin-authored
   * and never contains end-user input.  DomSanitizer.bypassSecurityTrustHtml
   * prevents Angular's XSS guard from stripping valid CSS/HTML in templates.
   */
  readonly safeHtml = computed((): SafeHtml =>
    this.sanitizer.bypassSecurityTrustHtml(this.html()),
  );
}
