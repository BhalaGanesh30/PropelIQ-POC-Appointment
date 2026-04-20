# Task - TASK_002

## Requirement Reference

- User Story: us_010
- Story Location: .propel/context/tasks/EP-DATA/us_010/us_010.md
- Acceptance Criteria:
  - AC-1: Given the schema is applied, When an attempt is made to insert an Appointment record with a non-existent PatientId, Then the database rejects the insert with a foreign key violation error.
  - AC-2: Given transactional operations are executed (booking, arrival, waitlist swap, coding finalization), When a partial operation fails mid-transaction, Then the entire transaction rolls back and no partial data is persisted.
- Edge Case:
  - What happens if a bulk import violates referential integrity mid-batch? Transaction rolls back entire batch; error report identifies the offending rows.

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
| Library | Npgsql | 8.x |
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

Implement the application-layer transactional consistency and referential integrity handling patterns using EF Core's `DbContext` as the Unit of Work. Create an `IUnitOfWork` abstraction that wraps `SaveChangesAsync` in explicit database transactions, ensuring multi-step domain operations (booking, arrival, waitlist swap, coding finalization) either commit atomically or roll back completely — no partial data is ever persisted (DR-002). Implement structured error handling for PostgreSQL FK violation errors (`23503` SQLSTATE) that translates `DbUpdateException` containing `PostgresException` into domain-specific errors with the offending entity and FK column identified. For bulk import scenarios, implement a batch processor that wraps the entire batch in a single transaction and produces an error report identifying offending rows when referential integrity violations occur mid-batch.

## Dependent Tasks

- US_009 task_001 (requires all entity models with FK relationships configured)
- US_009 task_002 (requires InitialSchema migration applied with FK constraints)
- US_002 tasks (requires ASP.NET Core solution structure)

## Impacted Components

- New: `server/src/SharedKernel/Persistence/IUnitOfWork.cs` (Unit of Work abstraction)
- New: `server/src/SharedKernel/Persistence/UnitOfWork.cs` (implementation wrapping AppDbContext)
- New: `server/src/SharedKernel/Persistence/DatabaseErrorHandler.cs` (FK/constraint violation translator)
- New: `server/src/SharedKernel/Persistence/BulkImportProcessor.cs` (batch transaction with error report)
- New: `server/src/SharedKernel/Persistence/Exceptions/ReferentialIntegrityException.cs` (domain exception)
- New: `server/src/SharedKernel/Persistence/Exceptions/BulkImportException.cs` (batch error exception)
- Modify: `server/src/PropelIQ.Api/Program.cs` (register IUnitOfWork in DI)

## Implementation Plan

1. **Create `IUnitOfWork` interface** in SharedKernel that defines the transactional boundary contract. Application services depend on this abstraction rather than directly on `DbContext.SaveChangesAsync`:

```csharp
namespace PropelIQ.SharedKernel.Persistence;

public interface IUnitOfWork : IDisposable, IAsyncDisposable
{
    Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);
    Task BeginTransactionAsync(CancellationToken cancellationToken = default);
    Task CommitTransactionAsync(CancellationToken cancellationToken = default);
    Task RollbackTransactionAsync(CancellationToken cancellationToken = default);
}
```

2. **Create `UnitOfWork` implementation** that wraps `AppDbContext` and manages explicit `IDbContextTransaction` instances. The `CommitTransactionAsync` method catches exceptions, rolls back, and rethrows to guarantee AC-2:

```csharp
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;

namespace PropelIQ.SharedKernel.Persistence;

public sealed class UnitOfWork : IUnitOfWork
{
    private readonly AppDbContext _context;
    private IDbContextTransaction? _transaction;

    public UnitOfWork(AppDbContext context) => _context = context;

    public async Task<int> SaveChangesAsync(CancellationToken ct = default)
    {
        try
        {
            return await _context.SaveChangesAsync(ct);
        }
        catch (DbUpdateException ex)
        {
            throw DatabaseErrorHandler.Translate(ex);
        }
    }

    public async Task BeginTransactionAsync(CancellationToken ct = default)
    {
        _transaction = await _context.Database.BeginTransactionAsync(ct);
    }

    public async Task CommitTransactionAsync(CancellationToken ct = default)
    {
        try
        {
            await _context.SaveChangesAsync(ct);
            await (_transaction?.CommitAsync(ct) ?? Task.CompletedTask);
        }
        catch
        {
            await RollbackTransactionAsync(ct);
            throw;
        }
    }

    public async Task RollbackTransactionAsync(CancellationToken ct = default)
    {
        if (_transaction is not null)
        {
            await _transaction.RollbackAsync(ct);
        }
    }

    public void Dispose() => _transaction?.Dispose();
    public async ValueTask DisposeAsync()
    {
        if (_transaction is not null)
            await _transaction.DisposeAsync();
    }
}
```

3. **Create `DatabaseErrorHandler`** to translate PostgreSQL-specific `DbUpdateException` into domain exceptions. The handler inspects the inner `PostgresException` for SQLSTATE codes and extracts the offending constraint name, table, and column:

```csharp
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace PropelIQ.SharedKernel.Persistence;

public static class DatabaseErrorHandler
{
    public static Exception Translate(DbUpdateException ex)
    {
        if (ex.InnerException is PostgresException pgEx)
        {
            return pgEx.SqlState switch
            {
                // FK violation (AC-1)
                PostgresErrorCodes.ForeignKeyViolation => new ReferentialIntegrityException(
                    $"Foreign key violation on table '{pgEx.TableName}': " +
                    $"constraint '{pgEx.ConstraintName}' " +
                    $"referencing column '{pgEx.ColumnName}'. " +
                    $"The referenced record does not exist.",
                    pgEx.TableName,
                    pgEx.ConstraintName,
                    pgEx),

                // Unique constraint violation
                PostgresErrorCodes.UniqueViolation => new ReferentialIntegrityException(
                    $"Unique constraint violation on table '{pgEx.TableName}': " +
                    $"constraint '{pgEx.ConstraintName}'. " +
                    $"A record with the same value already exists.",
                    pgEx.TableName,
                    pgEx.ConstraintName,
                    pgEx),

                // Not null violation
                PostgresErrorCodes.NotNullViolation => new ReferentialIntegrityException(
                    $"Not null violation on table '{pgEx.TableName}': " +
                    $"column '{pgEx.ColumnName}' cannot be null.",
                    pgEx.TableName,
                    pgEx.ColumnName,
                    pgEx),

                _ => ex
            };
        }

        return ex;
    }
}
```

4. **Create domain exception types** with structured properties for API-layer error mapping:

```csharp
namespace PropelIQ.SharedKernel.Persistence.Exceptions;

public sealed class ReferentialIntegrityException : Exception
{
    public string? TableName { get; }
    public string? ConstraintName { get; }

    public ReferentialIntegrityException(
        string message,
        string? tableName,
        string? constraintName,
        Exception? innerException = null)
        : base(message, innerException)
    {
        TableName = tableName;
        ConstraintName = constraintName;
    }
}

public sealed class BulkImportException : Exception
{
    public IReadOnlyList<BulkImportError> Errors { get; }

    public BulkImportException(
        string message,
        IReadOnlyList<BulkImportError> errors)
        : base(message)
    {
        Errors = errors;
    }
}

public sealed record BulkImportError(
    int RowIndex,
    string EntityType,
    string ErrorMessage,
    string? ConstraintName);
```

5. **Create `BulkImportProcessor`** that wraps an entire batch in a single transaction (edge case). When a referential integrity violation occurs mid-batch, the transaction rolls back and an error report identifies the offending rows:

```csharp
namespace PropelIQ.SharedKernel.Persistence;

public sealed class BulkImportProcessor
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly AppDbContext _context;

    public BulkImportProcessor(IUnitOfWork unitOfWork, AppDbContext context)
    {
        _unitOfWork = unitOfWork;
        _context = context;
    }

    public async Task<BulkImportResult> ImportAsync<T>(
        IReadOnlyList<T> entities,
        CancellationToken ct = default) where T : class
    {
        var errors = new List<BulkImportError>();

        await _unitOfWork.BeginTransactionAsync(ct);
        try
        {
            for (int i = 0; i < entities.Count; i++)
            {
                _context.Set<T>().Add(entities[i]);

                // Flush per-row to detect FK violations early
                try
                {
                    await _context.SaveChangesAsync(ct);
                }
                catch (ReferentialIntegrityException ex)
                {
                    errors.Add(new BulkImportError(
                        RowIndex: i,
                        EntityType: typeof(T).Name,
                        ErrorMessage: ex.Message,
                        ConstraintName: ex.ConstraintName));

                    // Detach failed entity to continue validation
                    _context.Entry(entities[i]).State = EntityState.Detached;
                }
            }

            if (errors.Count > 0)
            {
                await _unitOfWork.RollbackTransactionAsync(ct);
                throw new BulkImportException(
                    $"Bulk import failed: {errors.Count} row(s) violated referential integrity.",
                    errors);
            }

            await _unitOfWork.CommitTransactionAsync(ct);
            return new BulkImportResult(entities.Count, 0);
        }
        catch (BulkImportException)
        {
            throw; // Already rolled back
        }
        catch
        {
            await _unitOfWork.RollbackTransactionAsync(ct);
            throw;
        }
    }
}

public sealed record BulkImportResult(int TotalRows, int FailedRows);
```

6. **Register `IUnitOfWork` in DI** in `Program.cs`:

```csharp
builder.Services.AddScoped<IUnitOfWork, UnitOfWork>();
builder.Services.AddScoped<BulkImportProcessor>();
```

7. **Usage pattern for transactional domain operations** (AC-2). Application services use `IUnitOfWork` for multi-step operations:

```csharp
// Example: Booking flow (Appointment + ReminderEvent + WaitlistEntry in one transaction)
public async Task BookAppointmentAsync(BookingRequest request, CancellationToken ct)
{
    await _unitOfWork.BeginTransactionAsync(ct);

    _context.Appointments.Add(appointment);
    _context.ReminderEvents.Add(reminder);

    if (request.AddToWaitlist)
        _context.WaitlistEntries.Add(waitlistEntry);

    await _unitOfWork.CommitTransactionAsync(ct);
    // If any step fails, CommitTransactionAsync rolls back everything
}
```

8. **FK violation error surfacing** (AC-1). When a `ReferentialIntegrityException` is thrown, the existing exception handling middleware (from US_002) translates it to an RFC 9457 ProblemDetails response with status 422:

```json
{
  "type": "https://propeliq.local/errors/referential-integrity",
  "title": "Referential Integrity Violation",
  "status": 422,
  "detail": "Foreign key violation on table 'appointments': constraint 'fk_appointments_patient_id' referencing column 'patient_id'. The referenced record does not exist.",
  "instance": "/api/v1/appointments"
}
```

## Current Project State

```text
propelIQ/
├── server/
│   ├── PropelIQ.sln
│   └── src/
│       ├── PropelIQ.Api/
│       │   ├── Program.cs
│       │   └── Middleware/
│       │       └── ExceptionHandlingMiddleware.cs
│       ├── SharedKernel/
│       │   ├── Domain/
│       │   │   └── EntityBase.cs         (from US_009)
│       │   └── Persistence/              (to be created)
│       └── SharedServices.Infrastructure/
│           └── Persistence/
│               ├── AppDbContext.cs
│               └── Migrations/
│                   └── <timestamp>_InitialSchema.cs
└── docker-compose.yml
```

> Placeholder: Update on execution based on US_009 and US_002 task completion state.

## Expected Changes

| Action | File Path | Description |
|--------|-----------|-------------|
| CREATE | server/src/SharedKernel/Persistence/IUnitOfWork.cs | Unit of Work interface with transaction management |
| CREATE | server/src/SharedKernel/Persistence/UnitOfWork.cs | Implementation wrapping AppDbContext with explicit IDbContextTransaction |
| CREATE | server/src/SharedKernel/Persistence/DatabaseErrorHandler.cs | PostgresException SQLSTATE translator to domain exceptions |
| CREATE | server/src/SharedKernel/Persistence/BulkImportProcessor.cs | Batch import with single-transaction rollback and error report |
| CREATE | server/src/SharedKernel/Persistence/Exceptions/ReferentialIntegrityException.cs | Domain exception for FK/unique/not-null violations |
| CREATE | server/src/SharedKernel/Persistence/Exceptions/BulkImportException.cs | Batch error exception with per-row error list |
| MODIFY | server/src/PropelIQ.Api/Program.cs | Register IUnitOfWork and BulkImportProcessor in DI |

## External References

- EF Core transactions: https://learn.microsoft.com/en-us/ef/core/saving/transactions
- EF Core DbUpdateException: https://learn.microsoft.com/en-us/dotnet/api/microsoft.entityframeworkcore.dbupdateexception
- Npgsql PostgresException: https://www.npgsql.org/doc/api/Npgsql.PostgresException.html
- PostgreSQL SQLSTATE error codes: https://www.postgresql.org/docs/15/errcodes-appendix.html
- PostgreSQL foreign keys: https://www.postgresql.org/docs/15/ddl-constraints.html#DDL-CONSTRAINTS-FK
- Unit of Work pattern: https://learn.microsoft.com/en-us/aspnet/mvc/overview/older-versions/getting-started-with-ef-5-using-mvc-4/implementing-the-repository-and-unit-of-work-patterns-in-an-asp-net-mvc-application
- RFC 9457 Problem Details: https://www.rfc-editor.org/rfc/rfc9457
- DR-002 (referential integrity): .propel/context/docs/design.md

## Build Commands

```bash
# Build solution
dotnet restore server/PropelIQ.sln
dotnet build server/PropelIQ.sln --configuration Release

# Run API (transactions tested via API endpoints)
dotnet run --project server/src/PropelIQ.Api

# Test FK violation (via psql)
docker exec propeliq-postgres psql -U app_user -d propeliq -c \
  "INSERT INTO app.appointments (id, patient_id, staff_user_id, scheduled_at, duration_minutes, appointment_type) \
   VALUES (gen_random_uuid(), '00000000-0000-0000-0000-999999999999', \
   (SELECT id FROM app.users LIMIT 1), now(), 30, 'Consultation');"
```

## Implementation Validation Strategy

- [ ] INSERT into `appointments` with non-existent `patient_id` raises FK violation error with descriptive message (AC-1)
- [ ] Multi-step transaction (booking + reminder + waitlist) commits atomically on success (AC-2)
- [ ] Multi-step transaction rolls back completely when any step fails — no partial data persisted (AC-2)
- [ ] `DatabaseErrorHandler` translates PostgresException SQLSTATE 23503 to `ReferentialIntegrityException`
- [ ] Bulk import with mid-batch FK violation rolls back entire batch and produces error report identifying offending rows (edge case)
- [ ] `IUnitOfWork` is registered as scoped in DI and injectable into application services
- [ ] FK violation surfaces as RFC 9457 ProblemDetails response with status 422

## Implementation Checklist

- [x] Create `IUnitOfWork` interface with `SaveChangesAsync`, `BeginTransactionAsync`, `CommitTransactionAsync`, `RollbackTransactionAsync`
- [x] Create `UnitOfWork` implementation wrapping `AppDbContext` with `IDbContextTransaction` management and auto-rollback on exception
- [x] Create `DatabaseErrorHandler` translating `PostgresException` SQLSTATE codes (23503, 23505, 23502) to domain exceptions
- [x] Create `ReferentialIntegrityException` with `TableName`, `ConstraintName` properties for structured error reporting
- [x] Create `BulkImportProcessor` with single-transaction batch processing and per-row error identification on FK violation
- [x] Create `BulkImportException` with `IReadOnlyList<BulkImportError>` for batch error report
- [x] Register `IUnitOfWork` (scoped) and `BulkImportProcessor` (scoped) in `Program.cs`
- [x] Document transactional usage pattern for multi-entity domain operations (booking, arrival, waitlist swap, coding finalization)
