using Microsoft.Extensions.Logging;
using PropelIQ.Modules.SharedServices.Application.Compliance;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;

namespace PropelIQ.Modules.SharedServices.Infrastructure.Compliance;

/// <summary>
/// Renders a <see cref="ComplianceReportData"/> into a branded PDF using QuestPDF 2024.x (US_058, AC-2).
///
/// PDF structure:
/// <list type="bullet">
///   <item>Cover page — practice name, report type, period, generation date.</item>
///   <item>Executive summary — key metrics from <see cref="ReportMetrics"/>.</item>
///   <item>Access log summary — table grouped by actor/role and by resource type.</item>
///   <item>Event type counts — sorted table with counts per event type.</item>
///   <item>Anomalies — severity-labelled list.</item>
///   <item>Footer — page numbers and report ID.</item>
/// </list>
///
/// QuestPDF community licence is declared at startup in
/// <c>SharedServicesServiceRegistration</c> via <c>Settings.License = LicenseType.Community</c>.
/// </summary>
public sealed class ComplianceReportPdfRenderer
{
    private const string PracticeNamePlaceholder = "PropelIQ Health";

    private readonly ILogger<ComplianceReportPdfRenderer> _logger;

    public ComplianceReportPdfRenderer(ILogger<ComplianceReportPdfRenderer> logger)
    {
        _logger = logger;
    }

    /// <summary>
    /// Renders the report synchronously and returns the PDF as a byte array.
    /// QuestPDF is synchronous by design; this method is intentionally not async.
    /// </summary>
    public byte[] Render(ComplianceReportData data)
    {
        _logger.LogDebug(
            "Rendering PDF for report {ReportId} ({Type}).", data.ReportId, data.ReportType);

        return Document.Create(container =>
        {
            container.Page(page =>
            {
                page.Size(PageSizes.A4);
                page.Margin(40);
                page.DefaultTextStyle(x => x.FontSize(10).FontFamily(Fonts.Arial));

                // ── Header ────────────────────────────────────────────────────
                page.Header().Column(col =>
                {
                    col.Item().Row(row =>
                    {
                        row.RelativeItem().Text(PracticeNamePlaceholder)
                           .FontSize(18).SemiBold();
                        row.ConstantItem(200)
                           .AlignRight()
                           .Text($"Report ID: {data.ReportId}")
                           .FontSize(8).FontColor(Colors.Grey.Medium);
                    });
                    col.Item().PaddingBottom(4)
                       .LineHorizontal(1).LineColor(Colors.Grey.Lighten2);
                });

                // ── Content ───────────────────────────────────────────────────
                page.Content().Column(col =>
                {
                    // Cover / title section
                    col.Item().PaddingVertical(16).Column(title =>
                    {
                        title.Item().Text($"HIPAA Compliance Report — {data.ReportType}")
                             .FontSize(16).SemiBold();
                        title.Item().Text(
                            $"Period: {data.PeriodStartUtc:yyyy-MM-dd} – {data.PeriodEndUtc:yyyy-MM-dd}")
                             .FontSize(12);
                        title.Item().Text(
                            $"Generated: {data.GeneratedAtUtc:yyyy-MM-dd HH:mm:ss} UTC")
                             .FontSize(10).FontColor(Colors.Grey.Darken1);
                    });

                    col.Item().LineHorizontal(0.5f).LineColor(Colors.Grey.Lighten3);

                    // Executive summary
                    col.Item().PaddingTop(12).Column(summary =>
                    {
                        summary.Item().Text("Executive Summary").FontSize(13).SemiBold();
                        summary.Item().PaddingTop(4).Table(table =>
                        {
                            table.ColumnsDefinition(cols =>
                            {
                                cols.RelativeColumn(2);
                                cols.RelativeColumn(1);
                            });

                            table.Cell().Background(Colors.Blue.Lighten4).Padding(4)
                                 .Text("Metric").SemiBold();
                            table.Cell().Background(Colors.Blue.Lighten4).Padding(4)
                                 .Text("Value").SemiBold();

                            AddTableRow(table, "Total Audit Events",
                                data.KeyMetrics.TotalAuditEvents.ToString());
                            AddTableRow(table, "Unique Actors",
                                data.KeyMetrics.UniqueActors.ToString());
                            AddTableRow(table, "Failed Access Attempts",
                                data.KeyMetrics.FailedAccessAttempts.ToString());
                            AddTableRow(table, "Detected Anomalies",
                                data.KeyMetrics.AnomalyCount.ToString());
                        });
                    });

                    // Access log summary — by actor
                    col.Item().PaddingTop(16).Column(al =>
                    {
                        al.Item().Text("Access Log Summary — By Actor").FontSize(12).SemiBold();
                        al.Item().Text($"Total access events: {data.AccessSummary.TotalAccessEvents}")
                          .FontSize(9).FontColor(Colors.Grey.Darken1);
                        al.Item().PaddingTop(4).Table(table =>
                        {
                            table.ColumnsDefinition(cols =>
                            {
                                cols.RelativeColumn(3);
                                cols.RelativeColumn(2);
                                cols.RelativeColumn(1);
                            });

                            table.Cell().Background(Colors.Blue.Lighten4).Padding(4)
                                 .Text("Actor (UUID)").SemiBold();
                            table.Cell().Background(Colors.Blue.Lighten4).Padding(4)
                                 .Text("Role").SemiBold();
                            table.Cell().Background(Colors.Blue.Lighten4).Padding(4)
                                 .Text("Count").SemiBold();

                            foreach (var actor in data.AccessSummary.ByActor)
                            {
                                AddTableRow3(table, actor.ActorName,
                                    actor.Role.Length > 0 ? actor.Role : "—",
                                    actor.AccessCount.ToString());
                            }

                            if (data.AccessSummary.ByActor.Count == 0)
                            {
                                table.Cell().ColumnSpan(3).Padding(4)
                                     .Text("No access events in period.")
                                     .FontColor(Colors.Grey.Medium);
                            }
                        });
                    });

                    // Access log summary — by resource type
                    col.Item().PaddingTop(12).Column(br =>
                    {
                        br.Item().Text("Access Log Summary — By Resource").FontSize(12).SemiBold();
                        br.Item().PaddingTop(4).Table(table =>
                        {
                            table.ColumnsDefinition(cols =>
                            {
                                cols.RelativeColumn(3);
                                cols.RelativeColumn(1);
                            });

                            table.Cell().Background(Colors.Blue.Lighten4).Padding(4)
                                 .Text("Resource Type").SemiBold();
                            table.Cell().Background(Colors.Blue.Lighten4).Padding(4)
                                 .Text("Access Count").SemiBold();

                            foreach (var res in data.AccessSummary.ByResource)
                            {
                                AddTableRow(table, res.ResourceType, res.AccessCount.ToString());
                            }
                        });
                    });

                    // Audit event counts
                    col.Item().PaddingTop(16).Column(ev =>
                    {
                        ev.Item().Text("Audit Event Counts by Type").FontSize(12).SemiBold();
                        ev.Item().PaddingTop(4).Table(table =>
                        {
                            table.ColumnsDefinition(cols =>
                            {
                                cols.RelativeColumn(3);
                                cols.RelativeColumn(1);
                            });

                            table.Cell().Background(Colors.Blue.Lighten4).Padding(4)
                                 .Text("Event Type").SemiBold();
                            table.Cell().Background(Colors.Blue.Lighten4).Padding(4)
                                 .Text("Count").SemiBold();

                            foreach (var evt in data.EventCounts)
                            {
                                AddTableRow(table, evt.EventType, evt.Count.ToString());
                            }

                            if (data.EventCounts.Count == 0)
                            {
                                table.Cell().ColumnSpan(2).Padding(4)
                                     .Text("No audit events in period.")
                                     .FontColor(Colors.Grey.Medium);
                            }
                        });
                    });

                    // Anomalies
                    col.Item().PaddingTop(16).Column(an =>
                    {
                        an.Item().Text("Detected Anomalies").FontSize(12).SemiBold();

                        if (data.Anomalies.Count == 0)
                        {
                            an.Item().PaddingTop(4)
                              .Text("No anomalies detected in this period.")
                              .FontColor(Colors.Green.Darken2);
                        }
                        else
                        {
                            foreach (var anomaly in data.Anomalies)
                            {
                                var severityColor = anomaly.Severity switch
                                {
                                    "High"   => Colors.Red.Medium,
                                    "Medium" => Colors.Orange.Medium,
                                    _        => Colors.Yellow.Darken2,
                                };

                                an.Item().PaddingTop(4).Row(row =>
                                {
                                    row.ConstantItem(60)
                                       .Background(severityColor)
                                       .Padding(3)
                                       .AlignCenter()
                                       .Text(anomaly.Severity)
                                       .FontColor(Colors.White)
                                       .FontSize(8);

                                    row.RelativeItem().PaddingLeft(8).Column(c =>
                                    {
                                        c.Item().Text(anomaly.AnomalyType).SemiBold();
                                        c.Item().Text(anomaly.Description)
                                         .FontColor(Colors.Grey.Darken2);
                                    });
                                });
                            }
                        }
                    });
                });

                // ── Footer ────────────────────────────────────────────────────
                page.Footer().Row(row =>
                {
                    row.RelativeItem()
                       .Text("PropelIQ Compliance Report — Confidential")
                       .FontSize(8).FontColor(Colors.Grey.Medium);

                    row.ConstantItem(120).AlignRight().Text(x =>
                    {
                        x.Span("Page ").FontSize(8).FontColor(Colors.Grey.Medium);
                        x.CurrentPageNumber().FontSize(8).FontColor(Colors.Grey.Medium);
                        x.Span(" of ").FontSize(8).FontColor(Colors.Grey.Medium);
                        x.TotalPages().FontSize(8).FontColor(Colors.Grey.Medium);
                    });
                });
            });
        })
        .GeneratePdf();
    }

    // ── Helpers to reduce repetitive cell construction ────────────────────────

    private static void AddTableRow(TableDescriptor table, string col1, string col2)
    {
        table.Cell()
             .BorderBottom(0.5f).BorderColor(Colors.Grey.Lighten2)
             .Padding(4)
             .Text(col1);
        table.Cell()
             .BorderBottom(0.5f).BorderColor(Colors.Grey.Lighten2)
             .Padding(4)
             .Text(col2);
    }

    private static void AddTableRow3(
        TableDescriptor table, string col1, string col2, string col3)
    {
        table.Cell()
             .BorderBottom(0.5f).BorderColor(Colors.Grey.Lighten2)
             .Padding(4).Text(col1);
        table.Cell()
             .BorderBottom(0.5f).BorderColor(Colors.Grey.Lighten2)
             .Padding(4).Text(col2);
        table.Cell()
             .BorderBottom(0.5f).BorderColor(Colors.Grey.Lighten2)
             .Padding(4).Text(col3);
    }
}
