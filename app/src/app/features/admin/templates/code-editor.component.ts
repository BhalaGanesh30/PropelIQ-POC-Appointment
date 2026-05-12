import { ChangeDetectionStrategy, Component, input, output } from '@angular/core';
import { FormsModule } from '@angular/forms';

/**
 * Monospace `<textarea>` editor for HTML and SMS template content (SCR-024, AC-1, AC-4).
 *
 * Uses JetBrains Mono per figma_spec.md SCR-024 specification.
 * Emits `contentChange` on every keystroke so the parent can debounce
 * the live preview call (AC-2).
 *
 * Accessibility:
 * - Visible `<label>` linked by `for="templateContent"` (UXR-205).
 * - `aria-describedby` wired by parent via `aria-describedby` on the textarea
 *   when validation errors are present.
 */
@Component({
  selector: 'app-code-editor',
  standalone: true,
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [FormsModule],
  template: `
    <div class="code-editor-wrapper">
      <label for="templateContent" class="sr-only">Template content</label>
      <textarea
        id="templateContent"
        class="code-editor"
        [class.html-mode]="templateType() === 'HTML'"
        [class.sms-mode]="templateType() === 'SMS'"
        [ngModel]="content()"
        (ngModelChange)="contentChange.emit($event)"
        [attr.aria-label]="'Template content — ' + templateType() + ' mode'"
        [attr.aria-describedby]="errorDescribedBy() || null"
        spellcheck="false"
        autocomplete="off"
        autocorrect="off"
        autocapitalize="off"
      ></textarea>
    </div>
  `,
  styles: [
    `
      .code-editor-wrapper {
        display: flex;
        flex-direction: column;
        height: 100%;
      }

      .code-editor {
        flex: 1;
        width: 100%;
        min-height: 400px;
        font-family: 'JetBrains Mono', 'Fira Code', 'Cascadia Code', monospace;
        font-size: 14px;
        line-height: 1.6;
        padding: 16px;
        border: 1px solid #ccc;
        border-radius: 4px;
        resize: vertical;
        tab-size: 2;
        background: #1e1e2e;
        color: #cdd6f4;
        box-sizing: border-box;
        transition: border-color 0.2s;
      }

      .code-editor:focus {
        outline: none;
        border-color: #6c63ff;
        box-shadow: 0 0 0 2px rgba(108, 99, 255, 0.2);
      }

      .sms-mode {
        font-size: 15px;
        background: #f8f9fa;
        color: #212529;
        border-color: #dee2e6;
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
    `,
  ],
})
export class CodeEditorComponent {
  readonly content = input.required<string>();
  readonly templateType = input.required<'HTML' | 'SMS'>();
  /** Optional ID to wire aria-describedby when validation errors are present. */
  readonly errorDescribedBy = input<string | null>(null);
  readonly contentChange = output<string>();
}
