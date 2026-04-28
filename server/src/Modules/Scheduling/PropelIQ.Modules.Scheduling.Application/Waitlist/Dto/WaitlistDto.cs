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
/// Response from GET /api/v1/waitlist/claim-details?token={token} (US_030 task_002).
/// Returned to the claim page component so the patient can see slot details
/// before confirming their claim.
/// </summary>
public record SlotClaimDetailsResponse
{
    public Guid WaitlistEntryId { get; init; }
    /// <summary>ISO 8601 UTC — converted to browser timezone client-side (edge case 2).</summary>
    public DateTimeOffset SlotDateTime { get; init; }
    public string SlotType { get; init; } = string.Empty;
    public string? ProviderName { get; init; }
    public int DurationMinutes { get; init; }
    /// <summary>UTC expiry for the countdown timer (AC-2).</summary>
    public DateTimeOffset ExpiresAtUtc { get; init; }
    public string Status { get; init; } = string.Empty;
}
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
