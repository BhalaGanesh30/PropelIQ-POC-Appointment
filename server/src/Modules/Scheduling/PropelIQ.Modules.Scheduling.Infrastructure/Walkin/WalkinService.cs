using System.Diagnostics;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using PropelIQ.Modules.Administration.Domain.Entities;
using PropelIQ.Modules.Scheduling.Application.Abstractions;
using PropelIQ.Modules.Scheduling.Application.Walkin.Dto;
using PropelIQ.Modules.Scheduling.Domain.Entities;
using PropelIQ.Modules.Scheduling.Domain.Enums;
using PropelIQ.Modules.SharedServices.Domain.Entities;
using PropelIQ.Modules.SharedServices.Infrastructure.Data;

namespace PropelIQ.Modules.Scheduling.Infrastructure.Walkin;

/// <summary>
/// Implements <see cref="IWalkinService"/> for walk-in queue insertion and
/// patient account conversion (EP-004 US_033).
///
/// AC-1: CreateWalkinAsync persists WalkIn + Appointment(WalkIn/Waiting),
///       calculates queue position, checks capacity, invalidates queue cache.
/// AC-2: ConvertToPatient=true path (or ConvertWalkinAsync) creates User +
///       Patient records and links them to the walk-in and its appointment.
/// AC-4: ExistingPatientId path links the walk-in to an existing account.
/// Edge Case 1: Patient existence is verified; 404 thrown when not found.
/// Edge Case 2: AtCapacity flag is set when today's count ≥ CapacityThreshold.
/// NFR-010: Every operation writes an immutable AuditRecord.
/// NFR-011: OTel span emitted per call.
/// </summary>
public sealed class WalkinService : IWalkinService
{
    // ── OTel (NFR-011) ─────────────────────────────────────────────────────────
    private static readonly ActivitySource ActivitySource =
        new("PropelIQ.Scheduling.WalkinService");

    private readonly AppDbContext _db;
    private readonly IWaitTimeEstimationService _waitTimeService;
    private readonly IDistributedCache _cache;
    private readonly WalkinOptions _options;
    private readonly ILogger<WalkinService> _logger;

    public WalkinService(
        AppDbContext db,
        IWaitTimeEstimationService waitTimeService,
        IDistributedCache cache,
        IOptions<WalkinOptions> options,
        ILogger<WalkinService> logger)
    {
        _db              = db;
        _waitTimeService = waitTimeService;
        _cache           = cache;
        _options         = options.Value;
        _logger          = logger;
    }

    // ── CreateWalkinAsync ──────────────────────────────────────────────────────

    /// <inheritdoc />
    public async Task<WalkinResponse> CreateWalkinAsync(
        CreateWalkinRequest request,
        Guid staffUserId,
        CancellationToken ct = default)
    {
        using var activity = ActivitySource.StartActivity("walkin.create");
        _logger.LogInformation("Creating walk-in for patient '{PatientName}'", request.PatientName);

        // ── 1. Resolve patient identity ────────────────────────────────────────

        Guid patientId;
        bool isConverted = false;

        if (request.ExistingPatientId.HasValue)
        {
            // AC-4: Verify the existing patient exists (Edge Case 1).
            var exists = await _db.Patients
                .AnyAsync(p => p.Id == request.ExistingPatientId.Value, ct);

            if (!exists)
                throw new KeyNotFoundException(
                    $"Patient {request.ExistingPatientId.Value} was not found.");

            patientId = request.ExistingPatientId.Value;
        }
        else if (request.ConvertToPatient)
        {
            // AC-2: Create a full patient account inline.
            patientId  = CreatePatientAccountCoreAsync(
                fullName:    request.PatientName,
                email:       request.Email ?? throw new ArgumentException("Email is required when ConvertToPatient is true."),
                dateOfBirth: request.DateOfBirth ?? throw new ArgumentException("DateOfBirth is required when ConvertToPatient is true."),
                phone:       request.Phone,
                ct:          ct);
            isConverted = true;
        }
        else
        {
            // Anonymous walk-in — create a non-activated system patient record so
            // Appointment.PatientId FK constraint is satisfied.
            patientId = CreateAnonymousPatientAsync(request.PatientName, request.Phone, ct);
        }

        // ── 2. Create WalkIn entity ────────────────────────────────────────────

        var walkIn = new WalkIn
        {
            PatientName     = request.PatientName,
            Phone           = request.Phone,
            VisitReason     = request.VisitReason,
            PatientId       = patientId,
            IsConverted     = isConverted,
            CreatedByUserId = staffUserId,
        };
        _db.WalkIns.Add(walkIn);

        // ── 3. Create queue Appointment ────────────────────────────────────────

        var appointment = new Appointment
        {
            PatientId       = patientId,
            ScheduledAt     = DateTimeOffset.UtcNow,
            DurationMinutes = 15,
            AppointmentType = AppointmentType.WalkIn.ToString(),
            Status          = AppointmentStatus.Confirmed.ToString(),
            QueueState      = QueueState.Waiting.ToString(),
        };
        _db.Appointments.Add(appointment);

        // Link appointment back to walk-in (both IDs are assigned by BaseEntity ctor).
        walkIn.AppointmentId = appointment.Id;

        // ── 4. Queue position & capacity (computed before SaveChanges) ─────────

        var today    = DateTimeOffset.UtcNow.Date;
        var tomorrow = today.AddDays(1);

        // Active queue count for position calculation (0-based; new entry is at end).
        var activeCount = await _db.Appointments.CountAsync(
            a => a.ScheduledAt >= today
              && a.ScheduledAt < tomorrow
              && a.QueueState != QueueState.Completed.ToString()
              && a.QueueState != QueueState.NoShow.ToString()
              && a.Status    != AppointmentStatus.Cancelled.ToString(),
            ct);

        // Total non-cancelled count for capacity check.
        var totalCount = await _db.Appointments.CountAsync(
            a => a.ScheduledAt >= today
              && a.ScheduledAt < tomorrow
              && a.Status != AppointmentStatus.Cancelled.ToString(),
            ct);

        // Edge Case 2: Capacity threshold check.
        var atCapacity = totalCount >= _options.CapacityThreshold;

        // Estimated wait for the new entry (it is at position `activeCount`).
        var estimatedWait = _waitTimeService.CalculateEstimatedWaitMinutes(
            activeCount,
            AppointmentType.WalkIn.ToString());

        // ── 5. Audit record (NFR-010) ──────────────────────────────────────────

        _db.AuditRecords.Add(new AuditRecord
        {
            EventType        = "WalkInCreated",
            ActorUserId      = staffUserId,
            TargetEntityId   = walkIn.Id,
            TargetEntityType = "WalkIn",
            OccurredAt       = DateTimeOffset.UtcNow,
            Details          = new AuditDetails
            {
                ChangeDescription = $"Walk-in created for patient '{request.PatientName}'",
                Metadata          =
                {
                    ["visitReason"]    = request.VisitReason,
                    ["queuePosition"]  = (activeCount + 1).ToString(),
                    ["atCapacity"]     = atCapacity.ToString(),
                },
            },
        });

        // ── 6. Persist everything atomically ──────────────────────────────────

        await _db.SaveChangesAsync(ct);
        _logger.LogInformation(
            "Walk-in {WalkinId} created for appointment {AppointmentId} at queue position {Position}",
            walkIn.Id, appointment.Id, activeCount + 1);

        // ── 7. Invalidate queue cache ──────────────────────────────────────────

        await InvalidateQueueCacheAsync(ct);

        // OTel tags (NFR-011).
        activity?.SetTag("walkin.id",      walkIn.Id.ToString());
        activity?.SetTag("appointment.id", appointment.Id.ToString());
        activity?.SetTag("at_capacity",    atCapacity.ToString());

        return new WalkinResponse
        {
            WalkinId             = walkIn.Id,
            AppointmentId        = appointment.Id,
            PatientName          = request.PatientName,
            VisitReason          = request.VisitReason,
            QueuePosition        = activeCount + 1,
            EstimatedWaitMinutes = estimatedWait,
            AtCapacity           = atCapacity,
            PatientId            = patientId,
        };
    }

    // ── ConvertWalkinAsync ─────────────────────────────────────────────────────

    /// <inheritdoc />
    public async Task<ConvertWalkinResponse> ConvertWalkinAsync(
        Guid walkinId,
        ConvertWalkinRequest request,
        Guid staffUserId,
        CancellationToken ct = default)
    {
        using var activity = ActivitySource.StartActivity("walkin.convert");

        var walkIn = await _db.WalkIns.FindAsync([walkinId], ct);

        if (walkIn is null)
            throw new KeyNotFoundException($"Walk-in {walkinId} was not found.");

        // Return 409 Conflict if already converted.
        if (walkIn.IsConverted)
            throw new InvalidOperationException(
                $"Walk-in {walkinId} has already been converted to a patient account.");

        // Create full patient account from provided demographics.
        var patientId = CreatePatientAccountCoreAsync(
            fullName:    walkIn.PatientName,
            email:       request.Email,
            dateOfBirth: request.DateOfBirth,
            phone:       request.Phone ?? walkIn.Phone,
            ct:          ct);

        // Update walk-in link.
        walkIn.PatientId    = patientId;
        walkIn.IsConverted  = true;

        // Update appointment patient FK if appointment exists.
        if (walkIn.AppointmentId.HasValue)
        {
            var appt = await _db.Appointments.FindAsync([walkIn.AppointmentId.Value], ct);
            if (appt is not null)
                appt.PatientId = patientId;
        }

        // Audit record (NFR-010).
        _db.AuditRecords.Add(new AuditRecord
        {
            EventType        = "WalkInConverted",
            ActorUserId      = staffUserId,
            TargetEntityId   = walkIn.Id,
            TargetEntityType = "WalkIn",
            OccurredAt       = DateTimeOffset.UtcNow,
            Details          = new AuditDetails
            {
                ChangeDescription = $"Walk-in {walkinId} converted to patient account",
                Metadata          =
                {
                    ["patientId"] = patientId.ToString(),
                },
            },
        });

        await _db.SaveChangesAsync(ct);
        _logger.LogInformation(
            "Walk-in {WalkinId} converted to patient {PatientId}", walkinId, patientId);

        activity?.SetTag("walkin.id",   walkinId.ToString());
        activity?.SetTag("patient.id",  patientId.ToString());

        return new ConvertWalkinResponse
        {
            PatientId        = patientId,
            WalkinId         = walkinId,
            ConversionStatus = "Converted",
        };
    }

    // ── Private helpers ────────────────────────────────────────────────────────

    /// <summary>
    /// Creates a User + Patient record from the provided demographics.
    /// The User account is created with <c>IsActive = false</c> (no login password)
    /// pending a password-set flow. Returns the new <c>Patient.Id</c>.
    /// </summary>
    private Guid CreatePatientAccountCoreAsync(
        string fullName,
        string email,
        DateOnly dateOfBirth,
        string? phone,
        CancellationToken ct)
    {
        var (firstName, lastName) = SplitName(fullName);

        // Create a non-activated user account — no PasswordHash until the patient
        // completes an activation flow.  IsActive=false prevents login.
        var user = new User
        {
            Email        = email,
            PasswordHash = string.Empty,
            Role         = "Patient",
            FirstName    = firstName,
            LastName     = lastName,
            IsActive     = false,
        };
        _db.Users.Add(user);

        var patient = new Patient
        {
            UserId     = user.Id,          // user.Id is Guid.NewGuid() from BaseEntity ctor
            FirstName  = firstName,
            LastName   = lastName,
            DateOfBirth = dateOfBirth,
            MRN        = GenerateMrn(),
            ContactPreferences = new ContactPreferences
            {
                PreferredPhone = phone,
            },
        };
        _db.Patients.Add(patient);

        // Both entities are tracked; SaveChangesAsync is called by the caller.
        // Return the patient ID (already assigned by BaseEntity ctor).
        return patient.Id;
    }

    /// <summary>
    /// Creates a placeholder User + Patient for anonymous walk-ins where staff has
    /// not provided existing patient details or opted for inline registration.
    /// The account is non-activated and uses a system-only email address.
    /// </summary>
    private Guid CreateAnonymousPatientAsync(
        string fullName,
        string? phone,
        CancellationToken ct)
    {
        var (firstName, lastName) = SplitName(fullName);

        var user = new User
        {
            Email        = $"walkin-{Guid.NewGuid():N}@walkin.internal",
            PasswordHash = string.Empty,
            Role         = "Patient",
            FirstName    = firstName,
            LastName     = lastName,
            IsActive     = false,
        };
        _db.Users.Add(user);

        var patient = new Patient
        {
            UserId     = user.Id,
            FirstName  = firstName,
            LastName   = lastName,
            DateOfBirth = DateOnly.MinValue,  // placeholder until conversion
            MRN        = GenerateMrn(),
            ContactPreferences = new ContactPreferences
            {
                PreferredPhone = phone,
            },
        };
        _db.Patients.Add(patient);

        return patient.Id;
    }

    /// <summary>Splits "First Last Name" into (first, rest-as-last).</summary>
    private static (string First, string Last) SplitName(string fullName)
    {
        var trimmed = fullName.Trim();
        var idx = trimmed.IndexOf(' ');
        if (idx < 0)
            return (trimmed, trimmed);
        return (trimmed[..idx], trimmed[(idx + 1)..]);
    }

    private static string GenerateMrn()
        => $"WI-{Guid.NewGuid().ToString("N")[..8].ToUpperInvariant()}";

    /// <summary>
    /// Removes all <c>queue:today:{date}:*</c> cache entries so the dashboard
    /// reflects the newly inserted walk-in on the next poll.
    /// </summary>
    private async Task InvalidateQueueCacheAsync(CancellationToken ct)
    {
        var date   = DateTimeOffset.UtcNow.ToString("yyyyMMdd");
        var states = Enum.GetNames<QueueState>()
            .Append("ALL")
            .Select(s => $"queue:today:{date}:{s}");

        foreach (var key in states)
        {
            try   { await _cache.RemoveAsync(key, ct); }
            catch (Exception ex)
            {
                // Cache invalidation failure is non-fatal — dashboard will self-correct
                // on the next 15-second TTL expiry (NFR-002).
                _logger.LogWarning(ex, "Failed to invalidate queue cache key {Key}", key);
            }
        }
    }
}
