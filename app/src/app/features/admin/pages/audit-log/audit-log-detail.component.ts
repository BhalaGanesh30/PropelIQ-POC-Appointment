import { Component, input, ChangeDetectionStrategy } from '@angular/core';
import { JsonPipe } from '@angular/common';

/**
 * Inline JSON detail panel shown when an audit log row is expanded (SCR-021).
 *
 * Displays the `metadata` record from the `AuditDetails.Metadata` column
 * in a monospace pre-formatted block (accessibility-friendly read-only viewer).
 *
 * UXR-202: Panel receives focus via the parent row's keyboard handler.
 */
@Component({
  selector: 'app-audit-log-detail',
  standalone: true,
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [JsonPipe],
  template: `
    <div class="detail-panel" role="region" aria-label="Audit event details">
      <pre class="json-viewer">{{ details() | json }}</pre>
    </div>
  `,
  styles: [`
    .detail-panel {
      padding: 12px 24px 16px;
      background: #FAFAFA;
      border-top: 1px solid #E0E0E0;
    }

    .json-viewer {
      font-family: 'Roboto Mono', 'Courier New', monospace;
      font-size: 12px;
      line-height: 1.6;
      white-space: pre-wrap;
      word-break: break-word;
      margin: 0;
      color: #212121;
    }
  `],
})
export class AuditLogDetailComponent {
  /** Structured metadata record from the audit entry's detail payload. */
  details = input.required<Record<string, unknown>>();
}
