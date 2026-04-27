/** PUT /api/v1/intake/draft request body */
export interface SaveDraftRequest {
  slotId?: string;
  formData: Record<string, unknown>;
  aiPopulatedFields?: string[];
}

/** PUT /api/v1/intake/draft response */
export interface SaveDraftResponse {
  draftId: string;
  savedAt: string;
}

/** GET /api/v1/intake/draft response */
export interface IntakeDraftResponse {
  id: string;
  slotId?: string;
  formData: Record<string, unknown>;
  aiPopulatedFields: string[];
  status: string;
  updatedAt: string;
}

/** POST /api/v1/intake/submit request body */
export interface SubmitIntakeRequest {
  draftId: string;
  appointmentId: string;
}

/** POST /api/v1/intake/submit response */
export interface SubmitIntakeResponse {
  intakeRecordId: string;
  appointmentId: string;
  submittedAt: string;
}

/** POST /api/v1/intake/ai-assist request body */
export interface IntakeAssistRequest {
  freeTextDescription: string;
  language?: string;
}

/** POST /api/v1/intake/ai-assist response */
export interface IntakeAssistResponse {
  aiAssisted: boolean;
  fallbackReason?: string;
  suggestions: IntakeFieldSuggestions;
  aiPopulatedFields: string[];
  confidence: number;
}

/** Structured fields extracted by AI from free-text description */
export interface IntakeFieldSuggestions {
  reasonForVisit?: string;
  symptomDescription?: string;
  severity?: string;
  onsetDuration?: string;
  bodyArea?: string;
  relevantMedicalHistory: string[];
  currentMedications: string[];
  allergies: string[];
}
