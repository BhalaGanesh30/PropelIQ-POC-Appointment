import {
  ChangeDetectionStrategy,
  Component,
  output,
  signal,
} from '@angular/core';
import { FormsModule } from '@angular/forms';
import { MatButtonModule } from '@angular/material/button';
import { MatIconModule } from '@angular/material/icon';
import { MatInputModule } from '@angular/material/input';
import { MatProgressSpinnerModule } from '@angular/material/progress-spinner';

/**
 * AI-assist panel rendered inside the intake form when AI mode is toggled on.
 * Exposes a `submitted` output that emits the trimmed free-text string so the
 * parent page can call the AI-assist API (AC-1).
 *
 * The parent calls `setProcessing(true/false)` to reflect the in-flight state
 * while keeping the spinner logic contained here.
 */
@Component({
  selector: 'app-ai-assist-panel',
  standalone: true,
  imports: [
    FormsModule,
    MatButtonModule,
    MatIconModule,
    MatInputModule,
    MatProgressSpinnerModule,
  ],
  templateUrl: './ai-assist-panel.component.html',
  styleUrl: './ai-assist-panel.component.scss',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class AiAssistPanelComponent {
  freeText = signal('');
  isProcessing = signal(false);

  /** Emits the trimmed free-text description when the user clicks Generate. */
  submitted = output<string>();

  onSubmit(): void {
    const text = this.freeText().trim();
    if (!text || this.isProcessing()) return;
    this.submitted.emit(text);
  }

  /** Called by the parent to toggle the loading state. */
  setProcessing(value: boolean): void {
    this.isProcessing.set(value);
  }
}
