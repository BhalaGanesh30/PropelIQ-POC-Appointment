# Task - TASK_001

## Requirement Reference

- User Story: us_009
- Story Location: .propel/context/tasks/EP-DATA/us_009/us_009.md
- Acceptance Criteria:
  - AC-1: Given the solution is built, When `dotnet ef migrations add InitialSchema` is executed, Then a migration is generated covering all entities: User, Patient, Appointment, WaitlistEntry, ReminderEvent, InsuranceProfile, ClinicalDocument, ClinicalFact, CodingDecision, and AuditRecord.
  - AC-2: Given the migration is applied, When the schema is inspected, Then every entity table has a globally unique identifier primary key (UUID/GUID), explicit foreign-key columns, and appropriate unique constraints.
  - AC-3: Given entities requiring flexible storage are defined, When JSONB columns are configured (e.g., contact preferences, audit details), Then EF Core JSONB mappings serialize and deserialize correctly in integration tests.
- Edge Case:
  - How does the system handle entity model changes mid-sprint? New migration is added additively; old migration is not altered to preserve zero-downtime rollout support per DR-007.

## Design References (Frontend Tasks Only)

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

## Applicable Technology Stack

| Layer | Technology | Version |
|-------|------------|---------|
| Frontend | N/A | N/A |
| Backend | ASP.NET Core Web API | 8.x |
| Database | PostgreSQL with pgvector | 15.x |
| Library | Microsoft.EntityFrameworkCore | 8.x |
| Library | Npgsql.EntityFrameworkCore.PostgreSQL | 8.x |
| Library | EFCore.NamingConventions | 8.x |
| AI/ML | N/A | N/A |
| Vector Store | N/A | N/A |
| AI Gateway | N/A | N/A |
| Mobile | N/A | N/A |

**Note**: All code, and libraries, MUST be compatible with versions above.

## AI References (AI Tasks Only)

| Reference Type | Value |
|----------------|-------|
| **AI Impact** | No |
| **AIR Requirements** | N/A |
| **AI Pattern** | N/A |
| **Prompt Template Path** | N/A |
| **Guardrails Config** | N/A |
| **Model Provider** | N/A |

## Mobile References (Mobile Tasks Only)

| Reference Type | Value |
|----------------|-------|
| **Mobile Impact** | No |
| **Platform Target** | N/A |
| **Min OS Version** | N/A |
| **Mobile Framework** | N/A |

## Task Overview

Define all ten domain entity classes as EF Core models — `User`, `Patient`, `Appointment`, `WaitlistEntry`, `ReminderEvent`, `InsuranceProfile`, `ClinicalDocument`, `ClinicalFact`, `CodingDecision`, and `AuditRecord` — distributed across their owning bounded module Domain projects. Register all entities via `DbSet<T>` properties in `AppDbContext` and configure them using the EF Core Fluent API: `Guid` primary keys (PostgreSQL `uuid` type), explicit foreign-key columns with cascade delete or restrict rules as appropriate, unique constraints, and JSONB column mappings for complex properties such as `ContactPreferences`, `InsuranceDetails`, and `AuditDetails`. All entity type configurations are isolated in `IEntityTypeConfiguration<T>` classes using snake_case naming conventions (via `EFCore.NamingConventions`) to align with PostgreSQL conventions. This task directly enables the `dotnet ef migrations add InitialSchema` command in task_002.

## Dependent Tasks

- US_003 task_002 (requires EF Core registered with `AppDbContext` and Npgsql provider)

## Impacted Components

- New: `server/src/SharedKernel/Domain/` — base `EntityBase.cs` (Guid Id, timestamps)
- New: `server/src/Scheduling.Domain/Entities/Appointment.cs`
- New: `server/src/Scheduling.Domain/Entities/WaitlistEntry.cs`
- New: `server/src/Scheduling.Domain/Entities/ReminderEvent.cs`
- New: `server/src/Administration.Domain/Entities/User.cs`
- New: `server/src/Administration.Domain/Entities/Patient.cs`
- New: `server/src/Administration.Domain/Entities/InsuranceProfile.cs`
- New: `server/src/ClinicalIntelligence.Domain/Entities/ClinicalDocument.cs`
- New: `server/src/ClinicalIntelligence.Domain/Entities/ClinicalFact.cs`
- New: `server/src/ClinicalIntelligence.Domain/Entities/CodingDecision.cs`
- New: `server/src/SharedServices.Domain/Entities/AuditRecord.cs`
- New: `server/src/*/Infrastructure/Persistence/Configurations/` — one `IEntityTypeConfiguration<T>` per entity
- Modify: `server/src/SharedServices.Infrastructure/Persistence/AppDbContext.cs` — add `DbSet<T>` registrations

## Implementation Plan

1. **Create `EntityBase.cs`** in SharedKernel as the base class for all domain entities. All entities inherit from this to satisfy DR-001 (UUID PKs) and DR-002 (audit timestamps):

```csharp
namespace PropelIQ.SharedKernel.Domain;

public abstract class EntityBase
{
    public Guid Id { get; init; } = Guid.NewGuid();
    public DateTimeOffset CreatedAt { get; init; } = DateTimeOffset.UtcNow;
    public DateTimeOffset UpdatedAt { get; set; } = DateTimeOffset.UtcNow;
}
```

2. **Define Administration domain entities** (`User`, `Patient`, `InsuranceProfile`):

```csharp
// User.cs — Authentication principal (Administration.Domain)
public sealed class User : EntityBase
{
    public required string Email { get; set; }
    public required string PasswordHash { get; set; }
    public required string Role { get; set; }           // Staff | Clinician | Admin | Patient
    public string? FirstName { get; set; }
    public string? LastName { get; set; }
    public bool IsActive { get; set; } = true;
    public DateTimeOffset? LastLoginAt { get; set; }
    public Patient? PatientProfile { get; set; }        // Navigation (nullable for staff users)
}

// Patient.cs — Demographic profile (Administration.Domain)
public sealed class Patient : EntityBase
{
    public required Guid UserId { get; set; }
    public required string FirstName { get; set; }
    public required string LastName { get; set; }
    public required DateOnly DateOfBirth { get; set; }
    public required string MRN { get; set; }            // Unique patient identifier
    public ContactPreferences ContactPreferences { get; set; } = new(); // JSONB
    public User User { get; set; } = null!;
    public ICollection<Appointment> Appointments { get; set; } = [];
    public ICollection<InsuranceProfile> InsuranceProfiles { get; set; } = [];
    public ICollection<ClinicalDocument> ClinicalDocuments { get; set; } = [];
}

// ContactPreferences.cs — JSONB value object
public sealed class ContactPreferences
{
    public bool SmsEnabled { get; set; } = true;
    public bool EmailEnabled { get; set; } = true;
    public string PreferredLanguage { get; set; } = "en";
    public string? PreferredPhone { get; set; }
}

// InsuranceProfile.cs (Administration.Domain)
public sealed class InsuranceProfile : EntityBase
{
    public required Guid PatientId { get; set; }
    public required string PayerName { get; set; }
    public required string MemberId { get; set; }
    public bool IsPrimary { get; set; }
    public string VerificationStatus { get; set; } = "Pending";
    public Patient Patient { get; set; } = null!;
}
```

3. **Define Scheduling domain entities** (`Appointment`, `WaitlistEntry`, `ReminderEvent`):

```csharp
// Appointment.cs (Scheduling.Domain)
public sealed class Appointment : EntityBase
{
    public required Guid PatientId { get; set; }
    public required Guid StaffUserId { get; set; }
    public required DateTimeOffset ScheduledAt { get; set; }
    public required int DurationMinutes { get; set; }
    public required string AppointmentType { get; set; }
    public string Status { get; set; } = "Scheduled";  // Scheduled|Arrived|Completed|Cancelled
    public string QueueState { get; set; } = "NotQueued";
    public Patient Patient { get; set; } = null!;
    public User StaffUser { get; set; } = null!;
    public WaitlistEntry? WaitlistEntry { get; set; }
    public ICollection<ReminderEvent> ReminderEvents { get; set; } = [];
}

// WaitlistEntry.cs (Scheduling.Domain)
public sealed class WaitlistEntry : EntityBase
{
    public required Guid PatientId { get; set; }
    public required Guid? AppointmentId { get; set; }
    public required int Priority { get; set; }
    public string Status { get; set; } = "Active";
    public DateTimeOffset? OfferedAt { get; set; }
    public Patient Patient { get; set; } = null!;
    public Appointment? Appointment { get; set; }
}

// ReminderEvent.cs (Scheduling.Domain)
public sealed class ReminderEvent : EntityBase
{
    public required Guid AppointmentId { get; set; }
    public required string Channel { get; set; }        // SMS | Email
    public string SendStatus { get; set; } = "Pending";
    public string? ConfirmationResponse { get; set; }
    public int RetryCount { get; set; } = 0;
    public DateTimeOffset? SentAt { get; set; }
    public Appointment Appointment { get; set; } = null!;
}
```

4. **Define ClinicalIntelligence domain entities** (`ClinicalDocument`, `ClinicalFact`, `CodingDecision`):

```csharp
// ClinicalDocument.cs (ClinicalIntelligence.Domain)
public sealed class ClinicalDocument : EntityBase
{
    public required Guid PatientId { get; set; }
    public required string FileName { get; set; }
    public required string Category { get; set; }       // Referral|Lab|Imaging|Other
    public string ExtractionStatus { get; set; } = "Pending";
    public string? StoragePath { get; set; }
    public Patient Patient { get; set; } = null!;
    public ICollection<ClinicalFact> ClinicalFacts { get; set; } = [];
}

// ClinicalFact.cs (ClinicalIntelligence.Domain) — DR-003: confidence, source ref, verification
public sealed class ClinicalFact : EntityBase
{
    public required Guid DocumentId { get; set; }
    public required string FactType { get; set; }       // Medication|Allergy|Diagnosis|Finding
    public required string Value { get; set; }
    public decimal ConfidenceScore { get; set; }
    public string VerificationState { get; set; } = "Unverified";
    public Guid? LastReviewedByUserId { get; set; }
    public DateTimeOffset? LastReviewedAt { get; set; }
    public ClinicalDocument Document { get; set; } = null!;
}

// CodingDecision.cs (ClinicalIntelligence.Domain)
public sealed class CodingDecision : EntityBase
{
    public required Guid PatientId { get; set; }
    public required Guid DocumentId { get; set; }
    public required string CodeType { get; set; }       // ICD10 | CPT
    public required string SuggestedCode { get; set; }
    public string? Rationale { get; set; }
    public decimal ConfidenceScore { get; set; }
    public string ReviewerAction { get; set; } = "Pending"; // Accepted|Rejected|Modified
    public string? FinalizedCode { get; set; }
    public Guid? ReviewedByUserId { get; set; }
    public Patient Patient { get; set; } = null!;
    public ClinicalDocument Document { get; set; } = null!;
}
```

5. **Define SharedServices domain entity** (`AuditRecord`) — append-only per DR-005:

```csharp
// AuditRecord.cs (SharedServices.Domain) — Append-only; no UpdatedAt needed
public sealed class AuditRecord
{
    public Guid Id { get; init; } = Guid.NewGuid();
    public required string EventType { get; init; }     // Auth|Access|Override|Coding|Config
    public required Guid ActorUserId { get; init; }
    public Guid? TargetEntityId { get; init; }
    public required string TargetEntityType { get; init; }
    public required DateTimeOffset OccurredAt { get; init; }
    public AuditDetails Details { get; init; } = new(); // JSONB
}

// AuditDetails.cs — JSONB value object for audit metadata
public sealed class AuditDetails
{
    public string? IpAddress { get; set; }
    public string? UserAgent { get; set; }
    public string? ChangeDescription { get; set; }
    public Dictionary<string, object?> Metadata { get; set; } = new();
}
```

6. **Create `IEntityTypeConfiguration<T>` classes** for each entity, placed in the Infrastructure layer of each module. Key configuration rules:
   - All Guid PKs use `.HasColumnType("uuid")` and `.ValueGeneratedOnAdd()` (Npgsql generates `gen_random_uuid()`)
   - JSONB columns use `.HasColumnType("jsonb")` on complex property columns
   - `AuditRecord` configures `UpdatedAt` column as omitted and the table as append-only via a database trigger check (documented in SQL comment)
   - `Patient.MRN` has `.IsRequired().HasMaxLength(50)` and `.HasIndex(p => p.MRN).IsUnique()`
   - `User.Email` has `.IsRequired().HasMaxLength(254)` and `.HasIndex(u => u.Email).IsUnique()`

Example configuration for `Patient`:
```csharp
public sealed class PatientConfiguration : IEntityTypeConfiguration<Patient>
{
    public void Configure(EntityTypeBuilder<Patient> builder)
    {
        builder.HasKey(p => p.Id);
        builder.Property(p => p.Id).HasColumnType("uuid").ValueGeneratedOnAdd();
        builder.Property(p => p.MRN).IsRequired().HasMaxLength(50);
        builder.HasIndex(p => p.MRN).IsUnique();

        // AC-3: JSONB column mapping
        builder.OwnsOne(p => p.ContactPreferences, cp =>
        {
            cp.ToJson();
        });

        builder.HasOne(p => p.User)
            .WithOne(u => u.PatientProfile)
            .HasForeignKey<Patient>(p => p.UserId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
```

7. **Register all entities in `AppDbContext.cs`** with `UseSnakeCaseNamingConvention()` and apply all `IEntityTypeConfiguration<T>` via `modelBuilder.ApplyConfigurationsFromAssembly(...)`:

```csharp
public class AppDbContext : DbContext
{
    public DbSet<User> Users => Set<User>();
    public DbSet<Patient> Patients => Set<Patient>();
    public DbSet<Appointment> Appointments => Set<Appointment>();
    public DbSet<WaitlistEntry> WaitlistEntries => Set<WaitlistEntry>();
    public DbSet<ReminderEvent> ReminderEvents => Set<ReminderEvent>();
    public DbSet<InsuranceProfile> InsuranceProfiles => Set<InsuranceProfile>();
    public DbSet<ClinicalDocument> ClinicalDocuments => Set<ClinicalDocument>();
    public DbSet<ClinicalFact> ClinicalFacts => Set<ClinicalFact>();
    public DbSet<CodingDecision> CodingDecisions => Set<CodingDecision>();
    public DbSet<AuditRecord> AuditRecords => Set<AuditRecord>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(AppDbContext).Assembly);
        base.OnModelCreating(modelBuilder);
    }
}
```

8. **Enable snake_case naming convention** in `DbContextOptionsBuilder` registration to produce PostgreSQL-idiomatic column names (e.g., `created_at`, `patient_id`) and align with the `app` schema configured in US_003:

```csharp
options.UseNpgsql(connectionString, o => o.MigrationsHistoryTable("__ef_migrations_history", "app"))
       .UseSnakeCaseNamingConvention();
```

## Current Project State

```text
propelIQ/
├── server/
│   ├── PropelIQ.sln
│   └── src/
│       ├── PropelIQ.Api/
│       ├── SharedKernel/
│       ├── Scheduling.Domain/
│       ├── Scheduling.Application/
│       ├── Scheduling.Infrastructure/
│       ├── ClinicalIntelligence.Domain/
│       ├── Administration.Domain/
│       └── SharedServices.Infrastructure/
│           └── Persistence/
│               └── AppDbContext.cs    (from US_003)
└── docker-compose.yml
```

> Placeholder: Update on execution based on US_002 and US_003 completion state.

## Expected Changes

| Action | File Path | Description |
|--------|-----------|-------------|
| CREATE | server/src/SharedKernel/Domain/EntityBase.cs | Base class with Guid Id, CreatedAt, UpdatedAt |
| CREATE | server/src/Administration.Domain/Entities/User.cs | User entity with role, email, password hash |
| CREATE | server/src/Administration.Domain/Entities/Patient.cs | Patient with MRN, DateOfBirth, JSONB ContactPreferences |
| CREATE | server/src/Administration.Domain/Entities/InsuranceProfile.cs | Insurance with payer, member ID, verification status |
| CREATE | server/src/Scheduling.Domain/Entities/Appointment.cs | Appointment with type, status, queue state |
| CREATE | server/src/Scheduling.Domain/Entities/WaitlistEntry.cs | Waitlist with priority, status, offered timestamp |
| CREATE | server/src/Scheduling.Domain/Entities/ReminderEvent.cs | Reminder with channel, send status, retry count |
| CREATE | server/src/ClinicalIntelligence.Domain/Entities/ClinicalDocument.cs | Document with category, extraction status |
| CREATE | server/src/ClinicalIntelligence.Domain/Entities/ClinicalFact.cs | Fact with confidence, verification state per DR-003 |
| CREATE | server/src/ClinicalIntelligence.Domain/Entities/CodingDecision.cs | ICD-10/CPT suggestion with rationale and reviewer action |
| CREATE | server/src/SharedServices.Domain/Entities/AuditRecord.cs | Append-only audit event with JSONB AuditDetails |
| CREATE | server/src/*/Infrastructure/Persistence/Configurations/*.cs | IEntityTypeConfiguration for each entity |
| MODIFY | server/src/SharedServices.Infrastructure/Persistence/AppDbContext.cs | Add DbSet registrations and ApplyConfigurationsFromAssembly |

## External References

- EF Core entity configuration (fluent API): https://learn.microsoft.com/en-us/ef/core/modeling/
- EF Core Npgsql PostgreSQL provider: https://www.npgsql.org/efcore/index.html
- Npgsql JSONB column mapping: https://www.npgsql.org/efcore/mapping/json.html
- EF Core snake_case naming conventions: https://github.com/efcore/EFCore.NamingConventions
- EF Core UUID (Guid) PostgreSQL: https://www.npgsql.org/efcore/modeling/generated-properties.html#guiduuid-generation
- EF Core owned entity types (JSON): https://learn.microsoft.com/en-us/ef/core/modeling/owned-entities
- DR-001, DR-002, DR-003, DR-005 (design.md): .propel/context/docs/design.md

## Build Commands

```bash
# Restore and build to verify entity compilation
dotnet restore server/PropelIQ.sln
dotnet build server/PropelIQ.sln --configuration Release

# Verify no model errors (EF Core design-time check)
dotnet ef dbcontext info --project server/src/SharedServices.Infrastructure \
  --startup-project server/src/PropelIQ.Api
```

## Implementation Validation Strategy

- [ ] Solution builds without errors after all entity classes are added
- [ ] `dotnet ef dbcontext info` returns AppDbContext metadata without errors
- [ ] All 10 entity DbSet properties are registered and discoverable
- [ ] Each entity has a `Guid Id` primary key with `uuid` column type
- [ ] `Patient.MRN` and `User.Email` have unique index configurations
- [ ] JSONB column types are configured for `ContactPreferences` and `AuditDetails`
- [ ] Foreign key relationships include explicit `OnDelete` behavior specification
- [ ] Snake_case naming convention produces `patient_id`, `created_at` style column names

## Implementation Checklist

- [ ] Create `EntityBase.cs` in SharedKernel with `Guid Id`, `CreatedAt`, `UpdatedAt`
- [ ] Create all 10 domain entity classes across Administration, Scheduling, ClinicalIntelligence, and SharedServices domain projects
- [ ] Create JSONB value object classes: `ContactPreferences` (Patient), `AuditDetails` (AuditRecord)
- [ ] Create `IEntityTypeConfiguration<T>` for each entity with UUID PK type, FK relationships, unique indexes, and JSONB column mappings
- [ ] Configure `AuditRecord` as append-only: no `UpdatedAt`, all properties `init`-only
- [ ] Register all 10 `DbSet<T>` properties in `AppDbContext.cs` and call `ApplyConfigurationsFromAssembly`
- [ ] Enable `UseSnakeCaseNamingConvention()` and set migrations history table to `app.__ef_migrations_history`
- [ ] Verify `dotnet ef dbcontext info` succeeds and model validation passes
