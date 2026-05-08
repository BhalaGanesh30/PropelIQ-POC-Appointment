/**
 * Document category values for classification (EP-006 US_043 AC-1).
 * Maps to BE DocumentCategory enum; string values are snake_case per API contract.
 */
export enum DocumentCategory {
  LabReport    = 'lab_report',
  Referral     = 'referral',
  Prescription = 'prescription',
  Imaging      = 'imaging',
  Insurance    = 'insurance',
  Other        = 'other',
}

/** Human-readable labels for each category (used in filter dropdowns and badges). */
export const DOCUMENT_CATEGORY_LABELS: Record<DocumentCategory, string> = {
  [DocumentCategory.LabReport]:    'Lab Report',
  [DocumentCategory.Referral]:     'Referral',
  [DocumentCategory.Prescription]: 'Prescription',
  [DocumentCategory.Imaging]:      'Imaging',
  [DocumentCategory.Insurance]:    'Insurance',
  [DocumentCategory.Other]:        'Other',
};
