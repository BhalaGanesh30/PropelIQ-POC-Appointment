## Architecture Design

## Project Overview
Unified Patient Access and Clinical Intelligence Platform for patients,
front-desk staff, clinicians, and administrators. The solution supports digital
appointment lifecycle management, reminder automation, clinical document
processing, conflict detection, and transparent AI-assisted medical coding under
HIPAA-aligned controls with free-tier-first hosting constraints.

## Architecture Goals
- Goal 1: Reduce no-shows below 5 percent through resilient reminder and
  waitlist orchestration.
- Goal 2: Reduce clinical preparation from 20 minutes to 2 minutes using OCR,
  extraction, and consolidated profile views.
- Goal 3: Achieve auditable and explainable coding workflows with mandatory
  human approval and traceable rationale.
- Goal 4: Maintain secure, compliant operations with immutable auditing,
  strict RBAC, encryption, and observable service behavior.
- Goal 5: Support 500 concurrent users without material performance
  degradation using scalable stateless APIs and caching.

## Non-Functional Requirements
- NFR-001: System MUST serve patient and staff portal interactions within
  3 seconds p95 page-load time under normal operating conditions.
- NFR-002: System MUST respond to synchronous API requests within 500 ms p95
  for booking, queue, and profile retrieval endpoints.
- NFR-003: System MUST complete OCR and document extraction processing within
  2 minutes p95 for files up to 10 MB.
- NFR-004: System MUST support 500 concurrent active users while maintaining
  NFR-001 and NFR-002 SLO thresholds.
- NFR-005: System MUST maintain 99.9 percent monthly service availability with
  automatic health checks and restart policies.
- NFR-006: System MUST provide recovery time objective of 4 hours and recovery
  point objective of 1 hour for critical data stores.
- NFR-007: System MUST encrypt protected health information at rest using
  AES-256 and enforce TLS 1.3 for all external and internal service traffic.
- NFR-008: System MUST enforce session timeout at 15 minutes with inactivity
  warning at 2 minutes and secure token revocation on logout.
- NFR-009: System MUST provide WCAG 2.1 AA-compliant experiences for patient
  and staff interfaces including keyboard and assistive-technology support.
- NFR-010: System MUST maintain immutable audit evidence for access events,
  coding decisions, and overrides with minimum 7-year retention.
- NFR-011: System MUST maintain observability coverage with structured logs,
  request traces, and service-level metrics across all modules.
- NFR-012: System MUST protect against abuse by enforcing per-user rate limits
  at or below 100 requests per minute and anomaly alerting.

## Data Requirements
- DR-001: System MUST persist core entities using globally unique identifiers
  and explicit foreign-key relationships for user, patient, appointment,
  document, clinical item, coding decision, and audit records.
- DR-002: System MUST enforce referential integrity and transactional
  consistency for booking, arrival, waitlist swap, and coding finalization.
- DR-003: System MUST store extracted clinical fields with confidence score,
  source document reference, verification state, and last reviewer metadata.
- DR-004: System MUST retain patient records and uploaded documents for
  10 years with policy-based archival after 3 years.
- DR-005: System MUST retain immutable audit and access logs for 7 years with
  append-only write constraints.
- DR-006: System MUST execute automated backups at least every 6 hours and
  provide point-in-time recovery for relational stores.
- DR-007: System MUST support schema migration with backward-compatible,
  zero-downtime rollouts for additive changes.
- DR-008: System MUST isolate tenant-level operational data and restrict
  direct access to PHI columns through role-aware data access policies.

### Domain Entities
- User: Authentication principal with role, status, session, and account
  security attributes; linked to staff or patient profile.
- Patient: Demographic and contact profile linked to appointments, documents,
  clinical extraction records, reminders, and insurance details.
- Appointment: Slot reservation aggregate with type, duration, status,
  queue-state, and waitlist relation.
- Reminder Event: Scheduled outbound communication with channel, send status,
  confirmation response, and retry metadata.
- Insurance Profile: Primary and secondary insurance details with card images,
  soft-validation status, and verification history.
- Clinical Document: Uploaded source file with category, scan result,
  extraction status, and text index metadata.
- Clinical Fact: Normalized medication, allergy, diagnosis, and timeline items
  with confidence and verification flags.
- Coding Decision: ICD-10 and CPT suggestion set with rationale, confidence,
  reviewer action, and finalized outcome.
- Audit Record: Append-only event for authentication, access, override,
  configuration changes, and coding review actions.

## AI Consideration

**Status:** Applicable

**If Not Applicable (Deterministic Project):**
**Rationale:** No `[AI-CANDIDATE]` or `[HYBRID]` tags present in spec.md.
Project follows deterministic architecture.

**If Applicable:** Proceed to AI Requirements section below.

## AI Requirements [CONDITIONAL: Only if AI Consideration = Applicable]
- AIR-001: System MUST perform OCR-assisted extraction of medications,
  allergies, diagnoses, and free-text clinical findings from uploaded
  documents using a hybrid pattern combining model inference and deterministic
  normalization rules.
- AIR-002: System MUST generate no-show risk classification per appointment
  using explainable feature contributions suitable for staff review.
- AIR-003: System MUST produce top-3 ICD-10 and CPT suggestions with explicit
  rationale linked to extracted clinical evidence.
- AIR-004: System MUST provide source citation references for generated coding
  rationale and extracted facts to support auditability.
- AIR-005: System MUST fallback to deterministic manual workflows for intake,
  coding, and extraction verification when model confidence is below configured
  thresholds.
- AIR-006: System MUST maintain AI response latency within 2.5 seconds p95 for
  synchronous suggestion APIs under nominal load.
- AIR-007: System MUST maintain coding suggestion agreement rate above 98
  percent against clinician-reviewed benchmark sets.
- AIR-008: System MUST enforce output schema validation at or above 99 percent
  for extraction payloads and coding recommendation responses.
- AIR-009: System MUST redact direct identifiers from prompts except minimum
  required treatment context and log redaction actions.
- AIR-010: System MUST enforce retrieval access control filters so only
  authorized patient-specific context is used in any AI-assisted reasoning.
- AIR-011: System MUST log prompts, context references, model responses,
  confidence values, and reviewer outcomes with 7-year retention.
- AIR-012: [UNCLEAR] System MUST enforce a maximum token budget per request,
  but the exact token cap and per-role budget policy require product decision.

### AI Architecture Pattern
**Selected Pattern:** Hybrid

## Architecture and Design Decisions
- Decision 1: Use modular layered monolith in Phase 1 with bounded modules
  for Scheduling, Clinical Intelligence, Administration, and Shared Services.
  This satisfies NFR-005 and NFR-011 while minimizing operational overhead.
- Decision 2: Implement asynchronous processing for OCR and extraction through
  queue-backed workers to satisfy NFR-003 and isolate latency from user APIs.
- Decision 3: Keep PostgreSQL as system of record with JSONB support for
  extracted artifacts and pgvector extension for retrieval workloads, aligned
  to DR-001, DR-003, and AIR-004.
- Decision 4: Enforce single authoritative writer per aggregate to prevent
  data races and preserve DR-002 consistency constraints.
- Decision 5: Use API gateway with centralized authentication, rate limiting,
  and observability to satisfy NFR-007, NFR-011, and NFR-012.
- Decision 6: Apply explicit retry, exponential backoff, and circuit-breaker
  policies for SMS, email, and AI provider integrations.
- Decision 7: Keep human-in-the-loop controls mandatory for coding decisions to
  satisfy AIR-003, AIR-005, and compliance obligations from NFR-010.

## Technology Stack
| Layer | Technology | Version | Justification (NFR/DR/AIR) |
|-------|------------|---------|----------------------------|
| Frontend | Angular | 17.x | NFR-001, NFR-009 |
| Mobile | N/A | N/A | NFR-001 (responsive web strategy) |
| Backend | ASP.NET Core Web API | 8.x | NFR-002, NFR-005, NFR-011 |
| Database | PostgreSQL with pgvector | 15.x | DR-001, DR-003, DR-006, AIR-004 |
| AI/ML | Azure OpenAI + pgvector + LiteLLM gateway | 2026 APIs | AIR-001, AIR-006, AIR-010 |
| Testing | xUnit, Playwright, k6 | latest stable | NFR-001, NFR-002, AIR-008 |
| Infrastructure | Docker, Render or Railway, Supabase/Neon | latest stable | NFR-004, NFR-005, DR-006 |
| Security | ASP.NET Identity, OAuth2/OIDC, Vault-managed secrets | latest stable | NFR-007, NFR-008, NFR-012 |
| Deployment | GitHub Actions with gated environments | latest stable | NFR-005, NFR-011 |
| Monitoring | OpenTelemetry, Grafana, Loki, Prometheus | latest stable | NFR-011, NFR-005 |
| Documentation | Markdown docs under .propel context | current | NFR-010, NFR-011 |

### Alternative Technology Options
- Frontend alternatives: React 19 and Vue 3 were considered; Angular selected
  for stronger out-of-box enterprise structure and consistency with stack goals.
- Backend alternatives: Node.js NestJS and Spring Boot were considered;
  ASP.NET Core selected for high throughput and integrated security features.
- Database alternatives: MySQL and MongoDB were considered; PostgreSQL selected
  for relational integrity, JSONB flexibility, and vector extension support.
- AI gateway alternatives: direct SDK and custom gateway were considered;
  LiteLLM-style gateway selected for routing, model abstraction, and telemetry.

### AI Component Stack [CONDITIONAL]
| Component | Technology | Purpose |
|-----------|------------|---------|
| Model Provider | Azure OpenAI GPT-4.1 family | Clinical extraction and coding reasoning |
| Vector Store | pgvector on PostgreSQL | Embedding storage and retrieval |
| AI Gateway | LiteLLM-compatible gateway | Routing, retries, throttling, logging |
| Guardrails | JSON schema validation + policy filters | Output validity and safety controls |

### Technology Decision
| Metric (from NFR/DR/AIR) | Candidate 1 | Candidate 2 | Rationale |
|--------------------------|-------------|-------------|-----------|
| API latency p95 (NFR-002) | ASP.NET Core: 9/10 | NestJS: 7/10 | Lower overhead and stronger typed pipeline favor Candidate 1 |
| Data integrity and retention (DR-001, DR-005) | PostgreSQL: 10/10 | MongoDB: 6/10 | Relational constraints and retention governance favor Candidate 1 |
| AI explainability and control (AIR-003, AIR-010) | Azure OpenAI + gateway: 9/10 | Direct single-provider SDK: 6/10 | Gateway abstraction improves control and fallback strategy |
| Cost on free-tier constraints (Constraint C-01) | Modular monolith: 9/10 | Microservices: 5/10 | Lower operational complexity and cost for Phase 1 |
| Operational observability (NFR-011) | OpenTelemetry stack: 9/10 | Basic platform logs: 5/10 | Distributed tracing and SLO metrics are mandatory |

## Technical Requirements
- TR-001: System MUST implement a modular layered architecture with strict
  boundary flow from presentation to application to data layers, justified by
  NFR-005, NFR-011, and DR-002.
- TR-002: System MUST expose versioned REST APIs under /api/v1 with contract
  validation and backward compatibility policy, justified by NFR-002 and DR-007.
- TR-003: System MUST use PostgreSQL 15 or higher as the primary transactional
  datastore, justified by DR-001, DR-002, and DR-005.
- TR-004: System MUST use Redis-compatible caching for hot slot search and
  profile read acceleration with bounded TTL controls, justified by NFR-001 and
  NFR-002.
- TR-005: System MUST use asynchronous worker processing for OCR and extraction
  jobs with retry policies and dead-letter handling, justified by NFR-003,
  NFR-005, and AIR-001.
- TR-006: System MUST implement centralized identity and authorization with
  role claims and endpoint policy enforcement, justified by NFR-007, NFR-008,
  and DR-008.
- TR-007: System MUST enforce API gateway rate limiting and request shaping
  policies, justified by NFR-012 and NFR-005.
- TR-008: System MUST implement AI orchestration through a provider-agnostic
  gateway with circuit-breaker fallback to deterministic flows, justified by
  AIR-005, AIR-006, and AIR-011.
- TR-009: System MUST emit OpenTelemetry traces, metrics, and structured logs
  for all critical operations and external calls, justified by NFR-011 and
  NFR-005.
- TR-010: [UNCLEAR] System MUST define hosting platform finalization between
  managed PaaS and self-hosted Windows/IIS for production, but environment
  ownership and compliance responsibilities need stakeholder confirmation.

## Technical Constraints & Assumptions
- Constraint: Paid infrastructure is not permitted in Phase 1 baseline rollout.
- Constraint: HIPAA-aligned controls are mandatory before production release.
- Constraint: Staff-mediated check-in remains the only allowed check-in flow.
- Constraint: External provider quotas can affect SMS, email, and AI throughput.
- Assumption: Current user base and traffic profile can be handled with a
  modular monolith without early service decomposition.
- Assumption: Free-tier database and storage plans remain available through
  implementation timeline.
- Assumption: Clinical staff will perform mandatory review for low-confidence
  AI outputs.

## Development Workflow
1. Validate requirement traceability from spec to NFR, DR, AIR, and TR sets.
2. Build architecture foundation modules, contracts, and shared security layer.
3. Implement scheduling and reminder capabilities with observability hooks.
4. Implement document and clinical intelligence pipelines with AI guardrails.
5. Execute verification gates, compliance checks, and release readiness review.
