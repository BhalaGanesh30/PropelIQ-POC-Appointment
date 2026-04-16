## Epic - Unified Patient Access and Clinical Intelligence Platform

## Epic Summary Table

| Epic ID | Epic Title | Mapped Requirement IDs |
|---------|------------|------------------------|
| EP-TECH | Project Foundation and Infrastructure Scaffolding | TR-001, TR-002, TR-003, TR-004, TR-005, TR-006, TR-007, TR-008, TR-009, NFR-005, NFR-011 |
| EP-DATA | Core Data Layer and Persistence Foundation | DR-001, DR-002, DR-003, DR-004, DR-005, DR-006, DR-007, DR-008 |
| EP-001 | User Authentication and Access Control | FR-UM-001, FR-UM-002, FR-UM-003, FR-UM-004, FR-UM-005, NFR-007, NFR-008, NFR-012 |
| EP-002 | Appointment Scheduling and Calendar Sync | FR-AS-001, FR-AS-002, FR-AS-003, FR-AS-004, FR-AS-005, FR-AS-006, FR-AS-007 |
| EP-003 | Reminders, Notifications, and Waitlist Automation | FR-RN-001, FR-RN-002, FR-RN-003, FR-RN-004, NFR-001, NFR-002 |
| EP-004 | Staff Operations and Queue Management | FR-SO-001, FR-SO-002, FR-SO-003, FR-SO-004, FR-SO-005, FR-SO-006 |
| EP-005 | Insurance Pre-Check and Validation | FR-IP-001, FR-IP-002, FR-IP-003 |
| EP-006 | Document Upload and Processing | FR-DM-001, FR-DM-002, FR-DM-003, FR-DM-004, NFR-003 |
| EP-007 | Clinical Data Aggregation and Conflict Detection | FR-CA-001, FR-CA-002, FR-CA-003, FR-CA-004, FR-CA-005, NFR-004 |
| EP-008 | AI-Assisted Medical Coding | FR-MC-001, FR-MC-002, FR-MC-003, FR-MC-004, AIR-001, AIR-002, AIR-003, AIR-004, AIR-005, AIR-006, AIR-007, AIR-008 |
| EP-009 | AI Safety, Observability, and Operational Controls | AIR-009, AIR-010, AIR-011, NFR-010 |
| EP-010 | Audit Trail and Compliance Reporting | FR-AC-001, FR-AC-002, FR-AC-003, NFR-006 |
| EP-011 | Platform Administration and Configuration | FR-AD-001, FR-AD-002, FR-AD-003, FR-AD-004, NFR-009 |

## Epic Description

### EP-TECH: Project Foundation and Infrastructure Scaffolding
**Business Value**: Enables all subsequent development by establishing the
project foundation, toolchain, deployment pipeline, and base architectural
scaffolding required for every feature epic.

**Description**: Bootstrap the greenfield project with Angular 17 frontend,
ASP.NET Core 8 backend, PostgreSQL 15 with pgvector database, Redis cache
layer, Docker containerization, CI/CD pipeline, observability stack, and AI
gateway integration scaffold. Establish modular layered architecture with
bounded modules for Scheduling, Clinical Intelligence, Administration, and
Shared Services.

**UI Impact**: Yes

**Screen References**: N/A

**Key Deliverables**:
- Angular 17 SPA project scaffold with Material UI and routing shell
- ASP.NET Core 8 Web API project with modular layer structure
- PostgreSQL database provisioning with pgvector extension
- Redis cache integration scaffold
- Docker compose for local development
- GitHub Actions CI/CD pipeline with build, test, and deploy stages
- OpenTelemetry instrumentation baseline
- LiteLLM AI gateway configuration scaffold
- API gateway with authentication middleware skeleton
- Rate limiting and request shaping middleware

**Dependent EPICs**:
- None

---

### EP-DATA: Core Data Layer and Persistence Foundation
**Business Value**: Enables data operations for all feature epics requiring
persistence by establishing entity models, relationships, integrity rules,
migration tooling, backup policies, and access control at the data layer.

**Description**: Implement all domain entities from design.md including User,
Patient, Appointment, Waitlist Entry, Reminder Event, Insurance Profile,
Clinical Document, Clinical Fact, Coding Decision, and Audit Record. Configure
referential integrity, JSONB columns for flexible storage, append-only audit
tables, role-aware data access policies, automated backup scheduling, and
schema migration tooling with zero-downtime rollout support.

**UI Impact**: No

**Screen References**: N/A

**Key Deliverables**:
- Entity Framework Core model classes for all domain entities
- Database migration scripts with seed and mock data
- Foreign key and unique constraint definitions
- JSONB column configuration for contact preferences and audit details
- Append-only audit table with write-restriction policies
- Automated backup configuration (6-hour interval)
- Point-in-time recovery verification
- Schema migration tooling with backward-compatible rollout support
- Role-aware data access policies for PHI column isolation

**Dependent EPICs**:
- EP-TECH - Foundational - Requires project scaffold and database provisioning

---

### EP-001: User Authentication and Access Control
**Business Value**: Establishes trusted identity management for all platform
users, enabling secure role-based access, session governance, and audit-ready
authentication events critical for HIPAA compliance.

**Description**: Implement patient self-registration with email and phone
verification, role-based access control for Patient, Staff, and Admin roles,
staff account invitation and lifecycle management, session timeout with
inactivity warning, single-session enforcement, password reset, and account
lockout policies. All authentication and authorization events are recorded in
the audit trail.

**UI Impact**: Yes

**Screen References**: N/A

**Key Deliverables**:
- Patient registration with email and phone verification flow
- JWT-based authentication with refresh token support
- Role-based authorization policies for three roles
- Staff invitation and account lifecycle management API
- Session timeout (15 min) with 2-minute warning and single-session enforcement
- Password reset by email workflow
- Account lockout after 5 failed attempts (30-minute lock)
- TLS 1.3 enforcement and AES-256 encryption for credentials
- Rate limiting per user (100 req/min)

**Dependent EPICs**:
- EP-TECH - Foundational - Requires auth infrastructure and middleware
- EP-DATA - Foundational - Requires User entity and session storage

---

### EP-002: Appointment Scheduling and Calendar Sync
**Business Value**: Delivers the core patient-facing booking experience that
directly drives online adoption (70% target) and enables staff-assisted
booking, reducing phone overhead and staff time per appointment.

**Description**: Implement appointment slot search within a 30-day window with
15, 30, and 60 minute durations, AI-assisted or manual intake capture with
autosave, booking confirmation with PDF, QR code, and ICS attachment,
reschedule and cancel with 24-hour policy and staff override, preferred-slot
waitlist with 2-hour claim window, Google Calendar sync via ICS, and
appointment history with filters and PDF export.

**UI Impact**: Yes

**Screen References**: N/A

**Key Deliverables**:
- Slot search API with date, duration, and type filters
- Slot availability caching in Redis
- Intake form with AI-assisted and manual modes and autosave
- Booking creation with atomic slot reservation
- Confirmation delivery (email with PDF, QR, ICS) under 1 minute
- Reschedule and cancel API with 24-hour policy enforcement
- Staff override for scheduling constraints
- Preferred-slot waitlist with monitoring and 2-hour claim window
- Google Calendar ICS export integration
- Appointment history with status and date filters and PDF export

**Dependent EPICs**:
- EP-TECH - Foundational - Requires API and caching infrastructure
- EP-DATA - Foundational - Requires Appointment and Waitlist entities

---

### EP-003: Reminders, Notifications, and Waitlist Automation
**Business Value**: Directly addresses no-show reduction from 15% to less than
5%, representing over $150K annual revenue recovery through proactive
multi-channel reminders and intelligent waitlist slot reallocation.

**Description**: Implement automated reminder scheduling at 7-day, 2-day,
1-day, and 2-hour intervals before appointments, multi-channel delivery via
email (SendGrid) and SMS (Twilio), one-click patient confirmation actions,
no-show risk scoring with staff-facing indicators, patient notification
preferences management, and preferred-slot alert dispatch with claim-window
countdown.

**UI Impact**: Yes

**Screen References**: N/A

**Key Deliverables**:
- Reminder scheduler with configurable cadence (7d, 2d, 1d, 2h)
- SendGrid email integration for reminders and alerts
- Twilio SMS integration for reminders and alerts
- One-click confirmation and cancellation action links
- No-show risk scoring engine (Low, Medium, High)
- Staff dashboard risk indicators
- Patient notification preference management (channel and timing)
- Preferred-slot alert with 2-hour claim window countdown
- Retry and dead-letter handling for failed deliveries

**Dependent EPICs**:
- EP-TECH - Foundational - Requires notification infrastructure
- EP-DATA - Foundational - Requires Reminder Event entity

---

### EP-004: Staff Operations and Queue Management
**Business Value**: Improves operational throughput by centralizing queue
management, arrival workflows, and walk-in handling into a single dashboard,
targeting 60% staff productivity increase.

**Description**: Implement real-time queue dashboard with status color-coding
and wait-time estimates, staff-only arrival check-in with one-click state
transitions, walk-in creation with queue insertion and patient registration
conversion, scheduling override with mandatory reason capture and audit,
staff-assisted booking, and daily schedule view with drag-and-drop and print.

**UI Impact**: Yes

**Screen References**: N/A

**Key Deliverables**:
- Real-time queue dashboard with WebSocket or polling updates
- Status color-coding (waiting, in-progress, completed, no-show)
- Wait-time estimation algorithm
- Staff-only arrival check-in with one-click transitions
- Walk-in creation, queue insertion, and registration conversion
- Scheduling override with reason capture and audit logging
- Staff-assisted booking without patient verification
- Daily schedule calendar view with drag-and-drop
- Print-friendly schedule rendering

**Dependent EPICs**:
- EP-TECH - Foundational - Requires API and real-time infrastructure
- EP-DATA - Foundational - Requires Appointment entity with queue state

---

### EP-005: Insurance Pre-Check and Validation
**Business Value**: Reduces claim processing friction by validating insurance
details early in the booking flow without blocking appointment completion,
improving downstream billing accuracy.

**Description**: Implement insurance soft validation against format rules and
reference database without blocking booking, secure storage of primary and
secondary insurance details and card images with AES-256 encryption, and
insurance verification reporting with status filters and export.

**UI Impact**: Yes

**Screen References**: N/A

**Key Deliverables**:
- Insurance format validation engine (policy number, provider patterns)
- Dummy reference database for soft validation
- Primary and secondary insurance detail storage
- Card image upload and encrypted storage
- Insurance verification status report with filters
- Report export capability (PDF, CSV)

**Dependent EPICs**:
- EP-TECH - Foundational - Requires API and storage infrastructure
- EP-DATA - Foundational - Requires Insurance Profile entity

---

### EP-006: Document Upload and Processing
**Business Value**: Enables clinical preparation path by transforming uploaded
documents into searchable, structured data, reducing clinical prep time from
20 minutes toward the 2-minute target.

**Description**: Implement document upload accepting PDF, JPG, PNG, and TIFF up
to 10 MB with malware scanning, asynchronous OCR processing via Tesseract with
status tracking and completion under 2 minutes, in-browser document viewing
with zoom, rotate, and full-text search, and document categorization, rename,
and soft-delete operations.

**UI Impact**: Yes

**Screen References**: N/A

**Key Deliverables**:
- Document upload API with file type and size validation
- Malware scanning integration before persistence
- Encrypted document storage in Cloudflare R2
- Background OCR worker using Tesseract with queue-based processing
- Processing status tracking (queued, processing, completed, failed)
- In-browser document viewer with zoom, rotate, and text search
- Document categorization, rename, and soft-delete APIs
- Retry and dead-letter handling for failed OCR jobs

**Dependent EPICs**:
- EP-TECH - Foundational - Requires background worker and storage infrastructure
- EP-DATA - Foundational - Requires Clinical Document entity

---

### EP-007: Clinical Data Aggregation and Conflict Detection
**Business Value**: Delivers the 360-degree patient profile that consolidates
data from multiple documents into a unified view, enabling conflict detection
that directly impacts patient safety and coding accuracy.

**Description**: Implement NLP-based clinical entity extraction from processed
documents with confidence scoring, unified 360-degree patient profile view
loading in under 3 seconds with source traceability, drug-drug and drug-allergy
conflict detection with severity classification and mandatory clinician
acknowledgment, authorized data editing and verification with immutable audit
trail, and chronological timeline view with filter and print support.

**UI Impact**: Yes

**Screen References**: N/A

**Key Deliverables**:
- Clinical entity extraction service with confidence scoring
- 360-degree patient profile aggregation API (<3s load time)
- Source document traceability links in profile view
- Drug-drug and drug-allergy conflict detection rules engine
- Severity classification (low, moderate, high, critical)
- Mandatory clinician acknowledgment for critical conflicts
- Data editing and verification workflows with audit trail
- Chronological timeline view with category and date filters
- Print-friendly profile and timeline rendering

**Dependent EPICs**:
- EP-TECH - Foundational - Requires API and AI gateway infrastructure
- EP-DATA - Foundational - Requires Clinical Fact entity

---

### EP-008: AI-Assisted Medical Coding
**Business Value**: Reduces claim denials by 25% through Trust-First AI coding
suggestions with transparent rationale, targeting over 98% AI-human agreement
rate while maintaining mandatory human oversight.

**Description**: Implement ICD-10 and CPT code suggestion generation with top-3
ranked results, confidence scores, and explainable rationale linked to clinical
evidence. Enforce mandatory human review with accept, modify, or reject
decisions. Provide code search with autocomplete and favorites. Integrate with
AI gateway for OCR extraction, no-show risk classification, and coding
reasoning with schema validation, latency controls, and fallback to
deterministic flows.

**UI Impact**: Yes

**Screen References**: N/A

**Key Deliverables**:
- ICD-10 suggestion API with top-3 ranked codes and rationale
- CPT and E/M mapping suggestion API with rationale
- Confidence scoring and threshold-based gating
- Trust-First transparency: reasoning shown for every suggestion
- Accept, modify, reject decision workflow with audit logging
- Code search API with keyword, code lookup, autocomplete, and favorites
- AI gateway integration for extraction and coding requests
- Schema validation for AI outputs (>=99% validity)
- Latency enforcement (<=2.5s p95 for suggestion APIs)
- Circuit breaker fallback to manual coding on provider failure
- No-show risk classification model integration
- Coding agreement rate monitoring (>=98% target)

**Dependent EPICs**:
- EP-TECH - Foundational - Requires AI gateway and API infrastructure
- EP-DATA - Foundational - Requires Coding Decision and Clinical Fact entities

---

### EP-009: AI Safety, Observability, and Operational Controls
**Business Value**: Ensures AI operations meet compliance and safety standards
by enforcing PII redaction, access control filtering, comprehensive prompt and
response logging, and audit retention for all AI-assisted workflows.

**Description**: Implement PII redaction from prompts before model invocation,
retrieval access control filters ensuring patient-specific context isolation,
comprehensive logging of prompts, context references, model responses,
confidence values, and reviewer outcomes with 7-year retention, and immutable
audit evidence for all AI-related access and decision events.

**UI Impact**: No

**Screen References**: N/A

**Key Deliverables**:
- PII redaction pipeline for AI prompts
- Retrieval ACL filter enforcing patient-specific context boundaries
- AI prompt and response logging with structured metadata
- 7-year retention policy for AI audit records
- Immutable audit storage for AI access and coding decisions
- Redaction action logging for compliance verification

**Dependent EPICs**:
- EP-TECH - Foundational - Requires observability and AI gateway infrastructure
- EP-DATA - Foundational - Requires Audit Record entity

---

### EP-010: Audit Trail and Compliance Reporting
**Business Value**: Fulfills HIPAA audit requirements and enables patient
disclosure workflows, providing organizational compliance readiness and
reducing manual audit compilation effort.

**Description**: Implement immutable audit trail with 7-year retention and
restricted administrative access, comprehensive access logging for all patient
data views with patient disclosure request support, scheduled HIPAA compliance
report generation, and disaster recovery alignment with 4-hour RTO and 1-hour
RPO targets.

**UI Impact**: Yes

**Screen References**: N/A

**Key Deliverables**:
- Immutable append-only audit trail storage
- 7-year retention policy enforcement
- Admin-only audit log access with filtering and pagination
- Patient data view logging (every access recorded)
- Patient disclosure request response workflow
- Scheduled HIPAA compliance report generation
- Report export and distribution (PDF, email)
- Disaster recovery validation (4h RTO, 1h RPO)

**Dependent EPICs**:
- EP-TECH - Foundational - Requires observability infrastructure
- EP-DATA - Foundational - Requires Audit Record entity

---

### EP-011: Platform Administration and Configuration
**Business Value**: Empowers practice managers with operational visibility and
self-service configuration, reducing dependence on technical staff for routine
policy changes and enabling data-driven decisions through KPI dashboards.

**Description**: Implement admin configuration for slot templates, reminder
rules, session policies, and communication templates. Provide operational KPI
dashboards with exportable charts and scheduled distribution. Support user
lifecycle administration with bulk actions and activity history. Implement
versioned HTML and SMS template management with preview. Ensure WCAG 2.1 AA
accessibility across all admin interfaces.

**UI Impact**: Yes

**Screen References**: N/A

**Key Deliverables**:
- System configuration management (slots, reminders, sessions, templates)
- Configuration validation and versioned persistence
- Operational KPI dashboard with charts (no-show rate, utilization, wait time)
- Chart export (PNG, PDF) and scheduled email distribution
- User CRUD with bulk activate, deactivate, and role assignment
- User activity history view
- HTML and SMS notification template editor with preview
- Template versioning and rollback support
- WCAG 2.1 AA compliance across admin portal

**Dependent EPICs**:
- EP-TECH - Foundational - Requires admin portal scaffold
- EP-DATA - Foundational - Requires User entity and configuration storage

---

## Backlog Refinement Required

The following requirements are tagged [UNCLEAR] and are excluded from epic
mapping until clarified:

| Requirement ID | Description | Clarification Needed |
|----------------|-------------|----------------------|
| TR-010 | Hosting platform finalization between managed PaaS and self-hosted Windows/IIS | Who owns the production environment? What are compliance responsibilities for each option? |
| AIR-012 | Maximum token budget per AI request | What is the exact token cap? Should budgets differ by role or request type? |
