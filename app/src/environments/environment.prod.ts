export const environment = {
  production: true,
  // apiBaseUrl is injected at deploy time via server-side configuration or runtime config.
  // Do NOT embed real API URLs or secrets here — this file is compiled into the bundle.
  apiBaseUrl: '/api/v1',
  features: {
    aiCodingSuggestions: true,
    clinicalExtraction: true,
    auditLog: true,
  },
};
