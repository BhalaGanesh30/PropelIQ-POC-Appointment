using Microsoft.Extensions.Logging;
using PropelIQ.Modules.SharedServices.Application.Kpi;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;

namespace PropelIQ.Modules.SharedServices.Infrastructure.Kpi;

/// <summary>
/// Renders a <see cref="KpiSummaryResponse"/> into a PDF or PNG using QuestPDF (US_060, AC-3, AC-4).
///
/// <para>
/// QuestPDF community licence is declared once at startup in
/// <c>SharedServicesServiceRegistration</c> via <c>Settings.License = LicenseType.Community</c>.
/// </para>
///
/// Registered as a singleton in <c>SharedServicesServiceRegistration</c> — this class is stateless.
/// </summary>
public sealed class KpiReportPdfRenderer
{
    private const string PracticeNamePlaceholder = "PropelIQ Health";

    private readonly ILogger<KpiReportPdfRenderer> _logger;

    public KpiReportPdfRenderer(ILogger<KpiReportPdfRenderer> logger)
    {
        _logger = logger;
    }

    /// <summary>
    /// Renders a PDF KPI report for <paramref name="range"/> and returns the result as a
    /// <see cref="KpiExportResult"/>. QuestPDF is synchronous by design; this method is not async.
    /// </summary>
    public KpiExportResult RenderPdf(KpiSummaryResponse summary, DateRange range)
    {
        _logger.LogDebug(
            "Rendering KPI PDF report for range {From} to {To}.", range.From, range.To);

        var document = Document.Create(container =>
        {
            container.Page(page =>
            {
                page.Size(PageSizes.A4.Landscape());
                page.Margin(40);
                page.DefaultTextStyle(x => x.FontSize(10).FontFamily(Fonts.Arial));

                // ── Header ────────────────────────────────────────────────────
                page.Header().Column(col =>
                {
                    col.Item().Row(row =>
                    {
                        row.RelativeItem()
                            .Text(PracticeNamePlaceholder)
                            .FontSize(18).SemiBold();
                        row.ConstantItem(220)
                            .AlignRight()
                            .Text($"KPI Report — {range.From:yyyy-MM-dd} to {range.To:yyyy-MM-dd}")
                            .FontSize(9).FontColor(Colors.Grey.Medium);
                    });
                    col.Item().PaddingBottom(4)
                        .LineHorizontal(1).LineColor(Colors.Grey.Lighten2);
                });

                // ── Content ───────────────────────────────────────────────────
                page.Content().Column(col =>
                {
                    // Staleness warning (edge case 1)
                    if (summary.IsStale)
                    {
                        col.Item().PaddingBottom(8)
                            .Background(Colors.Orange.Lighten4)
                            .Padding(6)
                            .Text(
                                $"Data may be stale — last computed {summary.ComputedAtUtc:yyyy-MM-dd HH:mm} UTC")
                            .FontColor(Colors.Orange.Darken2).FontSize(9);
                    }

                    // Metric cards table
                    col.Item().PaddingTop(12).Column(section =>
                    {
                        section.Item().Text("Key Performance Indicators")
                            .FontSize(14).SemiBold();

                        section.Item().PaddingBottom(8);

                        section.Item().Table(table =>
                        {
                            table.ColumnsDefinition(cols =>
                            {
                                cols.RelativeColumn(3); // Metric name
                                cols.RelativeColumn(2); // Value
                                cols.RelativeColumn(2); // Previous period
                                cols.RelativeColumn(2); // Change
                            });

                            // Header row
                            foreach (var header in new[] { "Metric", "Current Period", "Previous Period", "Change %" })
                            {
                                table.Cell()
                                    .Background(Colors.Blue.Lighten4)
                                    .Padding(6)
                                    .Text(header).SemiBold().FontSize(10);
                            }

                            // Data rows
                            foreach (var card in summary.Cards)
                            {
                                table.Cell().Padding(6).Text(FormatMetricName(card.Metric));
                                table.Cell().Padding(6).Text(FormatValue(card.Metric, card.Value)).Bold();
                                table.Cell().Padding(6).Text(
                                    card.PreviousPeriodValue.HasValue
                                        ? FormatValue(card.Metric, card.PreviousPeriodValue.Value)
                                        : "—");
                                var change = card.ChangePercent;
                                table.Cell().Padding(6).Text(
                                    change.HasValue ? $"{change.Value:+0.0;-0.0;0.0}%" : "—")
                                    .FontColor(
                                        change is null     ? Colors.Black :
                                        change.Value >= 0  ? Colors.Green.Darken2 :
                                                             Colors.Red.Darken2);
                            }
                        });
                    });

                    // Empty-period annotation (edge case 2)
                    if (summary.Cards.All(c => c.Value == 0))
                    {
                        col.Item().PaddingTop(16)
                            .Text("No data for the selected period.")
                            .FontColor(Colors.Grey.Medium).Italic();
                    }
                });

                // ── Footer ────────────────────────────────────────────────────
                page.Footer().AlignCenter().Text(x =>
                {
                    x.Span("Generated ");
                    x.Span(DateTime.UtcNow.ToString("yyyy-MM-dd HH:mm") + " UTC");
                    x.Span(" — PropelIQ KPI Dashboard");
                });
            });
        });

        var bytes = document.GeneratePdf();
        return new KpiExportResult(
            bytes,
            "application/pdf",
            $"kpi-report-{range.From:yyyyMMdd}-{range.To:yyyyMMdd}.pdf");
    }

    /// <summary>
    /// Renders a PNG image of the first page of the KPI report and returns the result.
    /// Uses QuestPDF <c>GenerateImages()</c> to produce one PNG per page; returns the first page.
    /// </summary>
    public KpiExportResult RenderPng(KpiSummaryResponse summary, DateRange range)
    {
        _logger.LogDebug(
            "Rendering KPI PNG chart for range {From} to {To}.", range.From, range.To);

        var document = Document.Create(container =>
        {
            container.Page(page =>
            {
                page.Size(PageSizes.A4.Landscape());
                page.Margin(40);
                page.DefaultTextStyle(x => x.FontSize(10).FontFamily(Fonts.Arial));

                page.Header().Text($"KPI Summary — {range.From:yyyy-MM-dd} to {range.To:yyyy-MM-dd}")
                    .FontSize(14).SemiBold();

                page.Content().Column(col =>
                {
                    col.Item().PaddingTop(8).Table(table =>
                    {
                        table.ColumnsDefinition(cols =>
                        {
                            cols.RelativeColumn(3);
                            cols.RelativeColumn(2);
                        });

                        foreach (var header in new[] { "Metric", "Value" })
                        {
                            table.Cell()
                                .Background(Colors.Blue.Lighten4)
                                .Padding(5).Text(header).SemiBold();
                        }

                        foreach (var card in summary.Cards)
                        {
                            table.Cell().Padding(5).Text(FormatMetricName(card.Metric));
                            table.Cell().Padding(5).Text(FormatValue(card.Metric, card.Value)).Bold();
                        }
                    });
                });
            });
        });

        var images = document.GenerateImages();
        var firstPage = images.First();
        return new KpiExportResult(
            firstPage,
            "image/png",
            $"kpi-chart-{range.From:yyyyMMdd}-{range.To:yyyyMMdd}.png");
    }

    // ── Helpers ───────────────────────────────────────────────────────────────

    private static string FormatMetricName(KpiMetricType metric) => metric switch
    {
        KpiMetricType.NoShowRate            => "No-Show Rate",
        KpiMetricType.AppointmentUtilization => "Appointment Utilization",
        KpiMetricType.AverageWaitTime       => "Average Wait Time",
        KpiMetricType.BookingVolume         => "Booking Volume",
        _                                   => metric.ToString(),
    };

    private static string FormatValue(KpiMetricType metric, decimal value) => metric switch
    {
        KpiMetricType.NoShowRate            => $"{value:F1}%",
        KpiMetricType.AppointmentUtilization => $"{value:F1}%",
        KpiMetricType.AverageWaitTime       => $"{value:F0} min",
        KpiMetricType.BookingVolume         => $"{(int)value:N0}",
        _                                   => $"{value:F1}",
    };
}
