namespace PropelIQ.Modules.Scheduling.Application.Waitlist.Dto;

/// <summary>POST /api/v1/waitlist request body (AC-1).</summary>
public record JoinWaitlistRequest
{
    public DateTimeOffset PreferredDateStart { get; init; }
    public DateTimeOffset PreferredDateEnd { get; init; }

    /// <summary>Must be 15, 30, or 60 — mirrors <c>SlotDuration</c> enum values.</summary>
    public int PreferredDurationMinutes { get; init; }

    public string PreferredAppointmentType { get; init; } = string.Empty;
}

/// <summary>Waitlist entry response returned by GET and POST /api/v1/waitlist.</summary>
public record WaitlistEntryResponse
{
    public Guid Id { get; init; }
    public string Status { get; init; } = string.Empty;
    public DateTimeOffset PreferredDateStart { get; init; }
    public DateTimeOffset PreferredDateEnd { get; init; }
    public int PreferredDurationMinutes { get; init; }
    public string PreferredAppointmentType { get; init; } = string.Empty;
    public Guid? OfferedSlotId { get; init; }
    public DateTimeOffset? OfferedAt { get; init; }
    public DateTimeOffset? ClaimExpiresAt { get; init; }
    public int Position { get; init; }
    public DateTimeOffset CreatedAt { get; init; }
}

/// <summary>
/// Response from POST /api/v1/waitlist/{id}/claim (AC-3).
/// Returns standard booking confirmation details so the client can display
/// a confirmation screen identical to a direct booking.
/// </summary>
public record ClaimWaitlistResponse
{
    public Guid AppointmentId { get; init; }
    public string ConfirmationCode { get; init; } = string.Empty;
    public DateTimeOffset AppointmentTime { get; init; }
    public int DurationMinutes { get; init; }
    public string AppointmentType { get; init; } = string.Empty;
    public string? ProviderName { get; init; }
    public string? Location { get; init; }
}
