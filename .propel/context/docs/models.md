## Design Modelling

## UML Models Overview
This document provides the visual architecture models for the Unified Patient
Access and Clinical Intelligence Platform. Diagrams are derived from
[spec.md](.propel/context/docs/spec.md) (use cases UC-001 through UC-006) and
[design.md](.propel/context/docs/design.md) (architecture decisions, domain
entities, AI requirements, and technology stack). The document is organized
into architectural views followed by one sequence diagram per use case.

Because the architecture includes hybrid AI workflows, additional AI-assisted
subflow diagrams are included for intake assistance, no-show risk scoring,
clinical extraction, and retrieval-backed coding recommendations.

## Architectural Views

### System Context Diagram
```plantuml
@startuml
left to right direction
skinparam linetype ortho

actor "Patient" as Patient #LightBlue
actor "Staff" as Staff #LightBlue
actor "Clinician" as Clinician #LightBlue
actor "Admin" as Admin #LightBlue

rectangle "Unified Patient Access\n& Clinical Intelligence Platform" as System #LightGreen {
}

component "Email Provider\n(SendGrid)" as Email #LightGray
component "SMS Gateway\n(Twilio)" as SMS #LightGray
component "OCR Engine\n(Tesseract)" as OCR #LightGray
component "AI Model Provider\n(Azure OpenAI)" as AI #LightGray
component "Calendar Service\n(Google Calendar)" as Cal #LightGray

Patient --> System : HTTPS / REST\nBook, upload, confirm
Staff --> System : HTTPS / REST\nQueue, check-in, override
Clinician --> System : HTTPS / REST\nReview, code, verify
Admin --> System : HTTPS / REST\nConfigure, audit, report

System ..> Email : SMTP / API\nSend reminders
System ..> SMS : API\nSend reminders
System ..> OCR : In-process\nExtract text
System ..> AI : HTTPS / REST\nExtraction and coding
System ..> Cal : API\nICS sync
@enduml
```

### Component Architecture Diagram
```mermaid
graph LR
  subgraph "Presentation Layer"
    PP[Patient Portal]:::core
    SP[Staff Portal]:::core
    AP[Admin Portal]:::core
  end

  subgraph "API Gateway"
    GW[Gateway - Auth, Rate Limit, Routing]:::core
  end

  subgraph "Scheduling Module"
    APPT[Appointment Service]:::core
    WL[Waitlist Service]:::core
    REM[Reminder Service]:::core
  end

  subgraph "Clinical Intelligence Module"
    DOC[Document Service]:::core
    EXT[Extraction Service]:::core
    AGG[Aggregation Service]:::core
    COD[Coding Service]:::core
    CONF[Conflict Detection Service]:::core
  end

  subgraph "Shared Services"
    AUTH[Identity and Auth]:::core
    NOTIF[Notification Service]:::core
    AUD[Audit Service]:::core
    INS[Insurance Service]:::core
    CFG[Configuration Service]:::core
  end

  subgraph "AI Orchestration"
    AIGW[AI Gateway - LiteLLM]:::core
    GUARD[Guardrails and Schema Validation]:::core
  end

  subgraph "Data Layer"
    PG[(PostgreSQL + pgvector)]:::data
    REDIS[(Redis Cache)]:::data
    STORE[(Document Storage)]:::data
  end

  PP --> GW
  SP --> GW
  AP --> GW

  GW --> AUTH
  GW --> APPT
  GW --> DOC
  GW --> AGG
  GW --> CFG

  APPT --> WL
  APPT --> REM
  REM --> NOTIF
  WL --> NOTIF

  DOC --> EXT
  EXT --> AIGW
  AGG --> CONF
  COD --> AIGW
  AIGW --> GUARD

  AUTH --> PG
  APPT --> PG
  APPT --> REDIS
  DOC --> STORE
  EXT --> PG
  AGG --> PG
  COD --> PG
  AUD --> PG
  INS --> PG

  classDef core fill:#90ee90
  classDef data fill:#ffffe0
```

### Deployment Architecture Diagram
```plantuml
@startuml
left to right direction
skinparam linetype ortho

cloud "Internet" as internet #LightGray

node "Security Zone" as security #MistyRose {
  component "WAF + Rate Limits" as waf
  component "TLS 1.3 Termination" as tls
  component "Secrets and Keys" as keys
}

node "Shared Services Hub" as hub #LightGreen {
  component "API Gateway" as gw
  component "Identity / Session Service" as identity
  component "Audit + Observability" as obs
  component "AI Gateway + Guardrails" as aigw
}

node "Management Zone" as mgmt #Orange {
  component "CI/CD Controls" as cicd
  component "Health Checks" as health
  component "Metrics / Logs / Traces" as telemetry
}

node "Dev Workload Spoke" as dev #White {
  component "Angular SPA" as devspa
  component "ASP.NET Core API" as devapi
  component "Background Worker" as devworker
}

node "Test Workload Spoke" as test #White {
  component "Angular SPA" as testspa
  component "ASP.NET Core API" as testapi
  component "Background Worker" as testworker
}

node "Prod Workload Spoke" as prod #LightYellow {
  component "Angular SPA" as prodspa
  component "ASP.NET Core API" as prodapi
  component "Background Worker" as prodworker
}

node "Managed Data Services" as data #Yellow {
  database "PostgreSQL 15 + pgvector" as pg
  database "Redis Cache" as redis
  storage "Encrypted Document Store" as docs
  queue "Background Queue" as queue
}

node "External Integrations" as ext #LightGray {
  component "Azure OpenAI" as aoai
  component "Email Provider" as email
  component "SMS Gateway" as sms
  component "Calendar Service" as cal
}

internet --> waf : HTTPS
waf --> tls : Secure ingress
tls --> gw : Routed traffic

gw --> devapi : Internal routing
gw --> testapi : Internal routing
gw --> prodapi : Internal routing
identity --> devapi : Auth context
identity --> testapi : Auth context
identity --> prodapi : Auth context

devapi --> queue : Async jobs
testapi --> queue : Async jobs
prodapi --> queue : Async jobs
devworker --> queue : Dequeue jobs
testworker --> queue : Dequeue jobs
prodworker --> queue : Dequeue jobs

devapi --> pg : TLS
testapi --> pg : TLS
prodapi --> pg : TLS
devworker --> pg : TLS
testworker --> pg : TLS
prodworker --> pg : TLS

devapi --> redis : TLS
testapi --> redis : TLS
prodapi --> redis : TLS
prodapi --> docs : HTTPS
prodworker --> docs : HTTPS

prodapi ..> email : HTTPS / API
prodapi ..> sms : HTTPS / API
prodapi ..> cal : HTTPS / API
prodworker ..> aoai : HTTPS / API
aigw ..> aoai : HTTPS / API

obs --> telemetry : OTLP
health --> prodapi : Restart policy
health --> prodworker : Restart policy
keys --> gw : Secret injection
keys --> pg : Encryption keys
keys --> docs : Encryption keys
@enduml
```

### Data Flow Diagram
```plantuml
@startuml
!define PROCESS rectangle
!define DATASTORE database
!define EXTERNAL component

EXTERNAL "Patient / Staff" as user
EXTERNAL "Clinician" as clinician
EXTERNAL "Admin" as admin

PROCESS "Appointment\nBooking" as booking
PROCESS "Reminder\nEngine" as reminder
PROCESS "Document\nUpload" as upload
PROCESS "OCR and\nExtraction" as ocr
PROCESS "Aggregation\nand Conflict" as agg
PROCESS "Coding\nSuggestion" as coding
PROCESS "Audit\nLogger" as audit

DATASTORE "Appointment DB" as apptdb
DATASTORE "Document Store" as docstore
DATASTORE "Clinical Data DB" as clindb
DATASTORE "Audit Store" as auditdb
DATASTORE "Redis Cache" as cache

EXTERNAL "Email / SMS" as notify
EXTERNAL "Azure OpenAI" as ai

user -> booking : Search and book slots
booking -> apptdb : Persist appointment
booking -> cache : Cache available slots
booking -> reminder : Schedule reminders
reminder -> notify : Send email and SMS
reminder -> apptdb : Update confirmation

user -> upload : Upload documents
upload -> docstore : Store files
upload -> ocr : Trigger processing
ocr -> ai : Request extraction
ai -> ocr : Extracted entities
ocr -> clindb : Persist clinical facts

clinician -> agg : View 360 profile
agg -> clindb : Read clinical data
agg -> docstore : Fetch source docs
agg -> coding : Request code suggestions
coding -> ai : Generate ICD-10 / CPT
ai -> coding : Suggestions with rationale
clinician -> coding : Accept / modify / reject
coding -> clindb : Finalize decisions

admin -> audit : Review logs
audit -> auditdb : Read audit records

booking -> audit : Log booking events
ocr -> audit : Log extraction events
coding -> audit : Log coding decisions
@enduml
```

### Logical Data Model (ERD)
```mermaid
erDiagram
    USER {
        uuid user_id PK
        string email UK
        string password_hash
        enum role
        enum status
        timestamp created_at
        timestamp last_login
    }

    PATIENT {
        uuid patient_id PK
        uuid user_id FK
        string first_name
        string last_name
        date date_of_birth
        string phone
        jsonb contact_preferences
    }

    APPOINTMENT {
        uuid appointment_id PK
        uuid patient_id FK
        timestamp date_time
        enum type
        int duration_minutes
        enum status
        string reason
        timestamp arrived_at
    }

    WAITLIST_ENTRY {
        uuid entry_id PK
        uuid patient_id FK
        uuid preferred_slot_id FK
        timestamp created_at
        timestamp claimed_at
        enum status
    }

    REMINDER_EVENT {
        uuid reminder_id PK
        uuid appointment_id FK
        enum channel
        enum send_status
        timestamp scheduled_at
        timestamp sent_at
        enum confirmation_response
        int retry_count
    }

    INSURANCE_PROFILE {
        uuid insurance_id PK
        uuid patient_id FK
        enum tier
        string provider_name
        string policy_number
        enum validation_status
        string card_image_path
    }

    CLINICAL_DOCUMENT {
        uuid document_id PK
        uuid patient_id FK
        string file_path
        enum category
        enum scan_result
        enum extraction_status
        text extracted_text
        timestamp uploaded_at
    }

    CLINICAL_FACT {
        uuid fact_id PK
        uuid patient_id FK
        uuid document_id FK
        enum fact_type
        string name
        string value
        decimal confidence_score
        boolean verified
        uuid verified_by FK
        timestamp fact_date
    }

    CODING_DECISION {
        uuid decision_id PK
        uuid patient_id FK
        uuid fact_id FK
        string icd10_code
        string cpt_code
        decimal confidence
        text rationale
        enum reviewer_action
        uuid reviewer_id FK
        timestamp decided_at
    }

    AUDIT_RECORD {
        uuid audit_id PK
        uuid user_id FK
        enum event_type
        string entity_type
        uuid entity_id
        jsonb details
        timestamp created_at
    }

    USER ||--o| PATIENT : "has profile"
    PATIENT ||--o{ APPOINTMENT : "books"
    PATIENT ||--o{ WAITLIST_ENTRY : "joins"
    APPOINTMENT ||--o{ REMINDER_EVENT : "triggers"
    PATIENT ||--o{ INSURANCE_PROFILE : "has"
    PATIENT ||--o{ CLINICAL_DOCUMENT : "uploads"
    CLINICAL_DOCUMENT ||--o{ CLINICAL_FACT : "produces"
    PATIENT ||--o{ CLINICAL_FACT : "owns"
    CLINICAL_FACT ||--o{ CODING_DECISION : "maps to"
    USER ||--o{ AUDIT_RECORD : "generates"
```

### AI Architecture - RAG and Hybrid Pipeline
```mermaid
graph LR
  subgraph "Document Ingestion Pipeline"
    UPLOAD[Document Upload]:::core
    SCAN[Malware Scan]:::core
    OCR[OCR - Tesseract]:::core
    CHUNK[Chunking Engine]:::core
    EMBED[Embedding - text-embedding-3-small]:::core
    VECDB[(pgvector Store)]:::data
  end

  subgraph "Query Runtime"
    REQ[Clinician Request]:::actor
    FILTER[ACL Filter - AIR-010]:::core
    RETRIEVE[Top-K Retrieval]:::core
    RERANK[Rerank - MMR]:::core
    GUARD[Guardrails - Schema + PII Redaction]:::core
    LLM[Azure OpenAI GPT-4.1]:::external
    VALIDATE[Output Validation - AIR-008]:::core
    RESP[Suggestion Response]:::core
  end

  UPLOAD --> SCAN --> OCR --> CHUNK --> EMBED --> VECDB

  REQ --> FILTER
  FILTER --> RETRIEVE
  RETRIEVE --> VECDB
  VECDB --> RERANK
  RERANK --> GUARD
  GUARD --> LLM
  LLM --> VALIDATE
  VALIDATE --> RESP

  classDef core fill:#90ee90
  classDef data fill:#ffffe0
  classDef actor fill:#add8e6
  classDef external fill:#d3d3d3
```

### Use Case Sequence Diagrams

#### UC-001: Register and Authenticate User
**Source**: [spec.md#UC-001](.propel/context/docs/spec.md#UC-001)

```mermaid
sequenceDiagram
    participant User as Patient / Staff / Admin
    participant SPA as Angular SPA
    participant GW as API Gateway
    participant Auth as Identity Service
    participant DB as PostgreSQL
    participant Audit as Audit Service

    Note over User,Audit: UC-001 - Register and Authenticate User

    User->>SPA: Enter registration or login details
    SPA->>GW: POST /api/v1/auth/register or /login
    GW->>Auth: Validate credentials
    Auth->>DB: Query user record
    DB-->>Auth: User data or not found

    alt Registration
        Auth->>DB: Create user with role
        DB-->>Auth: Confirm creation
        Auth->>Auth: Send verification email/SMS
    end

    alt Valid credentials
        Auth->>Auth: Generate JWT + refresh token
        Auth->>DB: Store session record
        Auth->>Audit: Log authentication event
        Audit->>DB: Persist audit record
        Auth-->>GW: 200 OK + tokens
        GW-->>SPA: Authenticated session
        SPA-->>User: Dashboard redirect
    else Verification fails
        Auth-->>GW: 401 Unauthorized
        GW-->>SPA: Error with retry option
        SPA-->>User: Display actionable error
    else Lockout threshold
        Auth->>DB: Set lockout timestamp
        Auth->>Audit: Log lockout event
        Auth-->>GW: 429 Account locked
        GW-->>SPA: Lockout message
        SPA-->>User: Account locked for 30 minutes
    end
```

#### UC-002: Search, Book, and Confirm Appointment
**Source**: [spec.md#UC-002](.propel/context/docs/spec.md#UC-002)

```mermaid
sequenceDiagram
    participant Actor as Patient / Staff
    participant SPA as Angular SPA
    participant GW as API Gateway
    participant Appt as Appointment Service
    participant WL as Waitlist Service
    participant Cache as Redis Cache
    participant DB as PostgreSQL
    participant Notif as Notification Service
    participant Email as Email Provider
    participant Cal as Calendar Service

    Note over Actor,Cal: UC-002 - Search, Book, and Confirm Appointment

    Actor->>SPA: Search slots by date, duration, type
    SPA->>GW: GET /api/v1/appointments/slots
    GW->>Appt: Forward search request
    Appt->>Cache: Check cached availability
    Cache-->>Appt: Cached slots or miss
    Appt->>DB: Query available slots
    DB-->>Appt: Eligible slot list
    Appt-->>GW: Return slots + intake form
    GW-->>SPA: Display available slots
    SPA-->>Actor: Show slots and intake form

    Actor->>SPA: Complete intake and confirm booking
    SPA->>GW: POST /api/v1/appointments
    GW->>Appt: Create booking
    Appt->>DB: Reserve slot atomically
    DB-->>Appt: Booking confirmed
    Appt->>Cache: Invalidate slot cache
    Appt->>Notif: Send confirmation
    Notif->>Email: Email with PDF + QR + ICS
    Notif->>Cal: Export ICS attachment
    Appt-->>GW: 201 Created + confirmation
    GW-->>SPA: Booking confirmation
    SPA-->>Actor: Display confirmation with PDF

    alt No slots available
        Appt-->>GW: No availability
        GW-->>SPA: Offer waitlist
        Actor->>SPA: Join preferred-slot waitlist
        SPA->>GW: POST /api/v1/waitlist
        GW->>WL: Create waitlist entry
        WL->>DB: Persist waitlist entry
        WL-->>GW: 201 Waitlist confirmed
        GW-->>SPA: Waitlist confirmation
    end
```

#### UC-003: Manage Reminder and Preferred Slot Workflow
**Source**: [spec.md#UC-003](.propel/context/docs/spec.md#UC-003)

```mermaid
sequenceDiagram
    participant Sched as Reminder Scheduler
    participant Notif as Notification Service
    participant Email as Email Provider
    participant SMS as SMS Gateway
    participant Patient as Patient
    participant GW as API Gateway
    participant Appt as Appointment Service
    participant WL as Waitlist Service
    participant Risk as No-Show Risk Engine
    participant DB as PostgreSQL
    participant Staff as Staff

    Note over Sched,Staff: UC-003 - Manage Reminder and Preferred Slot Workflow

    Sched->>DB: Query upcoming appointments
    DB-->>Sched: Appointments with reminder schedule
    Sched->>Notif: Dispatch reminders (7d, 2d, 1d, 2h)
    Notif->>Email: Send email reminder
    Notif->>SMS: Send SMS reminder

    alt Patient confirms
        Patient->>GW: One-click confirm via link
        GW->>Appt: Update confirmation status
        Appt->>DB: Mark confirmed
    else Patient cancels
        Patient->>GW: Cancel appointment
        GW->>Appt: Process cancellation
        Appt->>DB: Update status to cancelled
        Appt->>WL: Check waitlisted patients
        WL->>DB: Query matching waitlist entries
        DB-->>WL: Eligible patients
        WL->>Notif: Send preferred-slot alert
        Notif->>Patient: Alert with 2-hour claim window
    else No response
        Sched->>Risk: Evaluate no-show risk
        Risk->>DB: Query patient history
        DB-->>Risk: Historical data
        Risk-->>Sched: Risk score (Low/Medium/High)
        Sched->>DB: Update risk indicator
        Sched->>Staff: Flag high-risk on dashboard
    end

    alt Claim window expires
        WL->>WL: Timer expires
        WL->>DB: Release entry
        WL->>Notif: Offer to next eligible patient
    end
```

#### UC-004: Upload and Process Clinical Documents
**Source**: [spec.md#UC-004](.propel/context/docs/spec.md#UC-004)

```mermaid
sequenceDiagram
    participant Actor as Patient / Staff
    participant SPA as Angular SPA
    participant GW as API Gateway
    participant Doc as Document Service
    participant Store as Document Storage
    participant Queue as Background Queue
    participant Worker as Extraction Worker
    participant OCR as OCR Engine
    participant AIGW as AI Gateway
    participant LLM as Azure OpenAI
    participant DB as PostgreSQL
    participant Audit as Audit Service
    participant Clinician as Clinician

    Note over Actor,Clinician: UC-004 - Upload and Process Clinical Documents

    Actor->>SPA: Select and upload document
    SPA->>GW: POST /api/v1/documents (multipart)
    GW->>Doc: Validate file type, size
    Doc->>Doc: Malware scan
    alt Invalid or infected
        Doc-->>GW: 400 Rejected with reason
        GW-->>SPA: Display rejection error
        SPA-->>Actor: Show error message
    end
    Doc->>Store: Persist encrypted file
    Store-->>Doc: Storage path
    Doc->>DB: Create document record (status: processing)
    Doc->>Queue: Enqueue extraction job
    Doc-->>GW: 202 Accepted
    GW-->>SPA: Upload confirmed, processing
    SPA-->>Actor: Show processing status

    Queue->>Worker: Dequeue job
    Worker->>Store: Retrieve document
    Worker->>OCR: Run OCR extraction
    OCR-->>Worker: Raw extracted text
    Worker->>AIGW: Request structured extraction
    AIGW->>LLM: Prompt with redacted context
    LLM-->>AIGW: Structured entities + confidence
    AIGW-->>Worker: Validated extraction output
    Worker->>DB: Persist clinical facts with confidence
    Worker->>DB: Update document status (completed)
    Worker->>Audit: Log extraction event

    alt Low confidence detected
        Worker->>DB: Flag for manual review
    end

    Clinician->>SPA: Open document review
    SPA->>GW: GET /api/v1/documents/{id}
    GW->>Doc: Retrieve document and facts
    Doc->>DB: Query clinical facts
    DB-->>Doc: Facts with confidence scores
    Doc-->>GW: Document + extracted data
    GW-->>SPA: Display for review
    Clinician->>SPA: Edit and verify data
    SPA->>GW: PUT /api/v1/clinical-facts/{id}
    GW->>Doc: Update verified status
    Doc->>DB: Persist with audit trail
    Doc->>Audit: Log verification event
```

#### UC-005: Review Profile, Conflicts, and Coding Suggestions
**Source**: [spec.md#UC-005](.propel/context/docs/spec.md#UC-005)

```mermaid
sequenceDiagram
    participant Clinician as Clinician
    participant SPA as Angular SPA
    participant GW as API Gateway
    participant Agg as Aggregation Service
    participant Conflict as Conflict Detection
    participant Coding as Coding Service
    participant AIGW as AI Gateway
    participant LLM as Azure OpenAI
    participant VecDB as pgvector
    participant DB as PostgreSQL
    participant Audit as Audit Service

    Note over Clinician,Audit: UC-005 - Review Profile, Conflicts, and Coding

    Clinician->>SPA: Open 360-degree patient profile
    SPA->>GW: GET /api/v1/patients/{id}/profile
    GW->>Agg: Build consolidated view
    Agg->>DB: Query medications, allergies, diagnoses
    DB-->>Agg: Clinical facts with sources
    Agg->>DB: Query timeline events
    DB-->>Agg: Chronological entries
    Agg-->>GW: Unified profile (<3s)
    GW-->>SPA: Render profile view
    SPA-->>Clinician: Display timeline, meds, allergies, diagnoses

    Clinician->>SPA: Request conflict check
    SPA->>GW: GET /api/v1/patients/{id}/conflicts
    GW->>Conflict: Evaluate interactions
    Conflict->>DB: Query active medications and allergies
    DB-->>Conflict: Current clinical data
    Conflict->>Conflict: Run drug-drug and drug-allergy rules
    Conflict-->>GW: Conflict alerts with severity
    GW-->>SPA: Display conflict warnings
    alt Critical conflict
        SPA-->>Clinician: Require explicit acknowledgment
        Clinician->>SPA: Acknowledge conflict
        SPA->>GW: POST /api/v1/conflicts/{id}/acknowledge
        GW->>Audit: Log acknowledgment
    end

    Clinician->>SPA: Request coding suggestions
    SPA->>GW: GET /api/v1/patients/{id}/codes
    GW->>Coding: Generate suggestions
    Coding->>VecDB: Retrieve relevant clinical context
    VecDB-->>Coding: Top-K chunks
    Coding->>AIGW: Request ICD-10 and CPT mapping
    AIGW->>LLM: Prompt with context and guardrails
    LLM-->>AIGW: Top 3 ICD-10 + CPT + rationale
    AIGW-->>Coding: Validated suggestions
    Coding-->>GW: Suggestions with confidence and rationale
    GW-->>SPA: Display coding suggestions
    SPA-->>Clinician: Show codes with reasoning

    alt Accept
        Clinician->>SPA: Accept suggestion
        SPA->>GW: POST /api/v1/coding-decisions
        GW->>Coding: Finalize accepted codes
        Coding->>DB: Persist decision (accepted)
        Coding->>Audit: Log coding decision
    else Modify
        Clinician->>SPA: Modify and submit
        SPA->>GW: POST /api/v1/coding-decisions (modified)
        GW->>Coding: Finalize modified codes
        Coding->>DB: Persist decision (modified)
        Coding->>Audit: Log coding decision
    else Reject
        Clinician->>SPA: Reject suggestion
        SPA->>GW: POST /api/v1/coding-decisions (rejected)
        GW->>Coding: Record rejection
        Coding->>DB: Persist decision (rejected)
        Coding->>Audit: Log coding decision
    end
```

#### UC-006: Administer Platform and Compliance Reporting
**Source**: [spec.md#UC-006](.propel/context/docs/spec.md#UC-006)

```mermaid
sequenceDiagram
    participant Admin as Admin
    participant SPA as Angular SPA
    participant GW as API Gateway
    participant Config as Configuration Service
    participant UserMgmt as User Management
    participant Audit as Audit Service
    participant Report as Report Generator
    participant DB as PostgreSQL

    Note over Admin,DB: UC-006 - Administer Platform and Compliance Reporting

    Admin->>SPA: Update scheduling configuration
    SPA->>GW: PUT /api/v1/admin/config
    GW->>Config: Validate and apply settings
    Config->>Config: Validate configuration rules
    alt Invalid configuration
        Config-->>GW: 400 Validation errors
        GW-->>SPA: Display specific errors
        SPA-->>Admin: Show what needs correction
    end
    Config->>DB: Persist versioned configuration
    Config->>Audit: Log configuration change
    Config-->>GW: 200 Updated
    GW-->>SPA: Confirm update
    SPA-->>Admin: Display success

    Admin->>SPA: Manage users
    SPA->>GW: GET /api/v1/admin/users
    GW->>UserMgmt: List users with filters
    UserMgmt->>DB: Query users
    DB-->>UserMgmt: User list
    UserMgmt-->>GW: Paginated user data
    GW-->>SPA: Display user list
    Admin->>SPA: Activate, deactivate, or bulk action
    SPA->>GW: PATCH /api/v1/admin/users
    GW->>UserMgmt: Apply changes
    UserMgmt->>DB: Update user records
    UserMgmt->>Audit: Log user management action
    UserMgmt-->>GW: 200 Updated
    GW-->>SPA: Confirm changes

    Admin->>SPA: Review audit logs
    SPA->>GW: GET /api/v1/admin/audit-logs
    GW->>Audit: Query logs with filters
    Audit->>DB: Read audit records
    DB-->>Audit: Filtered log entries
    Audit-->>GW: Paginated audit data
    GW-->>SPA: Display audit logs

    Admin->>SPA: Generate compliance report
    SPA->>GW: POST /api/v1/admin/reports
    GW->>Report: Generate HIPAA report
    Report->>DB: Aggregate compliance data
    DB-->>Report: Aggregated metrics
    alt Report exceeds threshold
        Report->>Report: Schedule async job
        Report-->>GW: 202 Job scheduled
        GW-->>SPA: Report generating
    else Quick report
        Report-->>GW: 200 Report PDF
        GW-->>SPA: Download report
        SPA-->>Admin: Display and download report
    end
```

### AI-Assisted Sequence Diagrams

#### UC-002 AI Subflow: AI-Assisted Intake Capture
**Source**: [spec.md#UC-002](.propel/context/docs/spec.md#UC-002)

```mermaid
sequenceDiagram
    participant Actor as Patient / Staff
    participant SPA as Angular SPA
    participant GW as API Gateway
    participant Intake as Intake Orchestrator
    participant Guard as AI Guardrails
    participant LLM as Azure OpenAI
    participant DB as PostgreSQL

    Note over Actor,DB: UC-002 AI Subflow - AI-Assisted Intake Capture

    Actor->>SPA: Request AI assistance for intake
    SPA->>GW: Submit draft intake data
    GW->>Intake: Create assistance request
    Intake->>Guard: Redact unnecessary identifiers
    Guard->>LLM: Structured intake completion prompt
    LLM-->>Guard: Suggested fields and rationale
    Guard-->>Intake: Schema-validated response

    alt Confidence above threshold
        Intake->>DB: Autosave suggested intake values
        Intake-->>GW: Assisted draft ready
    else Confidence below threshold
        Intake-->>GW: Manual workflow required
    end

    GW-->>SPA: Render assisted or manual intake path
```

#### UC-003 AI Subflow: No-Show Risk Scoring
**Source**: [spec.md#UC-003](.propel/context/docs/spec.md#UC-003)

```mermaid
sequenceDiagram
    participant Reminder as Reminder Service
    participant Risk as Risk Orchestrator
    participant Guard as AI Guardrails
    participant LLM as Azure OpenAI
    participant DB as PostgreSQL
    participant Audit as Audit Service

    Note over Reminder,Audit: UC-003 AI Subflow - No-Show Risk Scoring

    Reminder->>Risk: Request risk evaluation
    Risk->>DB: Read appointment history and reminder outcomes
    DB-->>Risk: Feature set
    Risk->>Guard: Redact direct identifiers and enforce schema
    Guard->>LLM: Risk scoring prompt
    LLM-->>Guard: Risk label and feature contributions
    Guard-->>Risk: Validated explainable result
    Risk->>DB: Persist score and explanation
    Risk->>Audit: Log prompt, confidence, and outcome
    Risk-->>Reminder: Low / Medium / High risk
```

#### UC-004 AI Subflow: OCR and Clinical Extraction
**Source**: [spec.md#UC-004](.propel/context/docs/spec.md#UC-004)

```mermaid
sequenceDiagram
    participant Worker as Extraction Worker
    participant Store as Document Storage
    participant AIGW as AI Gateway
    participant Guard as AI Guardrails
    participant LLM as Azure OpenAI
    participant DB as PostgreSQL
    participant Audit as Audit Service

    Note over Worker,Audit: UC-004 AI Subflow - OCR and Clinical Extraction

    Worker->>Store: Retrieve uploaded document
    Store-->>Worker: Source file
    Worker->>AIGW: Submit extraction request
    AIGW->>Guard: Apply ACL, redaction, and schema rules
    Guard->>LLM: OCR and extraction prompt
    LLM-->>Guard: Structured facts with confidence
    Guard-->>AIGW: Validated extraction payload

    alt Confidence above threshold
        AIGW->>DB: Persist facts and citations
    else Confidence below threshold
        AIGW->>DB: Persist draft facts with review flag
    end

    AIGW->>Audit: Log prompt metadata and response state
    AIGW-->>Worker: Extraction completion status
```

#### UC-005 AI Subflow: Retrieval-Backed Coding Suggestions
**Source**: [spec.md#UC-005](.propel/context/docs/spec.md#UC-005)

```mermaid
sequenceDiagram
    participant Coding as Coding Service
    participant ACL as Access Filter
    participant Vector as pgvector
    participant Guard as AI Guardrails
    participant LLM as Azure OpenAI
    participant DB as PostgreSQL
    participant Audit as Audit Service

    Note over Coding,Audit: UC-005 AI Subflow - Retrieval-Backed Coding Suggestions

    Coding->>ACL: Request authorized patient context
    ACL->>Vector: Retrieve patient-specific evidence only
    Vector-->>ACL: Ranked evidence chunks
    ACL-->>Coding: Authorized context set
    Coding->>Guard: Assemble coding prompt
    Guard->>LLM: ICD-10 and CPT suggestion request
    LLM-->>Guard: Top-3 suggestions and rationale
    Guard->>Guard: Validate schema and attach citations

    alt Confidence above threshold
        Guard-->>Coding: Suggestions, rationale, citations
    else Confidence below threshold
        Guard-->>Coding: Manual coding fallback required
    end

    Coding->>DB: Store pending or suggested coding state
    Coding->>Audit: Log context references and reviewer state
```
