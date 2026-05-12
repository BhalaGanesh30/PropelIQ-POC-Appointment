namespace PropelIQ.Modules.SharedServices.Domain.Entities;

/// <summary>
/// State machine transitions for a patient disclosure request (US_057, AC-2, AC-3).
/// Submitted → Compiling → PendingReview → Approved → Delivered | Rejected
/// </summary>
public enum DisclosureStatus
{
    Submitted,
    Compiling,
    PendingReview,
    Approved,
    Delivered,
    Rejected,
}
