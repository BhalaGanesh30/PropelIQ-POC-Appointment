---
task_id: task_002
user_story: us_039
epic: EP-005
layer: Backend
status: not-started
effort_hours: 7
---

# Task - task_002

## Requirement Reference

- **User Story**: [us_039] Insurance Verification Report and Export
- **Story Location**: [.propel/context/tasks/EP-005/us_039/us_039.md](.propel/context/tasks/EP-005/us_039/us_039.md)
- **Acceptance Criteria**:
  - AC-1: Given I am authenticated as a staff member, When I navigate to the insurance verification report, Then all patient insurance records are displayed with their validation status (SoftValidated, ValidationFailed, ValidationPending).
  - AC-2: Given the verification report is displayed, When I apply a status filter, Then only records matching the selected status are shown within 500 ms.
  - AC-3: Given I filter the report, When I click "Export PDF," Then the filtered records export as a PDF within 5 seconds with patient name, insurance provider, policy number, and validation status.
  - AC-4: Given I click "Export CSV," When the export is processed, Then a CSV file downloads with the same data fields suitable for import into a billing system.
- **Edge Cases**:
  - Edge Case 1: Report contains thousands of records — server-side pagination on the listing endpoint; export endpoints return all filtered records as a streamed file.
  - Edge Case 2: Patient role attempts access — API returns HTTP 403 via `[Authorize(Roles = "Staff,Admin")]`.

---

## Design References (Backend Task)

| Reference Type | Value |
|----------------|-------|
| **UI Impact** | No |
| **Figma URL** | N/A |
| **Wireframe Status** | N/A |
| **Wireframe Type** | N/A |
| **Wireframe Path/URL** | N/A |
| **Screen Spec** | N/A |
| **UXR Requirements** | N/A |
| **Design Tokens** | N/A |

---

## Applicable Technology Stack

| Layer | Technology | Version |
|-------|------------|---------|
| Frontend | Angular | 17.x |
| Backend | ASP.NET Core Web API | 8.x |
| ORM | Entity Framework Core | 8.x |
| Database | PostgreSQL | 15.x |
| Cache | Redis (StackExchange.Redis) | 2.x |
| PDF Generation | QuestPDF | 2024.x (free, MIT licence) |
| CSV Generation | CsvHelper | 33.x |
| Auth | ASP.NET Core Identity + JWT | 8.x |
| Observability | OpenTelemetry .NET | 1.x |
| AI/ML | N/A | N/A |
| Vector Store | N/A | N/A |
| AI Gateway | N/A | N/A |
| Mobile | N/A | N/A |

---

## AI References (AI Tasks Only)

| Reference Type | Value |
|----------------|-------|
| **AI Impact** | No |
| **AIR Requirements** | N/A |
| **AI Pattern** | N/A |
| **Prompt Template Path** | N/A |
| **Guardrails Config** | N/A |
| **Model Provider** | N/A |

---

## Mobile References (Mobile Tasks Only)

| Reference Type | Value |
|----------------|-------|
| **Mobile Impact** | No |
| **Platform Target** | N/A |
| **Min OS Version** | N/A |
| **Mobile Framework** | N/A |

---

## Task Overview

Implement the insurance verification report API in the `Insurance` module of the ASP.NET Core 8 Web API. This task creates three endpoints under `api/v1/insurance/verification-report`: (1) `GET /` returning a paginated list of insurance verification records with optional status filtering, server-side sorting, and Redis caching (30-second TTL) to meet the 500 ms response target (AC-2, NFR-002); (2) `GET /export/pdf` generating a PDF document of all filtered records (not paginated) using QuestPDF with patient name, insurance provider, policy number, validation status, and date columns formatted for A4 paper (AC-3); and (3) `GET /export/csv` generating a CSV file with the same fields using CsvHelper for billing system import (AC-4). All three endpoints are secured with `[Authorize(Roles = "Staff,Admin")]` — Patient role receives HTTP 403 (Edge Case 2, AC-4 of us_038). The listing endpoint supports server-side pagination for large datasets (Edge Case 1) while export endpoints stream the full filtered result set. The report data is sourced by joining `insurance_profiles` with `patients` and optionally `insurance_validation_results`, decrypting insurance fields via `IEncryptionService` (from US_038/task_001) before returning them in the response.

---

## Dependent Tasks

- **us_037/task_003** — `insurance_profiles` table with `validation_status` and `insurance_validation_results` table must exist.
- **us_038/task_001** — `IEncryptionService` must exist for decrypting policy number and provider name before including in report/export.
- **us_038/task_003** — Encrypted columns on `insurance_profiles` must exist.

---

## Impacted Components

| Component / Module | Action | Notes |
|--------------------|--------|-------|
| `InsuranceReportController` | CREATE | New controller: `GET /api/v1/insurance/verification-report`, `GET .../export/pdf`, `GET .../export/csv` |
| `IInsuranceReportService` | CREATE | Interface for report listing and export generation |
| `InsuranceReportService` | CREATE | Business logic: filtered query, pagination, PDF generation, CSV generation with decryption |
| `VerificationReportEntryDto` | CREATE | DTO: `PatientName`, `ProviderName`, `PolicyNumber`, `ValidationStatus`, `ValidatedAt` |
| `VerificationReportPagedResultDto` | CREATE | DTO: `Entries[]`, `TotalCount`, `Page`, `PageSize` |
| `VerificationReportFilterDto` | CREATE | Query parameters: `Status?`, `Page`, `PageSize`, `SortBy?`, `SortDirection?` |
| `InsurancePdfReportGenerator` | CREATE | QuestPDF document builder for the verification report |
| `InsuranceModule` DI registration | MODIFY | Register report services |
| `Server.csproj` | MODIFY | Add QuestPDF and CsvHelper NuGet packages |

---

## Implementation Plan

1. **Create DTOs** in `Insurance/DTOs/`:
   - `VerificationReportFilterDto` with `ValidationStatus? Status`, `int Page = 1`, `int PageSize = 25`, `string? SortBy`, `string? SortDirection`.
   - `VerificationReportEntryDto` with `Guid PatientId`, `string PatientName`, `string ProviderName`, `string PolicyNumber`, `string ValidationStatus`, `DateTimeOffset ValidatedAt`.
   - `VerificationReportPagedResultDto` wrapping `List<VerificationReportEntryDto> Entries`, `int TotalCount`, `int Page`, `int PageSize`.
2. **Create `IInsuranceReportService`** interface with:
   - `GetPagedReportAsync(VerificationReportFilterDto filter, CancellationToken ct)` returning `Task<VerificationReportPagedResultDto>`.
   - `GeneratePdfAsync(ValidationStatus? statusFilter, CancellationToken ct)` returning `Task<byte[]>`.
   - `GenerateCsvAsync(ValidationStatus? statusFilter, CancellationToken ct)` returning `Task<byte[]>`.
3. **Implement `InsuranceReportService.GetPagedReportAsync`**:
   - Build cache key: `insurance:report:{status}:{page}:{pageSize}:{sortBy}:{sortDir}`. Check Redis `IDistributedCache`. On cache miss:
   - Query `insurance_profiles` joined with `patients` on `patient_id`. Apply optional `WHERE validation_status = @status` filter. Apply server-side sorting via `ORDER BY` with parameterised column. Apply `OFFSET/FETCH` for pagination. Project to DTO.
   - For each entry, decrypt `PolicyNumber` and `ProviderName` via `IEncryptionService.Decrypt()` using stored `key_version`.
   - Cache the page result in Redis with 30-second TTL.
   - Return `VerificationReportPagedResultDto` with `TotalCount` from a separate `COUNT(*)` query.
4. **Implement `InsuranceReportService.GeneratePdfAsync`**:
   - Query all filtered records (no pagination) from `insurance_profiles` joined with `patients`. Decrypt fields.
   - Use `InsurancePdfReportGenerator` (QuestPDF) to generate a PDF document: title "Insurance Verification Report", generated date, filter applied, table with columns: Patient Name, Insurance Provider, Policy Number, Validation Status, Validated Date. Format for A4 paper with margins. Include page numbers.
   - Return the PDF as `byte[]`.
5. **Implement `InsuranceReportService.GenerateCsvAsync`**:
   - Query all filtered records (no pagination). Decrypt fields.
   - Use CsvHelper `CsvWriter` to generate CSV with headers: `PatientName`, `ProviderName`, `PolicyNumber`, `ValidationStatus`, `ValidatedAt`. Write to `MemoryStream`.
   - Return the CSV as `byte[]`.
6. **Create `InsurancePdfReportGenerator`** in `Insurance/Reports/InsurancePdfReportGenerator.cs`:
   - Use QuestPDF fluent API: `Document.Create(container => ...)`. Define page layout (A4, 2cm margins), header with title and date, table with alternating row colours, status column with colour-coded text (green/amber/red), footer with page numbers.
7. **Create `InsuranceReportController`** at route `api/v1/insurance/verification-report`:
   - `GET /` accepts `[FromQuery] VerificationReportFilterDto filter`. Returns `VerificationReportPagedResultDto`. Apply `[Authorize(Roles = "Staff,Admin")]`.
   - `GET /export/pdf` accepts `[FromQuery] ValidationStatus? status`. Returns `FileContentResult` with `application/pdf` content type and `Content-Disposition: attachment`. Apply same authorisation.
   - `GET /export/csv` accepts `[FromQuery] ValidationStatus? status`. Returns `FileContentResult` with `text/csv` content type. Apply same authorisation.
8. **Register DI and NuGet packages**: Add `QuestPDF` and `CsvHelper` to `Server.csproj`. Register `IInsuranceReportService` → `InsuranceReportService` as scoped. Set `QuestPDF.Settings.License = LicenseType.Community`.

---

## Current Project State

```
Server/
├── Modules/
│   ├── Insurance/
│   │   ├── Controllers/
│   │   │   ├── InsuranceController.cs                 ← EXISTS (us_037, us_038)
│   │   │   └── InsuranceReportController.cs           ← CREATE
│   │   ├── Services/
│   │   │   ├── IInsuranceReportService.cs             ← CREATE
│   │   │   ├── InsuranceReportService.cs              ← CREATE
│   │   │   └── [existing services...]
│   │   ├── Reports/
│   │   │   └── InsurancePdfReportGenerator.cs         ← CREATE
│   │   └── DTOs/
│   │       ├── VerificationReportEntryDto.cs          ← CREATE
│   │       ├── VerificationReportPagedResultDto.cs    ← CREATE
│   │       ├── VerificationReportFilterDto.cs         ← CREATE
│   │       └── [existing DTOs...]
│   └── [existing modules...]
├── Server.csproj                                       ← MODIFY (add QuestPDF, CsvHelper)
├── Program.cs                                          ← MODIFY (DI registration)
└── [existing structure...]
```

> Placeholder: Update this tree after dependent tasks are complete and the actual module structure is confirmed.

---

## Expected Changes

| Action | File Path | Description |
|--------|-----------|-------------|
| CREATE | `Server/Modules/Insurance/Controllers/InsuranceReportController.cs` | `GET /api/v1/insurance/verification-report`, `GET .../export/pdf`, `GET .../export/csv` with `[Authorize(Roles="Staff,Admin")]` |
| CREATE | `Server/Modules/Insurance/Services/IInsuranceReportService.cs` | Interface for paged report, PDF export, CSV export |
| CREATE | `Server/Modules/Insurance/Services/InsuranceReportService.cs` | Filtered query with decryption, Redis caching, pagination, PDF and CSV generation |
| CREATE | `Server/Modules/Insurance/Reports/InsurancePdfReportGenerator.cs` | QuestPDF A4 document builder with status-coloured table |
| CREATE | `Server/Modules/Insurance/DTOs/VerificationReportEntryDto.cs` | Per-record response DTO |
| CREATE | `Server/Modules/Insurance/DTOs/VerificationReportPagedResultDto.cs` | Paged wrapper DTO |
| CREATE | `Server/Modules/Insurance/DTOs/VerificationReportFilterDto.cs` | Query parameter DTO with status filter, pagination, sorting |
| MODIFY | `Server/Server.csproj` | Add `QuestPDF` and `CsvHelper` NuGet package references |
| MODIFY | `Server/Program.cs` | Register `IInsuranceReportService` as scoped; set QuestPDF community licence |

---

## External References

- QuestPDF documentation: https://www.questpdf.com/getting-started.html
- QuestPDF table component: https://www.questpdf.com/api-reference/table.html
- CsvHelper documentation: https://joshclose.github.io/CsvHelper/getting-started/
- ASP.NET Core 8 `FileContentResult`: https://learn.microsoft.com/en-us/aspnet/core/mvc/models/file-uploads?view=aspnetcore-8.0
- EF Core 8 pagination with OFFSET/FETCH: https://learn.microsoft.com/en-us/ef/core/querying/pagination
- ASP.NET Core 8 `IDistributedCache` with Redis: https://learn.microsoft.com/en-us/aspnet/core/performance/caching/distributed?view=aspnetcore-8.0
- FR-IP-003: System MUST provide insurance verification reports with status filters and export capability
- NFR-002: API response within 500 ms p95 — enforced by Redis-cached report pages (30s TTL)

---

## Build Commands

```bash
# Restore and build
dotnet restore
dotnet build

# Run API locally
dotnet run --project Server/Server.csproj

# Run tests
dotnet test
```

---

## Implementation Validation Strategy

- [ ] Unit tests pass for `InsuranceReportService.GetPagedReportAsync` (mock EF context, mock `IDistributedCache`, mock `IEncryptionService`)
- [ ] Unit tests pass for `InsuranceReportService.GeneratePdfAsync` (verify PDF byte array is non-empty, contains expected text)
- [ ] Unit tests pass for `InsuranceReportService.GenerateCsvAsync` (parse generated CSV, verify headers and row count)
- [ ] Integration tests pass for `GET /api/v1/insurance/verification-report` returning paginated records
- [ ] Integration tests pass for `GET /api/v1/insurance/verification-report?status=ValidationFailed` returning filtered records
- [ ] Integration tests pass for `GET .../export/pdf` returning `application/pdf` content type
- [ ] Integration tests pass for `GET .../export/csv` returning `text/csv` content type
- [ ] Authorization verified: unauthenticated → `401`; Patient role → `403`; Staff → `200`
- [ ] Report listing response within 500 ms (AC-2, NFR-002) with Redis caching
- [ ] Export endpoints return all filtered records regardless of pagination (Edge Case 1)

---

## Implementation Checklist

- [ ] Create filter, entry, and paged result DTOs with validation defaults (`Page = 1`, `PageSize = 25`)
- [ ] Implement `InsuranceReportService.GetPagedReportAsync` with EF Core filtered query, `IEncryptionService` decryption, and Redis caching (30s TTL)
- [ ] Implement `InsuranceReportService.GeneratePdfAsync` using QuestPDF with A4 layout, status-coloured table, and page numbers
- [ ] Implement `InsuranceReportService.GenerateCsvAsync` using CsvHelper with billing-system-compatible headers
- [ ] Create `InsurancePdfReportGenerator` with QuestPDF fluent API for formatted A4 report
- [ ] Create `InsuranceReportController` with `[Authorize(Roles = "Staff,Admin")]` for listing, PDF export, and CSV export endpoints
- [ ] Add `QuestPDF` (community licence) and `CsvHelper` NuGet packages; register services in DI
- [ ] Export endpoints stream full filtered result set without pagination constraints (Edge Case 1)
