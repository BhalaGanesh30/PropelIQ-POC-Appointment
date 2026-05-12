import { ChangeDetectionStrategy, Component, computed, input } from '@angular/core';

/**
 * Real-time SMS character counter with multi-part segment estimate (US_062, edge case 1).
 *
 * GSM-7 rules:
 * - Single message: max 160 characters.
 * - Concatenated (multi-part): 153 characters per segment (6-byte UDH header).
 *
 * Displayed below the code editor for SMS templates only.
 * `role="status" aria-live="polite"` announces updates to screen readers (UXR-205).
 */
@Component({
  selector: 'app-sms-counter',
  standalone: true,
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <div
      class="sms-counter"
      [class.over-limit]="isOverLimit()"
      [class.multi-part]="isMultiPart()"
      role="status"
      aria-live="polite"
      aria-atomic="true"
    >
      <span class="char-count">
        {{ characterCount() }} / 160 characters
      </span>

      @if (isMultiPart()) {
        <span class="multi-part-badge">
          Multi-part SMS — approx. {{ estimatedSegments() }} message(s)
        </span>
      }
    </div>
  `,
  styles: [
    `
      .sms-counter {
        display: flex;
        align-items: center;
        gap: 12px;
        padding: 6px 16px;
        font-size: 13px;
        color: #555;
        border-top: 1px solid #e0e0e0;
        transition: color 0.2s;
      }

      .over-limit {
        color: #e65100;
      }

      .multi-part-badge {
        font-weight: 600;
        background: #fff3e0;
        color: #e65100;
        padding: 2px 8px;
        border-radius: 12px;
        font-size: 12px;
        border: 1px solid #ffcc02;
      }
    `,
  ],
})
export class SmsCounterComponent {
  readonly content = input.required<string>();

  readonly characterCount = computed(() => this.content().length);

  /** True once the content length exceeds the single-message threshold. */
  readonly isOverLimit = computed(() => this.characterCount() > 160);

  /** Alias for template readability — same condition as isOverLimit. */
  readonly isMultiPart = computed(() => this.isOverLimit());

  /** ceil(count / 153) per GSM concatenated-SMS specification. */
  readonly estimatedSegments = computed(() =>
    this.isMultiPart() ? Math.ceil(this.characterCount() / 153) : 1,
  );
}
