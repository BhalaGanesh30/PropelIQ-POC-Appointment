using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;
using PropelIQ.Modules.Scheduling.Domain.Events;

namespace PropelIQ.Modules.Scheduling.Infrastructure.Booking;

/// <summary>
/// Generates the PDF appointment confirmation document using QuestPDF.
/// AC-3: PDF must contain date, time, duration, type, and provider name.
/// </summary>
public sealed class PdfGenerator
{
    static PdfGenerator()
    {
        // QuestPDF community license — free for non-commercial / open-source use.
        QuestPDF.Settings.License = LicenseType.Community;
    }

    public byte[] GenerateConfirmationPdf(BookingConfirmedEvent booking)
    {
        var document = Document.Create(container =>
        {
            container.Page(page =>
            {
                page.Size(PageSizes.A4);
                page.Margin(40);

                page.Header()
                    .Text("Appointment Confirmation")
                    .FontSize(24).Bold().FontColor(Colors.Blue.Medium);

                page.Content().PaddingVertical(20).Column(col =>
                {
                    col.Spacing(12);

                    col.Item()
                        .Text($"Confirmation Code: {booking.ConfirmationCode}")
                        .FontSize(16).Bold();

                    col.Item().LineHorizontal(1).LineColor(Colors.Grey.Medium);

                    AddDetailRow(col, "Date",
                        booking.AppointmentTime.ToString("dddd, MMMM d, yyyy"));
                    AddDetailRow(col, "Time",
                        booking.AppointmentTime.ToString("h:mm tt"));
                    AddDetailRow(col, "Duration",
                        $"{booking.DurationMinutes} minutes");
                    AddDetailRow(col, "Type", booking.AppointmentType);
                    AddDetailRow(col, "Provider", booking.ProviderName ?? "TBD");
                    AddDetailRow(col, "Location", booking.Location ?? "Main Office");
                });

                page.Footer().AlignCenter()
                    .Text($"Generated on {DateTimeOffset.UtcNow:yyyy-MM-dd HH:mm} UTC")
                    .FontSize(8).FontColor(Colors.Grey.Medium);
            });
        });

        return document.GeneratePdf();
    }

    private static void AddDetailRow(ColumnDescriptor col, string label, string value)
    {
        col.Item().Row(row =>
        {
            row.RelativeItem(1).Text(label + ":").FontSize(12).Bold();
            row.RelativeItem(2).Text(value).FontSize(12);
        });
    }
}
