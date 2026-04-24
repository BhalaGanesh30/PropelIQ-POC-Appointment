using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;
using PropelIQ.Modules.Scheduling.Application.Appointments.Dto;

namespace PropelIQ.Modules.Scheduling.Infrastructure.Appointments;

/// <summary>
/// Generates the appointment history PDF report using QuestPDF.
/// AC-4: Receives an <see cref="IEnumerable{T}"/> of all filtered appointments
/// (not just the current page) so the PDF contains the complete result set.
/// The entire document is rendered in memory and returned as a byte array.
/// PDF generation must complete within 5 seconds per the acceptance criteria.
///
/// QuestPDF community license is set once via the static constructor.
/// </summary>
public sealed class AppointmentHistoryPdfGenerator
{
    static AppointmentHistoryPdfGenerator()
    {
        QuestPDF.Settings.License = LicenseType.Community;
    }

    /// <summary>
    /// Renders all <paramref name="appointments"/> into a single PDF.
    /// </summary>
    /// <param name="appointments">Complete filtered set — not paginated.</param>
    /// <param name="filter">Applied filter parameters shown in the report header.</param>
    /// <returns>PDF bytes.</returns>
    public byte[] Generate(
        IReadOnlyList<AppointmentHistoryItem> appointments,
        AppointmentHistoryFilter filter)
    {
        return Document.Create(container =>
        {
            container.Page(page =>
            {
                page.Size(PageSizes.A4);
                page.Margin(40);

                page.Header()
                    .Column(header =>
                    {
                        header.Item()
                            .Text("Appointment History")
                            .FontSize(22).Bold()
                            .FontColor(Colors.Blue.Medium);

                        header.Item()
                            .Text(BuildFilterSummary(filter))
                            .FontSize(10)
                            .FontColor(Colors.Grey.Darken2);
                    });

                page.Content()
                    .PaddingVertical(16)
                    .Column(col =>
                    {
                        col.Spacing(0);

                        // Table header row.
                        col.Item().Table(table =>
                        {
                            DefineColumns(table);
                            AddHeaderRow(table);

                            foreach (var apt in appointments)
                                AddDataRow(table, apt);
                        });

                        if (appointments.Count == 0)
                        {
                            col.Item()
                                .PaddingTop(20)
                                .AlignCenter()
                                .Text("No appointments found for the selected filters.")
                                .FontSize(12)
                                .FontColor(Colors.Grey.Medium);
                        }
                    });

                page.Footer()
                    .AlignRight()
                    .Text(text =>
                    {
                        text.Span("Page ").FontSize(10).FontColor(Colors.Grey.Medium);
                        text.CurrentPageNumber().FontSize(10).FontColor(Colors.Grey.Medium);
                        text.Span(" of ").FontSize(10).FontColor(Colors.Grey.Medium);
                        text.TotalPages().FontSize(10).FontColor(Colors.Grey.Medium);
                    });
            });
        }).GeneratePdf();
    }

    // ── Private helpers ──────────────────────────────────────────────────────

    private static void DefineColumns(TableDescriptor table)
    {
        table.ColumnsDefinition(cols =>
        {
            cols.RelativeColumn(3); // Date & Time
            cols.RelativeColumn(2); // Duration
            cols.RelativeColumn(2); // Type
            cols.RelativeColumn(2); // Status
            cols.RelativeColumn(3); // Provider
            cols.RelativeColumn(2); // Confirmation
        });
    }

    private static void AddHeaderRow(TableDescriptor table)
    {
        static IContainer CellStyle(IContainer container) =>
            container
                .Background(Colors.Blue.Medium)
                .Padding(6)
                .AlignCenter();

        static void Header(TableDescriptor t, string text) =>
            t.Cell().Element(CellStyle)
                .Text(text).FontSize(9).Bold().FontColor(Colors.White);

        Header(table, "Date & Time");
        Header(table, "Duration");
        Header(table, "Type");
        Header(table, "Status");
        Header(table, "Provider");
        Header(table, "Confirmation");
    }

    private static void AddDataRow(TableDescriptor table, AppointmentHistoryItem apt)
    {
        static IContainer CellStyle(IContainer container) =>
            container.BorderBottom(1).BorderColor(Colors.Grey.Lighten2).Padding(6);

        static void Cell(TableDescriptor t, string text) =>
            t.Cell().Element(CellStyle).Text(text).FontSize(9);

        Cell(table, apt.ScheduledAt.ToString("MMM d, yyyy h:mm tt"));
        Cell(table, $"{apt.DurationMinutes} min");
        Cell(table, apt.AppointmentType);
        Cell(table, apt.Status);
        Cell(table, apt.ProviderName ?? "—");
        Cell(table, apt.ConfirmationCode);
    }

    private static string BuildFilterSummary(AppointmentHistoryFilter filter)
    {
        var parts = new List<string>();

        if (filter.Status is not null)
            parts.Add($"Status: {filter.Status}");
        if (filter.DateFrom.HasValue)
            parts.Add($"From: {filter.DateFrom.Value:MMM d, yyyy}");
        if (filter.DateTo.HasValue)
            parts.Add($"To: {filter.DateTo.Value:MMM d, yyyy}");

        return parts.Count > 0
            ? string.Join("  |  ", parts)
            : "Showing all appointments";
    }
}
