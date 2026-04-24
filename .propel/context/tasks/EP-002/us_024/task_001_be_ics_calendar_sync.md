# Task - TASK_001

## Requirement Reference

- User Story: us_024
- Story Location: .propel/context/tasks/EP-002/us_024/us_024.md
- Acceptance Criteria:
  - AC-1: Given I have a confirmed appointment, When I click "Add to Calendar" on the confirmation page or in the confirmation email, Then an ICS file is generated containing the appointment title, date, time, duration, and location.
  - AC-2: Given an ICS file is generated, When I open it in Google Calendar, Outlook, or Apple Calendar, Then the event is imported correctly with all appointment details.
  - AC-3: Given my appointment is rescheduled, When I export the updated ICS, Then the ICS contains the updated date and time with a `SEQUENCE` increment so calendar apps recognize it as an update.
  - AC-4: Given my appointment is cancelled, When the cancellation is processed, Then a cancellation ICS (with `STATUS:CANCELLED`) is included in the cancellation email so calendar apps remove the event.
- Edge Cases:
  - What happens if the ICS generation fails? Booking confirmation is still delivered; ICS availability is retried and delivered in a follow-up email.
  - How does the system handle timezones? ICS events include explicit TZID to avoid timezone conversion errors across different calendar clients.

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
| Library | Npgsql.EntityFrameworkCore.PostgreSQL | latest stable |
| Library | Ical.Net | latest stable |
| Library | Polly | latest stable |
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

Enhance the existing `IcsGenerator` (from US_021 task_002) with full RFC 5545 compliance for cross-client calendar synchronization. The existing generator produces basic ICS files for new bookings but lacks: (1) explicit TZID timezone identifiers causing conversion errors across calendar clients (edge case), (2) `SEQUENCE` property tracking for rescheduled appointments so Google Calendar, Outlook, and Apple Calendar recognize updates rather than creating duplicate events (AC-3), and (3) `STATUS:CANCELLED` with `METHOD:CANCEL` for cancellation ICS so calendar apps automatically remove events (AC-4). This task adds a `SequenceNumber` column to the `Appointment` entity, introduces `GenerateUpdateIcs` and `GenerateCancellationIcs` methods on `IcsGenerator`, integrates the updated ICS into the `BookingRescheduledEvent` handler (from US_022 task_001) for email delivery of the updated ICS, and integrates the cancellation ICS into the `BookingCancelledEvent` handler for email delivery with the removal ICS. ICS generation failures are non-blocking — the booking confirmation is delivered regardless, and ICS is retried and sent in a follow-up email (edge case, using Polly retry per Decision 6).

## Dependent Tasks

- US_021 task_002 (requires IcsGenerator, ConfirmationArtifactService, IArtifactStorage, BookingConfirmedEventHandler)
- US_022 task_001 (requires BookingRescheduledEvent, BookingCancelledEvent, cancel/reschedule endpoints)
- US_021 task_001 (requires Appointment entity with BookingService)

## Impacted Components

- Modify: `server/src/PropelIQ.Application/Booking/Artifacts/IcsGenerator.cs` (add TZID, SEQUENCE, PRODID, METHOD, cancellation method)
- Modify: `server/src/PropelIQ.Domain/Entities/Appointment.cs` (add SequenceNumber column)
- New: `server/src/PropelIQ.Application/Booking/Artifacts/IcsOptions.cs` (configuration for PRODID, default timezone)
- Modify: `server/src/PropelIQ.Infrastructure/Booking/BookingRescheduledEventHandler.cs` (generate updated ICS with SEQUENCE increment)
- Modify: `server/src/PropelIQ.Infrastructure/Booking/BookingCancelledEventHandler.cs` (generate cancellation ICS with STATUS:CANCELLED)
- Modify: `server/src/PropelIQ.Application/Booking/BookingService.cs` (increment SequenceNumber on reschedule)
- Modify: `server/src/PropelIQ.Infrastructure/AppDbContext.cs` (add SequenceNumber column mapping)

## Implementation Plan

1. **Add `SequenceNumber` to Appointment entity**:

```csharp
// server/src/PropelIQ.Domain/Entities/Appointment.cs
// Add to existing Appointment entity
public int SequenceNumber { get; set; } = 0;
```

```csharp
// In AppDbContext.OnModelCreating — add to Appointment configuration
entity.Property(e => e.SequenceNumber)
    .HasDefaultValue(0);
```

2. **Create ICS configuration options**:

```csharp
// server/src/PropelIQ.Application/Booking/Artifacts/IcsOptions.cs
namespace PropelIQ.Application.Booking.Artifacts;

public class IcsOptions
{
    public const string SectionName = "Ics";

    /// <summary>PRODID per RFC 5545 Section 3.7.3</summary>
    public string ProductId { get; set; }
        = "-//PropelIQ//Appointment Scheduler//EN";

    /// <summary>IANA timezone identifier (e.g., "America/New_York")</summary>
    public string DefaultTimezone { get; set; } = "America/New_York";

    /// <summary>Organizer email for METHOD:REQUEST</summary>
    public string OrganizerEmail { get; set; }
        = "noreply@propeliq.com";
}
```

3. **Enhance `IcsGenerator` with RFC 5545 compliance**:

```csharp
// server/src/PropelIQ.Application/Booking/Artifacts/IcsGenerator.cs
using Ical.Net;
using Ical.Net.CalendarComponents;
using Ical.Net.DataTypes;
using Ical.Net.Serialization;
using Microsoft.Extensions.Options;

namespace PropelIQ.Application.Booking.Artifacts;

public class IcsGenerator
{
    private readonly IcsOptions _options;

    public IcsGenerator(IOptions<IcsOptions> options)
    {
        _options = options.Value;
    }

    // AC-1, AC-2: Generate initial booking ICS with full RFC 5545 compliance
    public byte[] GenerateBookingIcs(BookingConfirmedEvent booking)
    {
        var calendar = CreateCalendar("REQUEST");
        var calendarEvent = CreateBaseEvent(
            appointmentId: booking.AppointmentId,
            summary: $"Appointment - {booking.AppointmentType}",
            description: FormatDescription(
                booking.ProviderName, booking.ConfirmationCode),
            startTimeUtc: booking.AppointmentTime,
            durationMinutes: booking.DurationMinutes,
            location: booking.Location ?? "Main Office",
            sequenceNumber: 0);

        calendar.Events.Add(calendarEvent);
        return Serialize(calendar);
    }

    // AC-3: Generate updated ICS with SEQUENCE increment for reschedule
    public byte[] GenerateUpdateIcs(
        Guid appointmentId,
        string appointmentType,
        string? providerName,
        string confirmationCode,
        DateTime newStartTimeUtc,
        int durationMinutes,
        string? location,
        int sequenceNumber)
    {
        var calendar = CreateCalendar("REQUEST");
        var calendarEvent = CreateBaseEvent(
            appointmentId: appointmentId,
            summary: $"Appointment - {appointmentType} (Rescheduled)",
            description: FormatDescription(providerName, confirmationCode)
                + "\nThis event has been rescheduled.",
            startTimeUtc: newStartTimeUtc,
            durationMinutes: durationMinutes,
            location: location ?? "Main Office",
            sequenceNumber: sequenceNumber);

        calendar.Events.Add(calendarEvent);
        return Serialize(calendar);
    }

    // AC-4: Generate cancellation ICS with STATUS:CANCELLED
    public byte[] GenerateCancellationIcs(
        Guid appointmentId,
        string appointmentType,
        string? providerName,
        string confirmationCode,
        DateTime originalStartTimeUtc,
        int durationMinutes,
        string? location,
        int sequenceNumber)
    {
        var calendar = CreateCalendar("CANCEL");
        var calendarEvent = CreateBaseEvent(
            appointmentId: appointmentId,
            summary: $"CANCELLED: Appointment - {appointmentType}",
            description: FormatDescription(providerName, confirmationCode)
                + "\nThis appointment has been cancelled.",
            startTimeUtc: originalStartTimeUtc,
            durationMinutes: durationMinutes,
            location: location ?? "Main Office",
            sequenceNumber: sequenceNumber + 1);

        calendarEvent.Status = EventStatus.Cancelled;
        calendar.Events.Add(calendarEvent);
        return Serialize(calendar);
    }

    private Calendar CreateCalendar(string method)
    {
        var calendar = new Calendar();
        calendar.AddProperty("PRODID", _options.ProductId);
        calendar.AddProperty("METHOD", method);
        return calendar;
    }

    private CalendarEvent CreateBaseEvent(
        Guid appointmentId,
        string summary,
        string description,
        DateTime startTimeUtc,
        int durationMinutes,
        string location,
        int sequenceNumber)
    {
        // Edge case: Explicit TZID to avoid timezone conversion errors
        var tzId = _options.DefaultTimezone;

        var calendarEvent = new CalendarEvent
        {
            Summary = summary,
            Description = description,
            DtStart = new CalDateTime(startTimeUtc, tzId),
            DtEnd = new CalDateTime(
                startTimeUtc.AddMinutes(durationMinutes), tzId),
            Location = location,
            Uid = appointmentId.ToString(),
            Sequence = sequenceNumber
        };

        // Organizer for METHOD:REQUEST compliance
        calendarEvent.Organizer = new Organizer
        {
            Value = new Uri($"mailto:{_options.OrganizerEmail}")
        };

        // 30-minute reminder
        calendarEvent.Alarms.Add(new Alarm
        {
            Action = AlarmAction.Display,
            Trigger = new Trigger(TimeSpan.FromMinutes(-30)),
            Description = $"Upcoming: {summary}"
        });

        return calendarEvent;
    }

    private static string FormatDescription(
        string? providerName, string confirmationCode)
    {
        return $"Provider: {providerName ?? "TBD"}\n" +
               $"Confirmation: {confirmationCode}";
    }

    private static byte[] Serialize(Calendar calendar)
    {
        var serializer = new CalendarSerializer();
        var icsString = serializer.SerializeToString(calendar);
        return System.Text.Encoding.UTF8.GetBytes(icsString);
    }
}
```

4. **Increment `SequenceNumber` on reschedule in `BookingService`**:

```csharp
// In BookingService.RescheduleBookingAsync — add before SaveChangesAsync
appointment.SequenceNumber += 1;
```

5. **Integrate updated ICS into `BookingRescheduledEventHandler`**:

```csharp
// In BookingRescheduledEventHandler.HandleAsync
// After slot swap and appointment update:
var updatedIcsBytes = _icsGenerator.GenerateUpdateIcs(
    appointmentId: evt.AppointmentId,
    appointmentType: evt.AppointmentType,
    providerName: evt.ProviderName,
    confirmationCode: evt.ConfirmationCode,
    newStartTimeUtc: evt.NewAppointmentTime,
    durationMinutes: evt.DurationMinutes,
    location: evt.Location,
    sequenceNumber: evt.SequenceNumber);

// Store updated ICS
var icsPath = await _artifactStorage.StoreAsync(
    $"bookings/{evt.AppointmentId}",
    "appointment.ics",
    updatedIcsBytes,
    "text/calendar",
    ct);

// Attach to reschedule confirmation email
await _emailService.SendRescheduleConfirmationAsync(
    evt.PatientEmail, evt, icsPath, ct);
```

6. **Integrate cancellation ICS into `BookingCancelledEventHandler`**:

```csharp
// In BookingCancelledEventHandler.HandleAsync
var cancellationIcsBytes = _icsGenerator.GenerateCancellationIcs(
    appointmentId: evt.AppointmentId,
    appointmentType: evt.AppointmentType,
    providerName: evt.ProviderName,
    confirmationCode: evt.ConfirmationCode,
    originalStartTimeUtc: evt.AppointmentTime,
    durationMinutes: evt.DurationMinutes,
    location: evt.Location,
    sequenceNumber: evt.SequenceNumber);

// Store cancellation ICS
var icsPath = await _artifactStorage.StoreAsync(
    $"bookings/{evt.AppointmentId}",
    "cancellation.ics",
    cancellationIcsBytes,
    "text/calendar",
    ct);

// Attach to cancellation email so calendar apps remove the event
await _emailService.SendCancellationConfirmationAsync(
    evt.PatientEmail, evt, icsPath, ct);
```

7. **Add ICS generation failure resilience** (edge case):

```csharp
// In ConfirmationArtifactService — wrap ICS generation with Polly retry
// ICS failure is non-blocking: booking confirmation still delivered
private async Task GenerateIcsWithRetryAsync(
    BookingConfirmedEvent booking, CancellationToken ct)
{
    try
    {
        var icsBytes = _icsGenerator.GenerateBookingIcs(booking);
        await _artifactStorage.StoreAsync(
            $"bookings/{booking.AppointmentId}",
            "appointment.ics", icsBytes, "text/calendar", ct);
    }
    catch (Exception ex)
    {
        _logger.LogWarning(ex,
            "ICS generation failed for booking {AppointmentId}. " +
            "Scheduling follow-up retry.",
            booking.AppointmentId);

        // Queue follow-up ICS delivery via channel
        await _icsRetryChannel.Writer.WriteAsync(
            new IcsRetryMessage
            {
                AppointmentId = booking.AppointmentId,
                Booking = booking,
                AttemptCount = 0
            }, ct);
    }
}
```

8. **Register ICS configuration in DI**:

```csharp
// In DependencyInjection.cs or Program.cs
services.Configure<IcsOptions>(
    configuration.GetSection(IcsOptions.SectionName));
services.AddScoped<IcsGenerator>();
```

## Current Project State

```text
propelIQ/
└── server/
    └── src/
        ├── PropelIQ.Api/
        │   └── Controllers/
        │       └── BookingController.cs         (existing — artifacts endpoint)
        ├── PropelIQ.Application/
        │   └── Booking/
        │       ├── BookingService.cs            (modify — increment SequenceNumber)
        │       └── Artifacts/
        │           ├── ConfirmationArtifactService.cs  (modify — ICS retry)
        │           ├── IcsGenerator.cs          (modify — SEQUENCE, CANCELLED, TZID)
        │           ├── IcsOptions.cs            (new)
        │           ├── IArtifactStorage.cs       (existing)
        │           ├── PdfGenerator.cs           (existing)
        │           └── QrCodeGenerator.cs        (existing)
        ├── PropelIQ.Domain/
        │   ├── Entities/
        │   │   └── Appointment.cs               (modify — add SequenceNumber)
        │   └── Events/
        │       ├── BookingConfirmedEvent.cs      (existing)
        │       ├── BookingRescheduledEvent.cs    (existing)
        │       └── BookingCancelledEvent.cs      (existing)
        └── PropelIQ.Infrastructure/
            ├── Booking/
            │   ├── ArtifactStorage.cs            (existing)
            │   ├── BookingRescheduledEventHandler.cs (modify — attach updated ICS)
            │   └── BookingCancelledEventHandler.cs   (modify — attach cancellation ICS)
            ├── AppDbContext.cs                   (modify — SequenceNumber mapping)
            └── DependencyInjection.cs            (modify — register IcsOptions)
```

> Placeholder: Update on execution based on US_021 and US_022 task completion state.

## Expected Changes

| Action | File Path | Description |
|--------|-----------|-------------|
| MODIFY | server/src/PropelIQ.Domain/Entities/Appointment.cs | Add `SequenceNumber` property (int, default 0) |
| CREATE | server/src/PropelIQ.Application/Booking/Artifacts/IcsOptions.cs | Configuration for PRODID, default timezone, organizer email |
| MODIFY | server/src/PropelIQ.Application/Booking/Artifacts/IcsGenerator.cs | Add TZID, PRODID, METHOD, SEQUENCE; add `GenerateUpdateIcs` and `GenerateCancellationIcs` methods |
| MODIFY | server/src/PropelIQ.Application/Booking/BookingService.cs | Increment `SequenceNumber` on reschedule |
| MODIFY | server/src/PropelIQ.Infrastructure/Booking/BookingRescheduledEventHandler.cs | Generate and attach updated ICS to reschedule email |
| MODIFY | server/src/PropelIQ.Infrastructure/Booking/BookingCancelledEventHandler.cs | Generate and attach cancellation ICS to cancel email |
| MODIFY | server/src/PropelIQ.Infrastructure/AppDbContext.cs | Add SequenceNumber column mapping with default value |

## External References

- RFC 5545 (iCalendar): https://datatracker.ietf.org/doc/html/rfc5545
- Ical.Net GitHub: https://github.com/rianjs/ical.net
- Google Calendar ICS import: https://support.google.com/calendar/answer/37118

## Build Commands

```bash
# Build backend
cd server/src/PropelIQ.Api
dotnet build

# Run backend
dotnet run

# Test ICS download (AC-1)
curl -X GET "http://localhost:5000/api/v1/bookings/<booking-id>/artifacts/ics" \
  -H "Authorization: Bearer <jwt>" \
  -o appointment.ics
# Expected: ICS file with PRODID, METHOD:REQUEST, TZID, SEQUENCE:0

# Verify ICS opens in calendar app (AC-2)
# Open appointment.ics in Google Calendar / Outlook / Apple Calendar
# Expected: Event imported with title, date, time, duration, location
```

## Implementation Validation Strategy

- [ ] Initial booking ICS contains PRODID, METHOD:REQUEST, explicit TZID, SEQUENCE:0 (AC-1, AC-2)
- [ ] ICS opens correctly in Google Calendar with all appointment details (AC-2)
- [ ] ICS opens correctly in Outlook with all appointment details (AC-2)
- [ ] ICS opens correctly in Apple Calendar with all appointment details (AC-2)
- [ ] Rescheduled appointment ICS contains updated date/time and incremented SEQUENCE (AC-3)
- [ ] Calendar apps recognize rescheduled ICS as update (not duplicate) via UID + SEQUENCE (AC-3)
- [ ] Cancellation ICS contains STATUS:CANCELLED and METHOD:CANCEL (AC-4)
- [ ] Calendar apps remove event when cancellation ICS is imported (AC-4)
- [ ] ICS generation failure does not block booking confirmation delivery (edge case)

## Implementation Checklist

- [x] Add `SequenceNumber` property to `Appointment` entity with default 0
- [x] Create `IcsOptions` configuration class for PRODID, default timezone, organizer
- [x] Enhance `IcsGenerator` with TZID, PRODID, METHOD, SEQUENCE, and Organizer
- [x] Add `GenerateUpdateIcs` method with SEQUENCE increment for reschedule (AC-3)
- [x] Add `GenerateCancellationIcs` method with STATUS:CANCELLED and METHOD:CANCEL (AC-4)
- [x] Integrate updated ICS into `BookingRescheduledEventHandler` email delivery
- [x] Integrate cancellation ICS into `BookingCancelledEventHandler` email delivery
- [x] Add ICS generation failure retry with follow-up email delivery (edge case)
