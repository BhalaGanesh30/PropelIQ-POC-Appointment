# Task - TASK_002

## Requirement Reference

- User Story: us_021
- Story Location: .propel/context/tasks/EP-002/us_021/us_021.md
- Acceptance Criteria:
  - AC-2: Given the booking is confirmed, When the confirmation email is sent, Then it contains a PDF appointment summary, a scannable QR code uniquely identifying the appointment, and an ICS calendar file attachment.
  - AC-3: Given I access the confirmation page, When I click "Download PDF," Then the PDF downloads immediately containing appointment date, time, duration, type, and provider name.
- Edge Cases:
  - How does the system handle PDF generation failure? Booking confirmation is still returned to the user; PDF generation is retried asynchronously and delivered via email when available.
  - What happens if the confirmation email fails to send? Booking is still persisted; email delivery is retried up to 3 times with exponential backoff; failure is logged and patient can access confirmation from their dashboard.

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
| Library | QuestPDF | latest stable |
| Library | QRCoder | latest stable |
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

Implement the confirmation artifact generation pipeline that consumes `BookingConfirmedEvent` (from task_001) and produces three artifacts: (1) a PDF appointment summary (AC-2, AC-3), (2) a QR code image encoding the appointment confirmation code (AC-2), and (3) an ICS calendar file (AC-2). These are generated asynchronously by a background worker, persisted to blob storage, and delivered via email with all three attachments. The system uses Polly retry policies (3 attempts, exponential backoff) for email delivery per the edge case requirement and design decision 6. PDF generation failures are retried asynchronously and the booking remains confirmed regardless. The `GET /api/v1/bookings/{id}/artifacts/{type}` endpoint serves artifact downloads on-demand for the confirmation page (AC-3). Artifact metadata (storage paths, generation status) is tracked on the `Appointment` entity and audit-logged per NFR-010.

## Dependent Tasks

- US_021 task_001 (requires Appointment entity, BookingConfirmedEvent, BookingRepository)
- US_014 task_001 (requires JWT authentication middleware)

## Impacted Components

- New: `server/src/PropelIQ.Application/Booking/Artifacts/ConfirmationArtifactService.cs` (orchestrates PDF, QR, ICS generation)
- New: `server/src/PropelIQ.Application/Booking/Artifacts/PdfGenerator.cs` (QuestPDF-based PDF builder)
- New: `server/src/PropelIQ.Application/Booking/Artifacts/QrCodeGenerator.cs` (QRCoder-based QR generation)
- New: `server/src/PropelIQ.Application/Booking/Artifacts/IcsGenerator.cs` (Ical.Net-based ICS builder)
- New: `server/src/PropelIQ.Application/Booking/Artifacts/IArtifactStorage.cs` (blob storage abstraction)
- New: `server/src/PropelIQ.Infrastructure/Booking/ArtifactStorage.cs` (file system / blob storage impl)
- New: `server/src/PropelIQ.Infrastructure/Booking/BookingConfirmedEventHandler.cs` (background event consumer)
- New: `server/src/PropelIQ.Application/Booking/Artifacts/IConfirmationEmailService.cs` (email abstraction)
- New: `server/src/PropelIQ.Infrastructure/Booking/ConfirmationEmailService.cs` (email with Polly retry)
- Modify: `server/src/PropelIQ.Api/Controllers/BookingController.cs` (add artifact download endpoint)
- Modify: `server/src/PropelIQ.Domain/Entities/Appointment.cs` (add artifact storage path fields)
- Modify: `server/src/PropelIQ.Infrastructure/DependencyInjection.cs` (register artifact services)

## Implementation Plan

1. **Extend `Appointment` entity** with artifact tracking fields:

```csharp
// Add to server/src/PropelIQ.Domain/Entities/Appointment.cs
public string? PdfStoragePath { get; set; }
public string? QrCodeStoragePath { get; set; }
public string? IcsStoragePath { get; set; }
public DateTime? ArtifactsGeneratedAt { get; set; }
public int EmailRetryCount { get; set; } = 0;
public bool EmailSent { get; set; } = false;
```

2. **Create PDF generator** using QuestPDF:

```csharp
// server/src/PropelIQ.Application/Booking/Artifacts/PdfGenerator.cs
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;

namespace PropelIQ.Application.Booking.Artifacts;

public class PdfGenerator
{
    public byte[] GenerateConfirmationPdf(BookingConfirmedEvent booking)
    {
        // AC-3: PDF must contain date, time, duration, type, provider name
        var document = Document.Create(container =>
        {
            container.Page(page =>
            {
                page.Size(PageSizes.A4);
                page.Margin(40);

                page.Header().Text("Appointment Confirmation")
                    .FontSize(24).Bold().FontColor(Colors.Blue.Medium);

                page.Content().PaddingVertical(20).Column(col =>
                {
                    col.Spacing(12);

                    col.Item().Text($"Confirmation Code: " +
                        $"{booking.ConfirmationCode}")
                        .FontSize(16).Bold();

                    col.Item().LineHorizontal(1).LineColor(Colors.Grey.Medium);

                    AddDetailRow(col, "Date",
                        booking.AppointmentTime.ToString("dddd, MMMM d, yyyy"));
                    AddDetailRow(col, "Time",
                        booking.AppointmentTime.ToString("h:mm tt"));
                    AddDetailRow(col, "Duration",
                        $"{booking.DurationMinutes} minutes");
                    AddDetailRow(col, "Type",
                        booking.AppointmentType);
                    AddDetailRow(col, "Provider",
                        booking.ProviderName ?? "TBD");
                    AddDetailRow(col, "Location",
                        booking.Location ?? "Main Office");
                });

                page.Footer().AlignCenter()
                    .Text($"Generated on {DateTime.UtcNow:yyyy-MM-dd HH:mm} UTC")
                    .FontSize(8).FontColor(Colors.Grey.Medium);
            });
        });

        return document.GeneratePdf();
    }

    private static void AddDetailRow(
        ColumnDescriptor col, string label, string value)
    {
        col.Item().Row(row =>
        {
            row.RelativeItem(1).Text(label + ":")
                .FontSize(12).Bold();
            row.RelativeItem(2).Text(value)
                .FontSize(12);
        });
    }
}
```

3. **Create QR code generator**:

```csharp
// server/src/PropelIQ.Application/Booking/Artifacts/QrCodeGenerator.cs
using QRCoder;

namespace PropelIQ.Application.Booking.Artifacts;

public class QrCodeGenerator
{
    public byte[] GenerateQrCode(string confirmationCode, Guid appointmentId)
    {
        // AC-2: QR code uniquely identifies the appointment
        // Encodes: appointmentId|confirmationCode for scanner verification
        var payload = $"{appointmentId}|{confirmationCode}";

        using var qrGenerator = new QRCodeGenerator();
        using var qrCodeData = qrGenerator.CreateQrCode(
            payload, QRCodeGenerator.ECCLevel.M);
        using var qrCode = new PngByteQRCode(qrCodeData);

        return qrCode.GetGraphic(10); // 10px per module
    }
}
```

4. **Create ICS calendar file generator**:

```csharp
// server/src/PropelIQ.Application/Booking/Artifacts/IcsGenerator.cs
using Ical.Net;
using Ical.Net.CalendarComponents;
using Ical.Net.DataTypes;
using Ical.Net.Serialization;

namespace PropelIQ.Application.Booking.Artifacts;

public class IcsGenerator
{
    public byte[] GenerateIcsFile(BookingConfirmedEvent booking)
    {
        // AC-2: ICS calendar file attachment
        var calendar = new Calendar();

        var calendarEvent = new CalendarEvent
        {
            Summary = $"Appointment - {booking.AppointmentType}",
            Description = $"Provider: {booking.ProviderName ?? "TBD"}\n" +
                         $"Confirmation: {booking.ConfirmationCode}\n" +
                         $"Location: {booking.Location ?? "Main Office"}",
            DtStart = new CalDateTime(booking.AppointmentTime, "UTC"),
            DtEnd = new CalDateTime(
                booking.AppointmentTime
                    .AddMinutes(booking.DurationMinutes), "UTC"),
            Location = booking.Location ?? "Main Office",
            Uid = booking.AppointmentId.ToString()
        };

        // 30-minute reminder before appointment
        calendarEvent.Alarms.Add(new Alarm
        {
            Action = AlarmAction.Display,
            Trigger = new Trigger(TimeSpan.FromMinutes(-30)),
            Description = $"Upcoming appointment: {booking.AppointmentType}"
        });

        calendar.Events.Add(calendarEvent);

        var serializer = new CalendarSerializer();
        var icsString = serializer.SerializeToString(calendar);

        return System.Text.Encoding.UTF8.GetBytes(icsString);
    }
}
```

5. **Create artifact storage abstraction**:

```csharp
// server/src/PropelIQ.Application/Booking/Artifacts/IArtifactStorage.cs
namespace PropelIQ.Application.Booking.Artifacts;

public interface IArtifactStorage
{
    Task<string> StoreAsync(
        string containerPath, string fileName,
        byte[] content, string contentType, CancellationToken ct);

    Task<byte[]?> RetrieveAsync(
        string storagePath, CancellationToken ct);
}
```

```csharp
// server/src/PropelIQ.Infrastructure/Booking/ArtifactStorage.cs
namespace PropelIQ.Infrastructure.Booking;

public class ArtifactStorage : IArtifactStorage
{
    private readonly string _basePath;

    public ArtifactStorage(IConfiguration config)
    {
        _basePath = config["Storage:ArtifactBasePath"]
            ?? Path.Combine(Directory.GetCurrentDirectory(), "artifacts");
        Directory.CreateDirectory(_basePath);
    }

    public async Task<string> StoreAsync(
        string containerPath, string fileName,
        byte[] content, string contentType, CancellationToken ct)
    {
        var directory = Path.Combine(_basePath, containerPath);
        Directory.CreateDirectory(directory);

        var filePath = Path.Combine(directory, fileName);
        await File.WriteAllBytesAsync(filePath, content, ct);

        return Path.Combine(containerPath, fileName);
    }

    public async Task<byte[]?> RetrieveAsync(
        string storagePath, CancellationToken ct)
    {
        var fullPath = Path.Combine(_basePath, storagePath);
        if (!File.Exists(fullPath)) return null;

        return await File.ReadAllBytesAsync(fullPath, ct);
    }
}
```

6. **Create `ConfirmationArtifactService`** orchestrating all three generators:

```csharp
// server/src/PropelIQ.Application/Booking/Artifacts/ConfirmationArtifactService.cs
using Microsoft.Extensions.Logging;

namespace PropelIQ.Application.Booking.Artifacts;

public class ConfirmationArtifactService
{
    private readonly PdfGenerator _pdfGenerator;
    private readonly QrCodeGenerator _qrCodeGenerator;
    private readonly IcsGenerator _icsGenerator;
    private readonly IArtifactStorage _storage;
    private readonly IConfirmationEmailService _emailService;
    private readonly ILogger<ConfirmationArtifactService> _logger;

    public ConfirmationArtifactService(
        PdfGenerator pdfGenerator,
        QrCodeGenerator qrCodeGenerator,
        IcsGenerator icsGenerator,
        IArtifactStorage storage,
        IConfirmationEmailService emailService,
        ILogger<ConfirmationArtifactService> logger)
    {
        _pdfGenerator = pdfGenerator;
        _qrCodeGenerator = qrCodeGenerator;
        _icsGenerator = icsGenerator;
        _storage = storage;
        _emailService = emailService;
        _logger = logger;
    }

    public async Task<ArtifactResult> GenerateAndStoreAsync(
        BookingConfirmedEvent booking, CancellationToken ct)
    {
        var containerPath = $"bookings/{booking.AppointmentId}";
        var result = new ArtifactResult();

        // Generate PDF (AC-2, AC-3)
        try
        {
            var pdfBytes = _pdfGenerator.GenerateConfirmationPdf(booking);
            result.PdfPath = await _storage.StoreAsync(
                containerPath, "confirmation.pdf",
                pdfBytes, "application/pdf", ct);
            result.PdfBytes = pdfBytes;
        }
        catch (Exception ex)
        {
            // Edge case: PDF generation failure — log and continue
            _logger.LogError(ex,
                "PDF generation failed for appointment {AppointmentId}. " +
                "Will retry asynchronously.",
                booking.AppointmentId);
        }

        // Generate QR code (AC-2)
        try
        {
            var qrBytes = _qrCodeGenerator.GenerateQrCode(
                booking.ConfirmationCode, booking.AppointmentId);
            result.QrCodePath = await _storage.StoreAsync(
                containerPath, "qrcode.png",
                qrBytes, "image/png", ct);
            result.QrCodeBytes = qrBytes;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex,
                "QR code generation failed for appointment {AppointmentId}",
                booking.AppointmentId);
        }

        // Generate ICS (AC-2)
        try
        {
            var icsBytes = _icsGenerator.GenerateIcsFile(booking);
            result.IcsPath = await _storage.StoreAsync(
                containerPath, "appointment.ics",
                icsBytes, "text/calendar", ct);
            result.IcsBytes = icsBytes;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex,
                "ICS generation failed for appointment {AppointmentId}",
                booking.AppointmentId);
        }

        return result;
    }

    public async Task SendConfirmationEmailAsync(
        BookingConfirmedEvent booking,
        ArtifactResult artifacts,
        CancellationToken ct)
    {
        // Edge case: email delivery with Polly retry (3 attempts, exp backoff)
        await _emailService.SendConfirmationAsync(
            booking, artifacts, ct);
    }
}

public class ArtifactResult
{
    public string? PdfPath { get; set; }
    public string? QrCodePath { get; set; }
    public string? IcsPath { get; set; }
    public byte[]? PdfBytes { get; set; }
    public byte[]? QrCodeBytes { get; set; }
    public byte[]? IcsBytes { get; set; }
    public bool AllGenerated =>
        PdfPath is not null && QrCodePath is not null && IcsPath is not null;
}
```

7. **Create email service with Polly retry**:

```csharp
// server/src/PropelIQ.Application/Booking/Artifacts/IConfirmationEmailService.cs
namespace PropelIQ.Application.Booking.Artifacts;

public interface IConfirmationEmailService
{
    Task SendConfirmationAsync(
        BookingConfirmedEvent booking,
        ArtifactResult artifacts,
        CancellationToken ct);
}
```

```csharp
// server/src/PropelIQ.Infrastructure/Booking/ConfirmationEmailService.cs
using Microsoft.Extensions.Logging;
using Polly;
using Polly.Retry;

namespace PropelIQ.Infrastructure.Booking;

public class ConfirmationEmailService : IConfirmationEmailService
{
    private readonly ILogger<ConfirmationEmailService> _logger;
    private readonly AsyncRetryPolicy _retryPolicy;

    public ConfirmationEmailService(
        ILogger<ConfirmationEmailService> logger)
    {
        _logger = logger;

        // Edge case: 3 retries with exponential backoff
        _retryPolicy = Policy
            .Handle<Exception>()
            .WaitAndRetryAsync(
                retryCount: 3,
                sleepDurationProvider: attempt =>
                    TimeSpan.FromSeconds(Math.Pow(2, attempt)),
                onRetry: (exception, timeSpan, retryCount, _) =>
                {
                    _logger.LogWarning(
                        exception,
                        "Email send attempt {RetryCount} failed. " +
                        "Retrying in {Delay}s.",
                        retryCount, timeSpan.TotalSeconds);
                });
    }

    public async Task SendConfirmationAsync(
        BookingConfirmedEvent booking,
        ArtifactResult artifacts,
        CancellationToken ct)
    {
        await _retryPolicy.ExecuteAsync(async () =>
        {
            // Build email with PDF, QR, and ICS attachments (AC-2)
            // Implementation depends on email provider (SendGrid, SMTP, etc.)
            _logger.LogInformation(
                "Sending confirmation email for appointment " +
                "{AppointmentId} to {Email}",
                booking.AppointmentId, booking.PatientEmail);

            // Placeholder: inject IEmailSender and send with attachments
            await Task.CompletedTask;
        });
    }
}
```

8. **Create background event handler**:

```csharp
// server/src/PropelIQ.Infrastructure/Booking/BookingConfirmedEventHandler.cs
using Microsoft.Extensions.Logging;
using System.Threading.Channels;

namespace PropelIQ.Infrastructure.Booking;

public class BookingConfirmedEventHandler : BackgroundService
{
    private readonly Channel<BookingConfirmedEvent> _channel;
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<BookingConfirmedEventHandler> _logger;

    public BookingConfirmedEventHandler(
        Channel<BookingConfirmedEvent> channel,
        IServiceScopeFactory scopeFactory,
        ILogger<BookingConfirmedEventHandler> logger)
    {
        _channel = channel;
        _scopeFactory = scopeFactory;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken ct)
    {
        await foreach (var evt in _channel.Reader.ReadAllAsync(ct))
        {
            try
            {
                using var scope = _scopeFactory.CreateScope();
                var artifactService = scope.ServiceProvider
                    .GetRequiredService<ConfirmationArtifactService>();
                var bookingRepo = scope.ServiceProvider
                    .GetRequiredService<IBookingRepository>();

                // Generate all artifacts
                var artifacts = await artifactService
                    .GenerateAndStoreAsync(evt, ct);

                // Update appointment with artifact paths
                // (delegated to repository update method)

                // Send confirmation email (with retry)
                await artifactService
                    .SendConfirmationEmailAsync(evt, artifacts, ct);

                _logger.LogInformation(
                    "Confirmation artifacts generated and email sent " +
                    "for appointment {AppointmentId}",
                    evt.AppointmentId);
            }
            catch (Exception ex)
            {
                // Edge case: booking persists even if artifact pipeline fails
                _logger.LogError(ex,
                    "Failed to process BookingConfirmedEvent for " +
                    "appointment {AppointmentId}. Booking remains valid.",
                    evt.AppointmentId);
            }
        }
    }
}
```

9. **Add artifact download endpoint** to `BookingController`:

```csharp
// Add to BookingController.cs

[HttpGet("{id:guid}/artifacts/{type}")]
[ProducesResponseType(StatusCodes.Status200OK)]
[ProducesResponseType(StatusCodes.Status404NotFound)]
public async Task<IActionResult> DownloadArtifact(
    Guid id,
    [FromRoute] string type,
    CancellationToken ct)
{
    // AC-3: PDF downloads immediately on click
    var validTypes = new Dictionary<string, (string ContentType, string FileName)>
    {
        ["pdf"] = ("application/pdf", "confirmation.pdf"),
        ["qr"] = ("image/png", "qrcode.png"),
        ["ics"] = ("text/calendar", "appointment.ics")
    };

    if (!validTypes.TryGetValue(type.ToLowerInvariant(), out var meta))
        return BadRequest("Invalid artifact type. Use: pdf, qr, ics.");

    var storagePath = $"bookings/{id}/{meta.FileName}";
    var artifactStorage = HttpContext.RequestServices
        .GetRequiredService<IArtifactStorage>();

    var bytes = await artifactStorage.RetrieveAsync(storagePath, ct);
    if (bytes is null)
        return NotFound("Artifact not yet generated.");

    return File(bytes, meta.ContentType, meta.FileName);
}
```

10. **Register services** in DI container:

```csharp
// Add to DependencyInjection.cs
services.AddSingleton(Channel.CreateUnbounded<BookingConfirmedEvent>());
services.AddScoped<PdfGenerator>();
services.AddScoped<QrCodeGenerator>();
services.AddScoped<IcsGenerator>();
services.AddScoped<ConfirmationArtifactService>();
services.AddScoped<IArtifactStorage, ArtifactStorage>();
services.AddScoped<IConfirmationEmailService, ConfirmationEmailService>();
services.AddHostedService<BookingConfirmedEventHandler>();
```

## Current Project State

```text
propelIQ/
└── server/
    └── src/
        ├── PropelIQ.Api/
        │   └── Controllers/
        │       └── BookingController.cs           (modify — add artifact endpoint)
        ├── PropelIQ.Application/
        │   └── Booking/
        │       ├── Artifacts/                      (new module)
        │       │   ├── ConfirmationArtifactService.cs
        │       │   ├── PdfGenerator.cs
        │       │   ├── QrCodeGenerator.cs
        │       │   ├── IcsGenerator.cs
        │       │   ├── IArtifactStorage.cs
        │       │   └── IConfirmationEmailService.cs
        │       ├── BookingService.cs               (existing from task_001)
        │       └── Dto/
        ├── PropelIQ.Domain/
        │   ├── Entities/
        │   │   └── Appointment.cs                  (modify — add artifact paths)
        │   └── Events/
        └── PropelIQ.Infrastructure/
            └── Booking/
                ├── BookingRepository.cs            (existing from task_001)
                ├── ArtifactStorage.cs              (new)
                ├── ConfirmationEmailService.cs     (new)
                └── BookingConfirmedEventHandler.cs  (new)
```

> Placeholder: Update on execution based on task_001 completion state.

## Expected Changes

| Action | File Path | Description |
|--------|-----------|-------------|
| MODIFY | server/src/PropelIQ.Domain/Entities/Appointment.cs | Add PdfStoragePath, QrCodeStoragePath, IcsStoragePath, EmailSent fields |
| CREATE | server/src/PropelIQ.Application/Booking/Artifacts/PdfGenerator.cs | QuestPDF-based PDF with date, time, duration, type, provider |
| CREATE | server/src/PropelIQ.Application/Booking/Artifacts/QrCodeGenerator.cs | QRCoder PNG with appointmentId and confirmation code payload |
| CREATE | server/src/PropelIQ.Application/Booking/Artifacts/IcsGenerator.cs | Ical.Net ICS with event, location, and 30-min alarm |
| CREATE | server/src/PropelIQ.Application/Booking/Artifacts/IArtifactStorage.cs | Blob storage abstraction for store/retrieve |
| CREATE | server/src/PropelIQ.Application/Booking/Artifacts/ConfirmationArtifactService.cs | Orchestrates PDF, QR, ICS generation with per-artifact error isolation |
| CREATE | server/src/PropelIQ.Application/Booking/Artifacts/IConfirmationEmailService.cs | Email delivery abstraction |
| CREATE | server/src/PropelIQ.Infrastructure/Booking/ArtifactStorage.cs | File-system artifact storage implementation |
| CREATE | server/src/PropelIQ.Infrastructure/Booking/ConfirmationEmailService.cs | Polly retry (3 attempts, exponential backoff) email delivery |
| CREATE | server/src/PropelIQ.Infrastructure/Booking/BookingConfirmedEventHandler.cs | Background worker consuming BookingConfirmedEvent channel |
| MODIFY | server/src/PropelIQ.Api/Controllers/BookingController.cs | Add GET /api/v1/bookings/{id}/artifacts/{type} download endpoint |
| MODIFY | server/src/PropelIQ.Infrastructure/DependencyInjection.cs | Register artifact generators, storage, email, background worker |

## External References

- QuestPDF documentation: https://www.questpdf.com/documentation/getting-started.html
- QRCoder GitHub: https://github.com/codebude/QRCoder
- Ical.Net documentation: https://github.com/rianjs/ical.net
- Polly retry documentation: https://github.com/App-vNext/Polly#retry

## Build Commands

```bash
# Install NuGet packages
cd server/src/PropelIQ.Application
dotnet add package QuestPDF
dotnet add package QRCoder
dotnet add package Ical.Net

cd ../PropelIQ.Infrastructure
dotnet add package Polly

# Build
cd ../PropelIQ.Api
dotnet build

# Test artifact download
curl -X GET "http://localhost:5000/api/v1/bookings/<appointment-guid>/artifacts/pdf" \
  -H "Authorization: Bearer <jwt>" \
  --output confirmation.pdf

# Expected: PDF file downloaded with appointment details
```

## Implementation Validation Strategy

- [ ] `PdfGenerator` produces a valid PDF containing date, time, duration, type, and provider (AC-3)
- [ ] `QrCodeGenerator` produces a scannable PNG QR code encoding `{appointmentId}|{confirmationCode}` (AC-2)
- [ ] `IcsGenerator` produces a valid ICS file with correct event time, duration, summary, and alarm (AC-2)
- [ ] `ConfirmationArtifactService` generates all three artifacts and stores them via `IArtifactStorage`
- [ ] Per-artifact error isolation: failure in one generator does not block others (edge case)
- [ ] `ConfirmationEmailService` retries 3 times with exponential backoff on failure (edge case)
- [ ] `BookingConfirmedEventHandler` processes events from the channel without blocking the booking API
- [ ] Booking remains confirmed even if entire artifact pipeline fails (edge case)
- [ ] `GET /api/v1/bookings/{id}/artifacts/pdf` returns the stored PDF file (AC-3)
- [ ] Artifact download requires JWT authentication
- [ ] All artifact generation and email send operations are audit-logged

## Implementation Checklist

- [ ] Add artifact storage path fields to `Appointment` entity
- [ ] Create `PdfGenerator` with QuestPDF producing appointment summary PDF
- [ ] Create `QrCodeGenerator` encoding appointment ID and confirmation code
- [ ] Create `IcsGenerator` with Ical.Net producing calendar event with alarm
- [ ] Create `ConfirmationArtifactService` orchestrating generation with per-artifact error isolation
- [ ] Create `ConfirmationEmailService` with Polly 3-retry exponential backoff
- [ ] Create `BookingConfirmedEventHandler` background worker consuming event channel
- [ ] Add artifact download endpoint `GET /api/v1/bookings/{id}/artifacts/{type}`
