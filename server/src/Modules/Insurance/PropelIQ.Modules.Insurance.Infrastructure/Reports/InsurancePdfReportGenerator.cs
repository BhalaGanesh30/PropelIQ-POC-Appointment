using PropelIQ.Modules.Insurance.Application.Dto;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;

namespace PropelIQ.Modules.Insurance.Infrastructure.Reports;

/// <summary>
/// QuestPDF document builder for the insurance verification report
/// (EP-005 US_039 AC-3).
///
/// Generates an A4 PDF with:
/// - Title header and generation timestamp.
/// - Applied status filter label.
/// - Data table: Patient Name | Insurance Provider | Policy Number | Status | Validated Date.
/// - Status column colour-coded (green / amber / red).
/// - Alternating row background for readability.
/// - Page numbers in the footer.
///
/// QuestPDF Community licence is set globally in <c>InsuranceServiceRegistration</c>.
/// </summary>
internal sealed class InsurancePdfReportGenerator : IDocument
{
    private readonly IReadOnlyList<VerificationReportEntryDto> _entries;
    private readonly string? _statusFilter;
    private readonly DateTimeOffset _generatedAt;

    // ── Column widths (relative units — QuestPDF distributes proportionally) ──
    private const float ColPatient  = 3.0f;
    private const float ColProvider = 2.5f;
    private const float ColPolicy   = 2.0f;
    private const float ColStatus   = 2.0f;
    private const float ColDate     = 1.5f;

    // ── Brand colours ──────────────────────────────────────────────────────────
    private static readonly string ColourPrimary = "#1976D2";   // header bg
    private static readonly string ColourHeaderText = "#FFFFFF";
    private static readonly string ColourEvenRow = "#F5F9FF";   // alternating rows
    private static readonly string ColourBorderLight = "#E0E0E0";

    // ── Status badge colours (UXR-404) ─────────────────────────────────────────
    private static readonly string ColourSuccess = "#1B5E20";
    private static readonly string ColourWarning = "#E65100";
    private static readonly string ColourError   = "#B71C1C";
    private static readonly string ColourInfo    = "#0D47A1";

    public InsurancePdfReportGenerator(
        IReadOnlyList<VerificationReportEntryDto> entries,
        string? statusFilter,
        DateTimeOffset generatedAt)
    {
        _entries = entries;
        _statusFilter = statusFilter;
        _generatedAt = generatedAt;
    }

    public DocumentMetadata GetMetadata()
    {
        var meta = DocumentMetadata.Default;
        meta.Title        = "Insurance Verification Report";
        meta.Author       = "PropelIQ";
        meta.CreationDate = _generatedAt.UtcDateTime;
        return meta;
    }

    public DocumentSettings GetSettings() => DocumentSettings.Default;

    public void Compose(IDocumentContainer container)
    {
        container.Page(page =>
        {
            page.Size(PageSizes.A4);
            page.Margin(2, Unit.Centimetre);
            page.DefaultTextStyle(ts => ts.FontSize(9).FontFamily("Helvetica"));

            page.Header().Element(ComposeHeader);
            page.Content().Element(ComposeContent);
            page.Footer().Element(ComposeFooter);
        });
    }

    // ── Header ─────────────────────────────────────────────────────────────────

    private void ComposeHeader(IContainer container)
    {
        container.Column(col =>
        {
            col.Item()
               .Background(ColourPrimary)
               .Padding(12)
               .Row(row =>
               {
                   row.RelativeItem().Text("Insurance Verification Report")
                      .FontSize(16).Bold().FontColor(ColourHeaderText);
                   row.AutoItem().AlignRight()
                      .Text($"PropelIQ")
                      .FontSize(11).FontColor(ColourHeaderText);
               });

            col.Item().PaddingTop(4).Row(row =>
            {
                row.AutoItem()
                   .Text($"Generated: {_generatedAt:dd MMM yyyy HH:mm} UTC")
                   .FontSize(8).Italic().FontColor("#616161");

                if (_statusFilter is not null)
                {
                    row.AutoItem().PaddingLeft(16)
                       .Text($"Filter: {_statusFilter}")
                       .FontSize(8).Italic().FontColor("#616161");
                }

                row.RelativeItem().AlignRight()
                   .Text($"Total records: {_entries.Count}")
                   .FontSize(8).FontColor("#616161");
            });

            col.Item().PaddingTop(4).LineHorizontal(1).LineColor(Colors.Grey.Lighten2);
        });
    }

    // ── Content (data table) ───────────────────────────────────────────────────

    private void ComposeContent(IContainer container)
    {
        container.PaddingTop(8).Table(table =>
        {
            // ── Column definitions ────────────────────────────────────────────
            table.ColumnsDefinition(cols =>
            {
                cols.RelativeColumn(ColPatient);
                cols.RelativeColumn(ColProvider);
                cols.RelativeColumn(ColPolicy);
                cols.RelativeColumn(ColStatus);
                cols.RelativeColumn(ColDate);
            });

            // ── Header row ────────────────────────────────────────────────────
            table.Header(header =>
            {
                void HeaderCell(string text) =>
                    header.Cell()
                          .Background(ColourPrimary)
                          .Padding(6)
                          .Text(text)
                          .FontColor(ColourHeaderText)
                          .Bold()
                          .FontSize(9);

                HeaderCell("Patient Name");
                HeaderCell("Insurance Provider");
                HeaderCell("Policy Number");
                HeaderCell("Validation Status");
                HeaderCell("Validated Date");
            });

            // ── Data rows ─────────────────────────────────────────────────────
            for (var i = 0; i < _entries.Count; i++)
            {
                var entry = _entries[i];
                var bg = i % 2 == 0 ? "#FFFFFF" : ColourEvenRow;

                void DataCell(IContainer c, string text) =>
                    c.Background(bg)
                     .BorderBottom(1).BorderColor(ColourBorderLight)
                     .Padding(5)
                     .Text(text)
                     .FontSize(8.5f);

                table.Cell().Element(c => DataCell(c, entry.PatientName));
                table.Cell().Element(c => DataCell(c, entry.ProviderName));
                table.Cell().Element(c => DataCell(c, entry.PolicyNumber));

                // Status cell — colour-coded (UXR-404).
                table.Cell().Background(bg)
                     .BorderBottom(1).BorderColor(ColourBorderLight)
                     .Padding(5)
                     .Text(entry.ValidationStatus)
                     .FontSize(8.5f)
                     .FontColor(StatusColour(entry.ValidationStatus));

                table.Cell().Element(c => DataCell(c, entry.ValidatedAt.ToString("dd MMM yyyy")));
            }

            // ── Empty state ───────────────────────────────────────────────────
            if (_entries.Count == 0)
            {
                table.Cell().ColumnSpan(5)
                     .Padding(16)
                     .AlignCenter()
                     .Text("No insurance records match the selected filter.")
                     .Italic().FontColor("#757575");
            }
        });
    }

    // ── Footer ─────────────────────────────────────────────────────────────────

    private static void ComposeFooter(IContainer container)
    {
        container.PaddingTop(4)
                 .LineHorizontal(0.5f).LineColor(Colors.Grey.Lighten2);

        container.Row(row =>
        {
            row.AutoItem()
               .Text("PropelIQ — CONFIDENTIAL — For authorised staff use only")
               .FontSize(7).Italic().FontColor("#9E9E9E");

            row.RelativeItem().AlignRight()
               .Text(text =>
               {
                   text.DefaultTextStyle(ts => ts.FontSize(7).FontColor("#9E9E9E"));
                   text.Span("Page ");
                   text.CurrentPageNumber();
                   text.Span(" of ");
                   text.TotalPages();
               });
        });
    }

    // ── Helpers ────────────────────────────────────────────────────────────────

    private static string StatusColour(string status) =>
        status switch
        {
            "SoftValidated"     => ColourSuccess,
            "ValidationFailed"  => ColourError,
            "ValidationPending" => ColourWarning,
            "Warning"           => ColourWarning,
            _                   => ColourInfo,
        };
}
