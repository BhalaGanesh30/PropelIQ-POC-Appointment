using Ical.Net;
using Ical.Net.CalendarComponents;
using Ical.Net.DataTypes;
using Ical.Net.Serialization;
using Microsoft.Extensions.Options;
using PropelIQ.Modules.Scheduling.Application.Booking.Artifacts;
using PropelIQ.Modules.Scheduling.Domain.Events;

namespace PropelIQ.Modules.Scheduling.Infrastructure.Booking;

/// <summary>
/// Generates RFC 5545-compliant ICS calendar files for appointment booking,
/// rescheduling, and cancellation workflows.
///
/// AC-1 / AC-2: Initial booking ICS with explicit TZID, PRODID, METHOD:REQUEST, SEQUENCE:0.
/// AC-3: Updated ICS with incremented SEQUENCE so Google Calendar / Outlook / Apple Calendar
///       recognise the rescheduled event as an update (not a duplicate).
/// AC-4: Cancellation ICS with STATUS:CANCELLED and METHOD:CANCEL so calendar clients
///       automatically remove the event.
/// Edge case: explicit TZID on DTSTART/DTEND prevents timezone conversion errors across
///            different calendar clients.
/// </summary>
public sealed class IcsGenerator
{
    private readonly IcsOptions _options;

    public IcsGenerator(IOptions<IcsOptions> options)
    {
        _options = options.Value;
    }

    // ── AC-1 / AC-2: Initial booking ICS ────────────────────────────────────

    /// <summary>
    /// Generates an ICS file for a newly confirmed appointment.
    /// Includes PRODID, METHOD:REQUEST, explicit TZID, SEQUENCE:0, and a 30-minute alarm.
    /// </summary>
    public byte[] GenerateBookingIcs(BookingConfirmedEvent booking)
    {
        var calendar = BuildCalendar("REQUEST");
        var evt = BuildBaseEvent(
            appointmentId:   booking.AppointmentId,
            summary:         $"Appointment \u2013 {booking.AppointmentType}",
            description:     FormatDescription(booking.ProviderName, booking.ConfirmationCode),
            startTimeUtc:    booking.AppointmentTime.UtcDateTime,
            durationMinutes: booking.DurationMinutes,
            location:        booking.Location ?? "Main Office",
            sequenceNumber:  0);
        calendar.Events.Add(evt);
        return Serialize(calendar);
    }

    // ── AC-3: Rescheduled ICS with SEQUENCE increment ─────────────────────────

    /// <summary>
    /// Generates an updated ICS with an incremented SEQUENCE number so calendar clients
    /// recognise the event as an update rather than a new (duplicate) event.
    /// </summary>
    public byte[] GenerateUpdateIcs(
        Guid           appointmentId,
        string         appointmentType,
        string?        providerName,
        string         confirmationCode,
        DateTimeOffset newStartTime,
        int            durationMinutes,
        string?        location,
        int            sequenceNumber)
    {
        var calendar = BuildCalendar("REQUEST");
        var evt = BuildBaseEvent(
            appointmentId:   appointmentId,
            summary:         $"Appointment \u2013 {appointmentType} (Rescheduled)",
            description:     FormatDescription(providerName, confirmationCode)
                             + "\nThis appointment has been rescheduled.",
            startTimeUtc:    newStartTime.UtcDateTime,
            durationMinutes: durationMinutes,
            location:        location ?? "Main Office",
            sequenceNumber:  sequenceNumber);
        calendar.Events.Add(evt);
        return Serialize(calendar);
    }

    // ── AC-4: Cancellation ICS with STATUS:CANCELLED ──────────────────────────

    /// <summary>
    /// Generates a cancellation ICS (METHOD:CANCEL, STATUS:CANCELLED) so calendar clients
    /// automatically remove the event when the file is imported or delivered by email.
    /// The SEQUENCE is incremented by one beyond the stored value so clients process
    /// this as the authoritative final version of the event.
    /// </summary>
    public byte[] GenerateCancellationIcs(
        Guid           appointmentId,
        string         appointmentType,
        string?        providerName,
        string         confirmationCode,
        DateTimeOffset originalStartTime,
        int            durationMinutes,
        string?        location,
        int            sequenceNumber)
    {
        var calendar = BuildCalendar("CANCEL");
        var evt = BuildBaseEvent(
            appointmentId:   appointmentId,
            summary:         $"CANCELLED: Appointment \u2013 {appointmentType}",
            description:     FormatDescription(providerName, confirmationCode)
                             + "\nThis appointment has been cancelled.",
            startTimeUtc:    originalStartTime.UtcDateTime,
            durationMinutes: durationMinutes,
            location:        location ?? "Main Office",
            sequenceNumber:  sequenceNumber + 1);

        evt.Status = EventStatus.Cancelled;
        calendar.Events.Add(evt);
        return Serialize(calendar);
    }

    // ── private helpers ───────────────────────────────────────────────────────

    private Calendar BuildCalendar(string method)
    {
        var calendar = new Calendar();
        // PRODID and METHOD are top-level calendar properties (RFC 5545 §3.7).
        calendar.AddProperty("PRODID", _options.ProductId);
        calendar.AddProperty("METHOD", method);
        return calendar;
    }

    private CalendarEvent BuildBaseEvent(
        Guid     appointmentId,
        string   summary,
        string   description,
        DateTime startTimeUtc,
        int      durationMinutes,
        string   location,
        int      sequenceNumber)
    {
        // Edge case: explicit TZID prevents timezone conversion errors across clients.
        var tzId = _options.DefaultTimezone;

        var calendarEvent = new CalendarEvent
        {
            Uid         = appointmentId.ToString(),
            Summary     = summary,
            Description = description,
            Location    = location,
            DtStart     = new CalDateTime(startTimeUtc, tzId),
            DtEnd       = new CalDateTime(startTimeUtc.AddMinutes(durationMinutes), tzId),
            Sequence    = sequenceNumber,
            Organizer   = new Organizer
            {
                Value = new Uri($"mailto:{_options.OrganizerEmail}")
            },
        };

        // 30-minute display alarm for all event types.
        calendarEvent.Alarms.Add(new Alarm
        {
            Action      = AlarmAction.Display,
            Trigger     = new Trigger(TimeSpan.FromMinutes(-30)),
            Description = $"Upcoming: {summary}",
        });

        return calendarEvent;
    }

    private static string FormatDescription(string? providerName, string confirmationCode)
        => $"Provider: {providerName ?? "TBD"}\nConfirmation: {confirmationCode}";

    private static byte[] Serialize(Calendar calendar)
    {
        var serializer = new CalendarSerializer();
        var icsText    = serializer.SerializeToString(calendar);
        return System.Text.Encoding.UTF8.GetBytes(icsText);
    }
}

