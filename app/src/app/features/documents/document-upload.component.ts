import {
  ChangeDetectionStrategy,
  Component,
  DestroyRef,
  ElementRef,
  ViewChild,
  inject,
  signal,
} from '@angular/core';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import {
  EMPTY,
  interval,
  catchError,
  switchMap,
  takeWhile,
  tap,
} from 'rxjs';
import { HttpEventType, HttpErrorResponse } from '@angular/common/http';
import { CommonModule } from '@angular/common';

import { MatButtonModule } from '@angular/material/button';
import { MatExpansionModule } from '@angular/material/expansion';
import { MatIconModule } from '@angular/material/icon';
import { MatProgressBarModule } from '@angular/material/progress-bar';
import { MatProgressSpinnerModule } from '@angular/material/progress-spinner';
import { MatSnackBar, MatSnackBarModule } from '@angular/material/snack-bar';
import { MatTooltipModule } from '@angular/material/tooltip';
import { MatChipsModule } from '@angular/material/chips';

import { DocumentUploadService } from './document-upload.service';
import {
  UploadFileStatus,
  ScanResult,
  isFinalScanResult,
} from '../../shared/models/upload-file-status.model';
import {
  ExtractionStatus,
  isFinalExtractionStatus,
} from '../../shared/models/extraction-status.enum';
import type { DocumentUploadResponse } from '../../shared/models/document-upload-response.model';

/** Maximum accepted file size in bytes (10 MB, FR-DM-001, Edge Case 2). */
const MAX_FILE_SIZE = 10 * 1024 * 1024;

/** Accepted MIME types (AC-1). */
const ACCEPTED_MIME = new Set([
  'application/pdf',
  'image/jpeg',
  'image/png',
  'image/tiff',
]);

/** Accepted file extensions (AC-1 â€” checked alongside MIME as defence-in-depth). */
const ACCEPTED_EXTENSIONS = new Set(['.pdf', '.jpg', '.jpeg', '.png', '.tiff', '.tif']);

/** Human-readable accepted format list (AC-4 error message). */
const ACCEPTED_FORMAT_LABEL = 'PDF, JPG, PNG, TIFF';

/** Polling interval for scan/OCR status (milliseconds). */
const POLL_INTERVAL_MS = 3000;

/** Maximum OCR retry attempts before dead-letter queue (US_041 AC-4). */
const MAX_OCR_RETRIES = 3;

/** Badge config per scan result status (UXR-404). */
const BADGE_CONFIG: Record<
  ScanResult,
  { label: string; cssClass: string; ariaLabel: string }
> = {
  Scanning:       { label: 'Scanning',       cssClass: 'badge-info',    ariaLabel: 'Malware scan in progress' },
  Clean:          { label: 'Clean',          cssClass: 'badge-success', ariaLabel: 'Malware scan passed' },
  ThreatDetected: { label: 'Threat Detected',cssClass: 'badge-error',   ariaLabel: 'Malware threat detected â€” file rejected' },
  PendingScan:    { label: 'Pending Scan',   cssClass: 'badge-warning', ariaLabel: 'Scan queued â€” scanner unavailable' },
  Processing:     { label: 'Processing',     cssClass: 'badge-info',    ariaLabel: 'OCR processing in progress' },
  Completed:      { label: 'Completed',      cssClass: 'badge-success', ariaLabel: 'Processing complete' },
  Failed:         { label: 'Failed',         cssClass: 'badge-error',   ariaLabel: 'Processing failed' },
};

/** Badge config per extraction status (UXR-404, US_041). */
const EXTRACTION_BADGE_CONFIG: Record<
  string,
  { label: string; cssClass: string; ariaLabel: string; showSpinner: boolean }
> = {
  Queued:     { label: 'OCR Queued',     cssClass: 'badge-neutral',  ariaLabel: 'OCR job queued',                  showSpinner: false },
  Processing: { label: 'OCR Processing', cssClass: 'badge-info',     ariaLabel: 'OCR processing in progress',      showSpinner: true  },
  Completed:  { label: 'OCR Completed',  cssClass: 'badge-success',  ariaLabel: 'OCR extraction complete',         showSpinner: false },
  Failed:     { label: 'OCR Failed',     cssClass: 'badge-error',    ariaLabel: 'OCR extraction failed',           showSpinner: false },
};

/**
 * Document Upload Screen (EP-006 US_040/US_041 SCR-011).
 *
 * AC-1: Validates file type (ext + MIME) and size (â‰¤10 MB) before upload.
 * AC-2: Files are uploaded and malware-scanned before being persisted.
 * AC-3: Threat-detected files are shown with a red error banner.
 * AC-4: Unsupported type shows error toast listing accepted formats.
 * Edge Case 1 (US_040): PendingScan badge shown when scanner is unavailable.
 * Edge Case 2 (US_040): Oversized files show error toast without initiating upload.
 * US_041 AC-1â†’AC-4: OCR status tracking with badges, retry button, extracted text preview.
 * US_041 Edge Case 1: Low-confidence extractions show "Manual Review Required" amber badge.
 * US_041 Edge Case 2: Concurrent documents track status independently per file row.
 *
 * UXR-201/202: WCAG AA contrast, full keyboard navigation.
 * UXR-203: aria-live="polite" region announces OCR status transitions.
 * UXR-205: Error messages linked via aria-describedby.
 * UXR-301: Responsive at 375/768/1440 px.
 * UXR-404: Status colour semantics (green/amber/red/blue/grey).
 * UXR-501: Retry button disabled + spinner during request.
 * UXR-505: Drag-and-drop with per-file progress bar and cancel capability.
 */
@Component({
  selector: 'app-document-upload',
  standalone: true,
  imports: [
    CommonModule,
    MatButtonModule,
    MatExpansionModule,
    MatIconModule,
    MatProgressBarModule,
    MatProgressSpinnerModule,
    MatSnackBarModule,
    MatTooltipModule,
    MatChipsModule,
  ],
  templateUrl: './document-upload.component.html',
  styleUrls: ['./document-upload.component.scss'],
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class DocumentUploadComponent {
  @ViewChild('fileInput') private readonly fileInputRef!: ElementRef<HTMLInputElement>;

  private readonly uploadService = inject(DocumentUploadService);
  private readonly snackBar = inject(MatSnackBar);
  private readonly destroyRef = inject(DestroyRef);

  // â”€â”€ State signals â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€
  readonly files = signal<UploadFileStatus[]>([]);
  readonly isDragOver = signal(false);

  // â”€â”€ Static config â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€
  readonly acceptedFormatLabel = ACCEPTED_FORMAT_LABEL;
  readonly badgeConfig = BADGE_CONFIG;
  readonly extractionBadgeConfig = EXTRACTION_BADGE_CONFIG;
  readonly isFinal = isFinalScanResult;
  readonly maxOcrRetries = MAX_OCR_RETRIES;

  /** PatientId injected from the JWT in a real scenario. Placeholder for now. */
  private readonly patientId = 'current';   // resolved by auth interceptor

  // â”€â”€ Drop zone event handlers â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€

  onDragOver(event: DragEvent): void {
    event.preventDefault();
    event.stopPropagation();
    this.isDragOver.set(true);
  }

  onDragLeave(event: DragEvent): void {
    event.preventDefault();
    event.stopPropagation();
    this.isDragOver.set(false);
  }

  onDrop(event: DragEvent): void {
    event.preventDefault();
    event.stopPropagation();
    this.isDragOver.set(false);
    const files = event.dataTransfer?.files;
    if (files?.length) this.processFiles(files);
  }

  /** Triggered when the hidden `<input type="file">` selection changes. */
  onFileInputChange(event: Event): void {
    const input = event.target as HTMLInputElement;
    if (input.files?.length) this.processFiles(input.files);
    // Reset so re-selecting the same file triggers the change event again.
    input.value = '';
  }

  /** Opens the system file picker (keyboard/button activation). */
  openFilePicker(): void {
    this.fileInputRef.nativeElement.click();
  }

  // â”€â”€ File handling â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€

  private processFiles(fileList: FileList): void {
    Array.from(fileList).forEach(file => {
      if (!this.validateFile(file)) return;
      this.startUpload(file);
    });
  }

  private validateFile(file: File): boolean {
    // Extension check (AC-1)
    const ext = '.' + file.name.split('.').pop()?.toLowerCase();
    const mimeOk = ACCEPTED_MIME.has(file.type);
    const extOk = ACCEPTED_EXTENSIONS.has(ext);

    if (!mimeOk || !extOk) {
      this.snackBar.open(
        `Unsupported file type. Accepted formats: ${ACCEPTED_FORMAT_LABEL}.`,
        'Dismiss',
        { duration: 6000, panelClass: 'snack-error' },
      );
      return false;
    }

    // Size check (Edge Case 2)
    if (file.size > MAX_FILE_SIZE) {
      this.snackBar.open(
        'File exceeds the maximum allowed size of 10 MB.',
        'Dismiss',
        { duration: 6000, panelClass: 'snack-error' },
      );
      return false;
    }

    return true;
  }

  // â”€â”€ Upload + progress â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€

  private startUpload(file: File): void {
    const entry: UploadFileStatus = {
      documentId:           null,
      fileName:             file.name,
      fileSize:             file.size,
      uploadProgress:       0,
      scanResult:           'Scanning',
      isUploading:          true,
      uploadError:          null,
      extractionStatus:     null,
      extractedTextPreview: null,
      needsManualReview:    false,
      retryCount:           0,
      isRetrying:           false,
    };

    this.files.update(list => [...list, entry]);
    const index = this.files().length - 1;

    this.uploadService.upload(file, this.patientId)
      .pipe(
        catchError((err: HttpErrorResponse) => {
          const msg = err.error?.message ?? 'Upload failed. Please try again.';
          this.updateFile(index, { isUploading: false, uploadError: msg });
          return EMPTY;
        }),
        takeUntilDestroyed(this.destroyRef),
      )
      .subscribe(event => {
        if (event.type === HttpEventType.UploadProgress) {
          const progress = event.total
            ? Math.round((100 * event.loaded) / event.total)
            : 0;
          this.updateFile(index, { uploadProgress: progress });
        }

        if (event.type === HttpEventType.Response) {
          const body = event.body as DocumentUploadResponse;
          this.updateFile(index, {
            documentId:     body.documentId,
            uploadProgress: 100,
            isUploading:    false,
            scanResult:     (body.scanResult as ScanResult) ?? 'Scanning',
          });

          if (body.documentId) {
            this.startPolling(body.documentId, index);
          }
        }
      });
  }

  // â”€â”€ Status polling â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€

  private startPolling(documentId: string, index: number): void {
    interval(POLL_INTERVAL_MS)
      .pipe(
        switchMap(() => this.uploadService.getStatus(documentId).pipe(
          catchError(() => EMPTY),   // ignore transient polling errors
        )),
        tap(status => {
          const scanResult = (status.scanResult as ScanResult) ?? 'Scanning';
          const extractionStatus = status.extractionStatus ?? null;

          this.updateFile(index, {
            scanResult,
            extractionStatus,
            extractedTextPreview: status.extractedText
              ? status.extractedText.slice(0, 500)
              : null,
            needsManualReview: status.needsManualReview ?? false,
          });

          // AC-3: surface threat detection prominently.
          if (scanResult === 'ThreatDetected') {
            this.snackBar.open(
              `File rejected: malware detected in "${this.files()[index].fileName}".`,
              'Dismiss',
              { duration: 0, panelClass: 'snack-error' },
            );
          }

          // US_041 AC-2/AC-3: announce OCR status transitions via snackbar for
          // non-screen-reader users; aria-live region handles assistive tech.
          if (extractionStatus === ExtractionStatus.Completed) {
            this.snackBar.open(
              `OCR completed for "${this.files()[index].fileName}".`,
              'Dismiss',
              { duration: 4000 },
            );
          }

          if (extractionStatus === ExtractionStatus.Failed) {
            const file = this.files()[index];
            if (file.retryCount < MAX_OCR_RETRIES) {
              this.snackBar.open(
                `OCR failed for "${file.fileName}". Click Retry OCR to try again.`,
                'Dismiss',
                { duration: 6000, panelClass: 'snack-error' },
              );
            }
          }
        }),
        // Stop polling when both scan AND extraction have reached terminal states.
        takeWhile(status => {
          const scanDone = isFinalScanResult((status.scanResult as ScanResult) ?? 'Scanning');
          const extractionDone = status.extractionStatus
            ? isFinalExtractionStatus(status.extractionStatus)
            : false;
          // If scan is not done, keep polling.
          // If scan is done but extraction hasn't started yet, keep polling.
          // Stop only when scan is terminal AND (extraction is terminal OR not applicable).
          if (!scanDone) return true;
          if ((status.scanResult as ScanResult) === 'ThreatDetected') return false;
          if ((status.scanResult as ScanResult) === 'PendingScan') return true;
          // Scan is Clean â€” poll until extraction is terminal too.
          return !extractionDone;
        }, true),
        takeUntilDestroyed(this.destroyRef),
      )
      .subscribe();
  }

  // â”€â”€ OCR retry (US_041 AC-4) â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€

  retryOcr(index: number): void {
    const file = this.files()[index];
    if (!file?.documentId || file.isRetrying || file.retryCount >= MAX_OCR_RETRIES) return;

    this.updateFile(index, { isRetrying: true });

    this.uploadService.retryOcr(file.documentId)
      .pipe(
        catchError((err: HttpErrorResponse) => {
          const msg = err.error?.message ?? 'Retry failed. Please try again.';
          this.snackBar.open(msg, 'Dismiss', { duration: 5000, panelClass: 'snack-error' });
          this.updateFile(index, { isRetrying: false });
          return EMPTY;
        }),
        takeUntilDestroyed(this.destroyRef),
      )
      .subscribe(() => {
        this.updateFile(index, {
          isRetrying:       false,
          retryCount:       file.retryCount + 1,
          extractionStatus: ExtractionStatus.Queued,
        });
        // Resume polling after retry
        this.startPolling(file.documentId!, index);
      });
  }

  // â”€â”€ File list actions â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€

  removeFile(index: number): void {
    this.files.update(list => list.filter((_, i) => i !== index));
  }

  retryUpload(index: number): void {
    const entry = this.files()[index];
    if (!entry || entry.isUploading) return;
    this.snackBar.open(
      'Please re-select the file to retry the upload.',
      'OK',
      { duration: 5000 },
    );
  }

  /** Formats bytes as a human-readable string (e.g. "3.2 MB"). */
  formatFileSize(bytes: number): string {
    if (bytes < 1024) return `${bytes} B`;
    if (bytes < 1024 * 1024) return `${(bytes / 1024).toFixed(1)} KB`;
    return `${(bytes / (1024 * 1024)).toFixed(1)} MB`;
  }

  // â”€â”€ Helpers â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€

  private updateFile(index: number, patch: Partial<UploadFileStatus>): void {
    this.files.update(list =>
      list.map((f, i) => (i === index ? { ...f, ...patch } : f)),
    );
  }
}
