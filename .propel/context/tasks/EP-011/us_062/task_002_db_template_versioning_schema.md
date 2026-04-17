# Task - TASK_002

## Requirement Reference

- User Story: us_062
- Story Location: .propel/context/tasks/EP-011/us_062/us_062.md
- Acceptance Criteria:
  - AC-1: Given I am authenticated as an Admin, When I create or edit an HTML or SMS notification template, Then the template is saved as a new version with the change date and my identity, while previous versions are preserved.
  - AC-3: Given I want to revert to a previous template version, When I select a prior version and click "Restore," Then the selected version becomes active as a new version and existing queued notifications using the old template remain unaffected.
- Edge Cases:
  - How does the system handle templates that reference deleted merge fields? Template validation detects orphaned placeholders and warns the admin before saving.

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
| Backend | N/A | N/A |
| Database | PostgreSQL | 15.x |
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

Create the PostgreSQL schema for versioned notification template storage. The `notification_templates` table holds template metadata (name, type HTML/SMS, description) with a FK to the current active version. The `template_versions` table stores immutable version records with content, optional subject line (HTML templates), version number, creator identity, and timestamp (AC-1). Each save creates a new row; restoring a prior version creates a new row with the old content, preserving the version chain so queued notifications referencing earlier version IDs remain unaffected (AC-3). A `merge_field_registry` table holds the canonical set of allowed merge fields with sample values for preview and validation (edge case 2). Indexes include a unique composite on `(template_id, version_number)` for version ordering and a partial index on `is_active = true` for fast active version lookup.

## Dependent Tasks

- US_015 task_001 (requires users table for FK on created_by_user_id)

## Impacted Components

- New: EF Core migration for `notification_templates` table
- New: EF Core migration for `template_versions` table
- New: EF Core migration for `merge_field_registry` table
- New: EF Core entity configuration for NotificationTemplate
- New: EF Core entity configuration for TemplateVersion
- Modify: `PropelIQ.Infrastructure/Data/AppDbContext.cs` (add DbSet properties)

## Implementation Plan

1. **Create `notification_templates` table**:

```sql
-- notification_templates table
CREATE TABLE notification_templates (
    id                 UUID PRIMARY KEY
                       DEFAULT gen_random_uuid(),
    name               VARCHAR(200) NOT NULL,
    type               VARCHAR(10)  NOT NULL
                       CHECK (type IN ('HTML','SMS')),
    description        TEXT         NOT NULL
                       DEFAULT '',
    current_version_id UUID         NULL,
    created_at_utc     TIMESTAMPTZ  NOT NULL
                       DEFAULT now()
);

-- Unique constraint on template name + type
CREATE UNIQUE INDEX
    ix_notification_templates_name_type
ON notification_templates (name, type);
```

2. **Create `template_versions` table**:

```sql
-- template_versions table
CREATE TABLE template_versions (
    id                 UUID PRIMARY KEY
                       DEFAULT gen_random_uuid(),
    template_id        UUID         NOT NULL
        REFERENCES notification_templates(id)
        ON DELETE CASCADE,
    version_number     INT          NOT NULL,
    content            TEXT         NOT NULL,
    subject            VARCHAR(500) NULL,
    is_active          BOOLEAN      NOT NULL
                       DEFAULT false,
    created_at_utc     TIMESTAMPTZ  NOT NULL
                       DEFAULT now(),
    created_by_user_id UUID         NOT NULL
        REFERENCES users(user_id)
        ON DELETE SET NULL,
    created_by_name    VARCHAR(200) NOT NULL
);

-- Unique version number per template
CREATE UNIQUE INDEX
    ix_template_versions_template_version
ON template_versions (template_id, version_number);

-- Fast active version lookup
CREATE UNIQUE INDEX
    ix_template_versions_active
ON template_versions (template_id)
WHERE is_active = true;

-- Reverse chronological version listing
CREATE INDEX
    ix_template_versions_template_created
ON template_versions
    (template_id, created_at_utc DESC);
```

3. **Add FK from notification_templates to template_versions** (deferred to avoid circular dependency):

```sql
-- Add FK for current_version_id after both
-- tables exist
ALTER TABLE notification_templates
ADD CONSTRAINT
    fk_templates_current_version
FOREIGN KEY (current_version_id)
    REFERENCES template_versions(id)
    ON DELETE SET NULL;
```

4. **Create `merge_field_registry` table** for canonical merge field definitions:

```sql
-- merge_field_registry table
CREATE TABLE merge_field_registry (
    field_name   VARCHAR(100) PRIMARY KEY,
    display_name VARCHAR(200) NOT NULL,
    sample_value VARCHAR(500) NOT NULL,
    category     VARCHAR(50)  NOT NULL
                 DEFAULT 'General',
    is_active    BOOLEAN      NOT NULL
                 DEFAULT true
);

-- Seed initial merge fields
INSERT INTO merge_field_registry
    (field_name, display_name,
     sample_value, category)
VALUES
    ('patient_name',
     'Patient Name',
     'Jane Smith',
     'Patient'),
    ('appointment_date',
     'Appointment Date',
     '2026-05-15',
     'Appointment'),
    ('appointment_time',
     'Appointment Time',
     '10:30 AM',
     'Appointment'),
    ('clinic_name',
     'Clinic Name',
     'PropelIQ Health Center',
     'Organization'),
    ('provider_name',
     'Provider Name',
     'Dr. Sarah Johnson',
     'Provider'),
    ('appointment_type',
     'Appointment Type',
     'Follow-up Visit',
     'Appointment'),
    ('cancellation_link',
     'Cancellation Link',
     'https://example.com/cancel/abc123',
     'Action'),
    ('reschedule_link',
     'Reschedule Link',
     'https://example.com/reschedule/abc123',
     'Action');
```

## Current Project State

```text
propelIQ/
└── PropelIQ.Infrastructure/
    └── Data/
        ├── AppDbContext.cs                            (modify)
        └── Migrations/
            └── XXXXXXXX_AddTemplateVersioning.cs      (new)
```

> Placeholder: Update on execution based on US_015 task_001 completion state.

## Expected Changes

| Action | File Path | Description |
|--------|-----------|-------------|
| CREATE | PropelIQ.Infrastructure/Data/Migrations/XXXXXXXX_AddTemplateVersioning.cs | EF Core migration: notification_templates, template_versions, merge_field_registry tables with indexes and seed data |
| MODIFY | PropelIQ.Infrastructure/Data/AppDbContext.cs | Add DbSet for NotificationTemplate, TemplateVersion, MergeFieldRegistryEntry; configure entity relationships |

## External References

- PostgreSQL Partial Indexes: https://www.postgresql.org/docs/15/indexes-partial.html
- PostgreSQL Unique Constraints: https://www.postgresql.org/docs/15/ddl-constraints.html#DDL-CONSTRAINTS-UNIQUE-CONSTRAINTS
- EF Core Migrations: https://learn.microsoft.com/en-us/ef/core/managing-schemas/migrations/
- EF Core Entity Configuration: https://learn.microsoft.com/en-us/ef/core/modeling/

## Build Commands

```bash
# Generate migration
cd PropelIQ.Infrastructure
dotnet ef migrations add AddTemplateVersioning \
  --startup-project ../PropelIQ.Api

# Apply migration
dotnet ef database update \
  --startup-project ../PropelIQ.Api

# Verify schema:
# 1. Check notification_templates table exists with name/type unique index
# 2. Check template_versions table with unique (template_id, version_number)
# 3. Check partial index on is_active = true
# 4. Check merge_field_registry seeded with 8 fields
# 5. Verify FK from notification_templates.current_version_id
#    to template_versions.id
```

## Implementation Validation Strategy

- [ ] notification_templates table created with name, type, description, current_version_id columns
- [ ] template_versions table created with immutable version rows (content, subject, version_number, identity)
- [ ] Unique composite index on (template_id, version_number) prevents duplicate versions
- [ ] Partial unique index on (template_id) WHERE is_active = true ensures single active version
- [ ] FK from current_version_id to template_versions enables active version navigation
- [ ] merge_field_registry table seeded with 8 merge fields across 4 categories
- [ ] CASCADE delete on template_versions when parent template is removed

## Implementation Checklist

- [ ] Create notification_templates table with name/type unique index and type CHECK constraint
- [ ] Create template_versions table with FK to notification_templates (CASCADE) and FK to users (SET NULL)
- [ ] Add unique composite index on (template_id, version_number) for version ordering
- [ ] Add partial unique index on (template_id) WHERE is_active = true for single active version guarantee
- [ ] Add deferred FK from notification_templates.current_version_id to template_versions
- [ ] Create merge_field_registry table and seed with 8 initial merge fields
