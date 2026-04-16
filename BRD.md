# Business Requirements Document (BRD)
# Unified Patient Access & Clinical Intelligence Platform


## Document Control & Approval

| Role | Name | Date | Status |
|------|------|------|--------|
| Executive Sponsor | _______________ | | ☐ Pending |
| Business Owner | _______________ | | ☐ Pending |
| Technical Lead | _______________ | | ☐ Pending |
| Compliance Officer | _______________ | | ☐ Pending |

---

# PART 1: EXECUTIVE SUMMARY & STRATEGIC CONTEXT

## 1. Executive Summary

### 1.1 Vision Statement
> *"Deliver a seamless, intelligent healthcare platform that transforms patient engagement through intuitive scheduling while empowering clinical staff with AI-assisted data aggregation and medical coding—built on trust, transparency, and compliance."*

### 1.2 Value Delivery Matrix

| Stakeholder | Key Value | Measurable Outcome |
|-------------|-----------|-------------------|
| **Patients** | 3-minute booking, smart reminders, preferred slot alerts | 70% online booking adoption |
| **Admin Staff** | Single dashboard, automated workflows | 60% productivity increase |
| **Clinical Staff** | 2-minute data prep, AI-assisted coding | 90% time savings |
| **Organization** | Reduced no-shows, fewer claim denials | $375K annual value |

### 1.3 Key Outcomes

| Metric | Current | Target | Impact |
|--------|---------|--------|--------|
| No-Show Rate | 15% | <5% | $150K+ revenue recovery |
| Clinical Prep Time | 20 min | 2 min | 90% efficiency gain |
| Coding Accuracy | 85% | >98% | 25% fewer claim denials |
| Staff Time/Appointment | 25 min | 10 min | 60% productivity increase |

---

## 2. Business Problem & Market Opportunity

### 2.1 Problem Statement

**Challenge 1: Scheduling Fragmentation**
- High no-show rates (15%) → Revenue loss ($150K+/year)
- Phone-only booking → Staff overhead, patient frustration
- No preferred slot management → Missed optimization opportunities

**Challenge 2: Clinical Data Silos**
- Manual data extraction → 20+ minutes per patient
- Unstructured documents → Missed information, errors
- No conflict detection → Patient safety risks
- Manual coding → Claim denials, revenue leakage

### 2.2 Market Gap

| Existing Solutions | Limitation | Our Differentiation |
|-------------------|------------|---------------------|
| Standalone Booking | No clinical context | Integrated intelligence |
| EHR Systems | Complex, expensive | Patient-first, zero cost |
| AI Coding Tools | Black-box decisions | Trust-First transparency |
| Document Management | No extraction | NLP-powered aggregation |

### 2.3 ROI Projection (3-Year)

| Value Driver | Annual Savings |
|--------------|----------------|
| No-show reduction | $150K |
| Staff productivity | $100K |
| Claim denial reduction | $75K |
| Clinical prep savings | $50K |
| **Total Annual Value** | **$375K** |

---

# PART 2: SOLUTION ARCHITECTURE

## 3. Architecture Overview

```
┌─────────────────────────────────────────────────────────────────┐
│                    PRESENTATION LAYER                            │
│  ┌──────────────┐  ┌──────────────┐  ┌──────────────┐          │
│  │Patient Portal│  │ Staff Portal │  │ Admin Portal │          │
│  └──────┬───────┘  └──────┬───────┘  └──────┬───────┘          │
└─────────┼─────────────────┼─────────────────┼───────────────────┘
          └─────────────────┼─────────────────┘
                            ▼
┌─────────────────────────────────────────────────────────────────┐
│                      API GATEWAY                                 │
│         [Auth] [Rate Limiting] [Routing] [Logging]              │
└───────────────────────────┬─────────────────────────────────────┘
                            ▼
┌─────────────────────────────────────────────────────────────────┐
│                   APPLICATION LAYER                              │
│  ┌────────────────────────────────────────────────────────────┐ │
│  │ SCHEDULING: Appointment | Waitlist | Reminder | Calendar   │ │
│  └────────────────────────────────────────────────────────────┘ │
│  ┌────────────────────────────────────────────────────────────┐ │
│  │ CLINICAL: Document | Aggregation | Conflict | Coding       │ │
│  └────────────────────────────────────────────────────────────┘ │
│  ┌────────────────────────────────────────────────────────────┐ │
│  │ SHARED: User | Notification | Audit | Insurance            │ │
│  └────────────────────────────────────────────────────────────┘ │
└───────────────────────────┬─────────────────────────────────────┘
                            ▼
┌─────────────────────────────────────────────────────────────────┐
│                      DATA LAYER                                  │
│  [PostgreSQL] [Redis Cache] [Document Storage] [Audit Store]   │
└─────────────────────────────────────────────────────────────────┘
```

## 4. Key Architectural Decisions

| ID | Decision | Rationale |
|----|----------|-----------|
| AD-001 | Angular 17+ Frontend | Enterprise-grade, TypeScript |
| AD-002 | .NET 8 Backend | Performance, C# type safety |
| AD-003 | PostgreSQL Database | Open-source, JSONB support |
| AD-004 | Free-tier Hosting | Zero infrastructure cost |
| AD-005 | Monolithic API (Phase 1) | Simplicity, faster delivery |
| AD-006 | Trust-First AI | Transparent reasoning |

---

# PART 3: STAKEHOLDERS & USER PERSONAS

## 5. Stakeholder Matrix

| Stakeholder | Interest | Influence | Engagement |
|-------------|----------|-----------|------------|
| Executive Sponsor | ROI, compliance | High | Manage Closely |
| IT Department | Integration, security | High | Keep Satisfied |
| Admin Staff | Workflow efficiency | Medium | Keep Informed |
| Clinical Staff | Data accuracy | Medium | Keep Informed |
| Patients | Ease of use | Low | Keep Informed |

## 6. User Personas

### Patient: Sarah Thompson (45, Marketing Manager)
- **Goals:** Quick booking, timely reminders, easy document upload
- **Pain Points:** Phone-only systems, forgotten appointments
- **Success:** Book in <3 min, mobile document upload

### Staff: Maria Garcia (35, Front Desk Coordinator)
- **Goals:** Efficient scheduling, reduced phone time
- **Pain Points:** Multiple systems, manual reminders
- **Success:** Single dashboard, 50% call reduction

### Clinical: Dr. James Wilson (52, Primary Care Physician)
- **Goals:** Quick patient review, trustworthy AI
- **Pain Points:** 20+ min document review, black-box AI
- **Success:** 2-min consolidated view, transparent reasoning

### Admin: Robert Chen (48, Practice Manager)
- **Goals:** Operational metrics, compliance readiness
- **Pain Points:** Limited visibility, manual audit compilation
- **Success:** Real-time dashboard, automated audit logs

---

# PART 4: FUNCTIONAL REQUIREMENTS

## 7. Requirements Summary

| Module | Count | Critical | High | Medium |
|--------|-------|----------|------|--------|
| User Management | 5 | 2 | 3 | 0 |
| Appointment Scheduling | 7 | 2 | 4 | 1 |
| Reminders & Notifications | 4 | 1 | 1 | 2 |
| Staff Operations | 6 | 2 | 4 | 0 |
| Insurance Pre-Check | 3 | 0 | 2 | 1 |
| Document Management | 4 | 2 | 1 | 1 |
| Clinical Data Aggregation | 5 | 3 | 1 | 1 |
| Medical Coding | 4 | 3 | 1 | 0 |
| Audit & Compliance | 3 | 2 | 1 | 0 |
| Administration | 4 | 0 | 3 | 1 |
| **TOTAL** | **45** | **17** | **21** | **7** |

## 8. Detailed Functional Requirements

### 8.1 User Management (FR-UM)

| ID | Title | Priority | Key Acceptance Criteria |
|----|-------|----------|------------------------|
| FR-UM-001 | Patient Self-Registration | Critical | Email/phone registration, verification, <30s completion |
| FR-UM-002 | Role-Based Access Control | Critical | 3 roles (Patient/Staff/Admin), audit logging |
| FR-UM-003 | Staff Account Management | High | Admin creates via email invite, deactivation support |
| FR-UM-004 | Session Management | High | 15-min timeout, 2-min warning, single session |
| FR-UM-005 | Password Management | High | Reset via email, 5 failed = 30-min lockout |

### 8.2 Appointment Scheduling (FR-AS)

| ID | Title | Priority | Key Acceptance Criteria |
|----|-------|----------|------------------------|
| FR-AS-001 | Search and Booking | Critical | 30-day search, 15/30/60-min slots, <5s confirmation |
| FR-AS-002 | Flexible Intake | Critical | AI or manual option, auto-save, editable |
| FR-AS-003 | Confirmation | High | Email <1 min, PDF with QR, .ics attachment |
| FR-AS-004 | Modification | High | Reschedule/cancel 24h before, staff override |
| FR-AS-005 | Preferred Slot Waitlist | High | Monitor cancellations, 2-hour claim window, auto-swap |
| FR-AS-006 | Calendar Sync | Medium | Google Calendar (free API), .ics download |
| FR-AS-007 | History | Medium | Filter by status/date, export PDF |

### 8.3 Reminders & Notifications (FR-RN)

| ID | Title | Priority | Key Acceptance Criteria |
|----|-------|----------|------------------------|
| FR-RN-001 | Automated Reminders | Critical | 7d/2d/1d/2h, SMS+email, one-click confirm |
| FR-RN-002 | No-Show Risk Assessment | Medium | Score: Low/Medium/High, staff flagging |
| FR-RN-003 | Notification Preferences | Medium | Toggle SMS/email, timing preferences |
| FR-RN-004 | Preferred Slot Alert | High | Immediate notification, 2-hour window |

### 8.4 Staff Operations (FR-SO)

| ID | Title | Priority | Key Acceptance Criteria |
|----|-------|----------|------------------------|
| FR-SO-001 | Queue Dashboard | Critical | Real-time, status colors, wait time estimates |
| FR-SO-002 | Arrival Management | Critical | Staff-only check-in, one-click marking |
| FR-SO-003 | Walk-In Management | High | Quick-add, queue insertion, convert to registered |
| FR-SO-004 | Appointment Override | Medium | Override restrictions, requires reason, audit log |
| FR-SO-005 | Staff Booking | High | Book for patients, skip verification |
| FR-SO-006 | Daily Schedule View | High | Calendar view, drag-drop, print |

### 8.5 Insurance Pre-Check (FR-IP)

| ID | Title | Priority | Key Acceptance Criteria |
|----|-------|----------|------------------------|
| FR-IP-001 | Soft Validation | High | Format check, dummy DB, doesn't block booking |
| FR-IP-002 | Insurance Storage | High | Primary/secondary, card images, encrypted |
| FR-IP-003 | Verification Report | Medium | Status list, filter, export |

### 8.6 Document Management (FR-DM)

| ID | Title | Priority | Key Acceptance Criteria |
|----|-------|----------|------------------------|
| FR-DM-001 | Document Upload | Critical | PDF/JPG/PNG/TIFF, 10MB max, virus scan |
| FR-DM-002 | Document Processing | Critical | OCR, <2 min, status tracking |
| FR-DM-003 | Document Viewing | High | In-browser, zoom/rotate, search |
| FR-DM-004 | Document Organization | Medium | Categorize, rename, soft delete |

### 8.7 Clinical Data Aggregation (FR-CA)

| ID | Title | Priority | Key Acceptance Criteria |
|----|-------|----------|------------------------|
| FR-CA-001 | Data Extraction | Critical | NLP extraction, >97% accuracy, confidence scores |
| FR-CA-002 | 360° Patient Profile | Critical | Unified view, <3s load, source linking |
| FR-CA-003 | Conflict Detection | Critical | Drug-drug, drug-allergy, severity levels |
| FR-CA-004 | Data Editing | High | Edit/add/verify, audit trail |
| FR-CA-005 | Timeline View | Medium | Chronological, filterable, printable |

### 8.8 Medical Coding (FR-MC)

| ID | Title | Priority | Key Acceptance Criteria |
|----|-------|----------|------------------------|
| FR-MC-001 | ICD-10 Mapping | Critical | Top 3 suggestions, confidence, **reasoning shown** |
| FR-MC-002 | CPT Mapping | Critical | Procedure mapping, E/M codes, **reasoning shown** |
| FR-MC-003 | Code Review | Critical | Accept/modify/reject, **Trust-First transparency** |
| FR-MC-004 | Code Search | High | By code/keyword, autocomplete, favorites |

### 8.9 Audit & Compliance (FR-AC)

| ID | Title | Priority | Key Acceptance Criteria |
|----|-------|----------|------------------------|
| FR-AC-001 | Audit Trail | Critical | Immutable, 7-year retention, admin-only |
| FR-AC-002 | Access Logging | Critical | Every view logged, patient can request |
| FR-AC-003 | Compliance Reports | High | HIPAA reports, scheduled delivery |

### 8.10 Administration (FR-AD)

| ID | Title | Priority | Key Acceptance Criteria |
|----|-------|----------|------------------------|
| FR-AD-001 | System Configuration | High | Slots, reminders, timeout, templates |
| FR-AD-002 | Operational Dashboard | High | KPIs, charts, export, scheduled reports |
| FR-AD-003 | User Management | High | CRUD, bulk operations, activity history |
| FR-AD-004 | Notification Templates | Medium | HTML/SMS editing, preview, versioning |

---

# PART 5: NON-FUNCTIONAL REQUIREMENTS

## 9. NFR Summary

### 9.1 Performance

| ID | Requirement | Target |
|----|-------------|--------|
| NFR-P-001 | Page load | <3 seconds (95th percentile) |
| NFR-P-002 | API response | <500ms (95th percentile) |
| NFR-P-003 | Document processing | <2 minutes |
| NFR-P-004 | Concurrent users | 500 without degradation |

### 9.2 Availability

| ID | Requirement | Target |
|----|-------------|--------|
| NFR-A-001 | Uptime | 99.9% |
| NFR-A-002 | RTO | 4 hours |
| NFR-A-003 | RPO | 1 hour |
| NFR-A-004 | Backup frequency | Every 6 hours |

### 9.3 Security

| ID | Requirement | Target |
|----|-------------|--------|
| NFR-SEC-001 | Encryption at rest | AES-256 |
| NFR-SEC-002 | Encryption in transit | TLS 1.3 |
| NFR-SEC-003 | Session timeout | 15 minutes |
| NFR-SEC-004 | HIPAA compliance | Full Security Rule |
| NFR-SEC-005 | Rate limiting | 100 req/min/user |

### 9.4 Usability

| ID | Requirement | Target |
|----|-------------|--------|
| NFR-U-001 | Mobile responsive | Full functionality |
| NFR-U-002 | Accessibility | WCAG 2.1 Level AA |
| NFR-U-003 | Browser support | Chrome, Firefox, Safari, Edge (latest 2) |
| NFR-U-004 | Training time | 2 hours |

---

# PART 6: TECHNOLOGY STACK & DATA ARCHITECTURE

## 10. Technology Stack

| Layer | Technology | Justification |
|-------|------------|---------------|
| **Frontend** | Angular 17+ | Enterprise-grade, TypeScript |
| **UI Components** | Angular Material | Accessibility, Material Design |
| **Backend** | .NET 8 Web API | Performance, C# |
| **Database** | PostgreSQL 15+ | Open-source, JSONB |
| **Cache** | Upstash Redis | Free tier, serverless |
| **Storage** | Cloudflare R2 / Local FS | Free tier, S3-compatible |
| **OCR** | Tesseract.js | Open-source |
| **PDF** | PDFKit / jsPDF | Open-source |
| **Email** | SendGrid (free tier) | 100/day free |
| **SMS** | Twilio (trial) | Free credits |

## 11. Hosting Architecture

| Component | Platform | Free Tier Limits |
|-----------|----------|------------------|
| Frontend | Netlify/Vercel | 100GB bandwidth |
| Backend | Railway/Render | 500 hours/month |
| Database | Supabase/Neon | 500MB |
| Cache | Upstash Redis | 10K requests/day |
| Storage | Cloudflare R2 | 10GB |

## 12. Data Model (Key Entities)

### User
```
user_id (UUID, PK) | email (UNIQUE) | password_hash | role (ENUM) | status (ENUM) | created_at
```

### Appointment
```
appt_id (UUID, PK) | patient_id (FK) | date_time | type | status | reason | arrived_at
```

### Document
```
document_id (UUID, PK) | patient_id (FK) | file_path | category | status | extracted_text
```

### Clinical Data
```
Medication: med_id | patient_id | name | dosage | confidence | verified
Diagnosis: diagnosis_id | patient_id | icd10_code | confidence | verified
Allergy: allergy_id | patient_id | allergen | severity | verified
```

## 13. Data Retention

| Data Type | Retention | Archive |
|-----------|-----------|---------|
| Patient records | 10 years | Cold storage after 3 years |
| Audit logs | 7 years | Immutable storage |
| Documents | 10 years | Cold storage after 3 years |
| Session logs | 90 days | Auto-delete |

---

# PART 7: SCOPE, RISKS & GOVERNANCE

## 14. Scope Definition

### 14.1 In-Scope (Phase 1)

| Category | Features |
|----------|----------|
| **User Roles** | Patient, Staff, Admin |
| **Booking** | Search, book, reschedule, cancel, waitlist |
| **Reminders** | SMS + Email, configurable timing |
| **Staff Ops** | Queue, arrivals, walk-ins |
| **Insurance** | Soft validation (dummy DB) |
| **Documents** | Upload, OCR, viewing |
| **Clinical** | 360° profile, conflict detection |
| **Coding** | ICD-10, CPT with Trust-First AI |
| **Audit** | Immutable logs, access tracking |

### 14.2 Out-of-Scope (Phase 1)

| Item | Reason | Future Phase |
|------|--------|--------------|
| Provider logins | Complexity | Phase 2 |
| Payment gateway | PCI compliance | Phase 2 |
| Family profiles | Data model complexity | Phase 2 |
| Patient self-check-in | Business requirement | Not planned |
| Direct EHR integration | HL7/FHIR required | Phase 3 |
| Claims submission | Clearinghouse needed | Phase 3 |
| Paid cloud | Budget constraint | Future |

## 15. Risk Register

| ID | Risk | Probability | Impact | Mitigation |
|----|------|-------------|--------|------------|
| R-01 | Free tier limits exceeded | Medium | High | Monitor usage, have paid fallback |
| R-02 | OCR accuracy below threshold | Medium | Medium | Human review workflow |
| R-03 | AI coding <98% accuracy | Low | High | Mandatory human review |
| R-04 | HIPAA compliance gaps | Low | Critical | Security audit, legal review |
| R-05 | Low patient adoption | Medium | High | UX optimization, staff promotion |
| R-06 | Data breach | Low | Critical | Encryption, access controls |
| R-07 | Scope creep | High | Medium | Strict change control |

## 16. Assumptions & Constraints

### Assumptions
- Users have modern browsers (Chrome, Firefox, Safari, Edge)
- Stable internet connectivity
- Free tier services remain available
- OCR accuracy sufficient for clinical documents
- Single-tenant deployment acceptable

### Constraints
- **C-01:** No paid cloud infrastructure
- **C-02:** HIPAA compliance required
- **C-03:** Free/open-source tools only
- **C-04:** Staff-controlled check-in only
- **C-05:** Windows Server/IIS for self-hosted

---

# PART 8: SUCCESS METRICS & GOVERNANCE

## 17. Key Performance Indicators

### Operational Efficiency
| KPI | Baseline | Target | Measurement |
|-----|----------|--------|-------------|
| No-show rate | 15% | <5% | No-shows / Total appointments |
| Staff time/appointment | 25 min | 10 min | Booking to check-in |
| Clinical prep time | 20 min | 2 min | Upload to unified view |

### Platform Adoption
| KPI | Target | Measurement |
|-----|--------|-------------|
| Patient dashboard creation | 80% | Registrations / New patients |
| Online booking adoption | 70% | Online / Total bookings |
| Document upload compliance | 90% | Pre-visit uploads |

### Clinical Accuracy
| KPI | Target | Measurement |
|-----|--------|-------------|
| AI-human coding agreement | >98% | Matched / Total reviewed |
| Conflict detection accuracy | >95% | True positives / Total |
| Data extraction accuracy | >97% | Correct / Total extractions |

## 18. Governance Framework

### Change Control Process
1. Submit change request with impact assessment
2. Review by Change Advisory Board
3. Prioritization against backlog
4. Approval by stakeholders
5. Implementation and validation

### Document Review Cycle
- **Frequency:** Quarterly
- **Next Review:** July 15, 2026
- **Owner:** Solution Architecture Team

---

# PART 9: APPENDICES

## Appendix A: Epic/Story Mapping

```
EPIC 1: User Authentication (6 stories)
EPIC 2: Appointment Scheduling (10 stories)
EPIC 3: Notifications & Reminders (6 stories)
EPIC 4: Staff Operations (6 stories)
EPIC 5: Insurance Management (3 stories)
EPIC 6: Document Management (4 stories)
EPIC 7: Clinical Data Aggregation (5 stories)
EPIC 8: Medical Coding (4 stories)
EPIC 9: Audit & Compliance (3 stories)
EPIC 10: Administration (4 stories)
EPIC 11: Infrastructure & DevOps (6 stories)
```

## Appendix B: API Endpoints Overview

```yaml
# Authentication
POST /api/v1/auth/register
POST /api/v1/auth/login
POST /api/v1/auth/refresh

# Appointments
GET  /api/v1/appointments/slots
POST /api/v1/appointments
PUT  /api/v1/appointments/{id}
POST /api/v1/appointments/{id}/arrive

# Documents
POST /api/v1/documents
GET  /api/v1/documents/{id}

# Clinical
GET  /api/v1/patients/{id}/profile
GET  /api/v1/patients/{id}/conflicts
GET  /api/v1/patients/{id}/codes

# Admin
GET  /api/v1/admin/users
GET  /api/v1/admin/audit-logs
```

## Appendix C: Glossary

| Term | Definition |
|------|------------|
| 360° Patient Profile | Unified view aggregated from multiple documents |
| Trust-First AI | AI with transparent reasoning for suggestions |
| Dynamic Slot Swap | Auto-reschedule when preferred slot opens |
| Soft Check | Validation that warns but doesn't block |
| No-Show Risk Score | Predictive likelihood of missing appointment |

## Appendix D: HIPAA Compliance Checklist

- [ ] Risk Analysis conducted
- [ ] Access Authorization procedures
- [ ] Unique User Identification
- [ ] Automatic Logoff (15 minutes)
- [ ] Encryption at rest and in transit
- [ ] Audit Controls implemented
- [ ] Transmission Security (TLS)

---

## Document Approval Signatures

| Role | Name | Signature | Date |
|------|------|-----------|------|
| Project Sponsor | | | |
| Business Owner | | | |
| Technical Lead | | | |
| Compliance Officer | | | |
| QA Lead | | | |

---

**Revision History**

| Version | Date | Author | Changes |
|---------|------|--------|---------|
| 1.0 | Apr 10, 2026 | Architecture Team | Initial draft |
| 2.0 | Apr 15, 2026 | Architecture Team | Optimized consolidation |

---

*End of Business Requirements Document*
