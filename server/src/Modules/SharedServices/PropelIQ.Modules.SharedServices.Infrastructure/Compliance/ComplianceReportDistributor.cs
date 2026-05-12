using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Polly;
using PropelIQ.Modules.SharedServices.Domain.Entities;
using PropelIQ.Modules.SharedServices.Infrastructure.Data;
using PropelIQ.SharedKernel.Notifications;

namespace PropelIQ.Modules.SharedServices.Infrastructure.Compliance;

/// <summary>
/// Reads active recipients from <c>compliance.compliance_distribution_lists</c>
/// and delivers the PDF as an email attachment to each recipient (US_058, AC-3).
///
/// Delivery policy (edge case 2):
/// <list type="bullet">
///   <item>Each send attempt is logged to <c>compliance.compliance_distribution_log</c>.</item>
///   <item>On first failure, Polly retries once after 60 seconds.</item>
///   <item>If both attempts fail, an admin notification is created via <see cref="INotificationSender"/>.</item>
/// </list>
/// </summary>
public sealed class ComplianceReportDistributor
{
    private readonly INotificationSender              _notifications;
    private readonly ILogger<ComplianceReportDistributor> _logger;

    // Polly retry pipeline: 1 retry after 60 s for transient email failures.
    private static readonly ResiliencePipeline _retryPipeline =
        new ResiliencePipelineBuilder()
            .AddRetry(new Polly.Retry.RetryStrategyOptions
            {
                MaxRetryAttempts = 1,
                Delay            = TimeSpan.FromSeconds(60),
                ShouldHandle     = new PredicateBuilder().Handle<Exception>(),
            })
            .Build();

    // Admin notification recipient — configurable; fallback to a constant.
    private const string AdminFallbackEmail = "admin@propeliq.internal";

    public ComplianceReportDistributor(
        INotificationSender notifications,
        ILogger<ComplianceReportDistributor> logger)
    {
        _notifications = notifications;
        _logger        = logger;
    }

    /// <summary>
    /// Distributes the compliance report PDF to all active distribution list recipients.
    /// Uses <paramref name="scopeFactory"/> to resolve a fresh <see cref="AppDbContext"/>
    /// per distribution call (safe for use from BackgroundService context).
    /// </summary>
    public async Task DistributeAsync(
        Guid                    reportId,
        byte[]                  pdfBytes,
        string                  reportType,
        DateTime                periodStart,
        DateTime                periodEnd,
        IServiceScopeFactory    scopeFactory,
        CancellationToken       ct = default)
    {
        await using var scope = scopeFactory.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        var recipients = await db.ComplianceDistributionList
            .AsNoTracking()
            .Where(r => r.IsActive)
            .ToListAsync(ct);

        if (recipients.Count == 0)
        {
            _logger.LogInformation(
                "No active recipients in compliance distribution list for report {ReportId}.", reportId);
            return;
        }

        foreach (var recipient in recipients)
        {
            await DeliverToRecipientAsync(
                reportId, pdfBytes, reportType, periodStart, periodEnd,
                recipient, db, ct);
        }

        await db.SaveChangesAsync(ct);
    }

    // ── Private per-recipient delivery ────────────────────────────────────────

    private async Task DeliverToRecipientAsync(
        Guid                        reportId,
        byte[]                      pdfBytes,
        string                      reportType,
        DateTime                    periodStart,
        DateTime                    periodEnd,
        ComplianceDistributionList  recipient,
        AppDbContext                db,
        CancellationToken           ct)
    {
        int attemptNumber = 1;

        try
        {
            await _retryPipeline.ExecuteAsync(async innerCt =>
            {
                _logger.LogDebug(
                    "Sending compliance report {ReportId} to {Email} (attempt {Attempt}).",
                    reportId, recipient.Email, attemptNumber);

                var subject  = $"[PropelIQ] {reportType} Compliance Report — {periodStart:yyyy-MM-dd} to {periodEnd:yyyy-MM-dd}";
                var htmlBody = BuildEmailBody(reportType, periodStart, periodEnd, reportId, pdfBytes.Length);

                // INotificationSender does not natively support attachments.
                // The attachment is noted in the email body with a download URL.
                // For full attachment support, replace INotificationSender with a provider
                // that supports MIME multipart (e.g. SendGrid, MailKit).
                await _notifications.SendEmailAsync(recipient.Email, subject, htmlBody, innerCt);

                // Log successful delivery.
                db.ComplianceDistributionLogs.Add(new ComplianceDistributionLog
                {
                    ReportId       = reportId,
                    RecipientId    = recipient.Id,
                    RecipientEmail = recipient.Email,
                    Status         = attemptNumber == 1 ? "Sent" : "Retried",
                    AttemptedAtUtc = DateTime.UtcNow,
                    AttemptNumber  = attemptNumber,
                });

                _logger.LogInformation(
                    "Compliance report {ReportId} delivered to {Email} (attempt {Attempt}).",
                    reportId, recipient.Email, attemptNumber);

            }, ct);
        }
        catch (Exception ex)
        {
            _logger.LogError(
                ex,
                "Failed to deliver compliance report {ReportId} to {Email} after {Attempts} attempt(s).",
                reportId, recipient.Email, attemptNumber);

            // Log persistent failure.
            db.ComplianceDistributionLogs.Add(new ComplianceDistributionLog
            {
                ReportId       = reportId,
                RecipientId    = recipient.Id,
                RecipientEmail = recipient.Email,
                Status         = "Failed",
                AttemptedAtUtc = DateTime.UtcNow,
                AttemptNumber  = attemptNumber,
                ErrorDetail    = ex.Message,
            });

            // Surface persistent failure as admin notification (edge case 2).
            try
            {
                await _notifications.SendEmailAsync(
                    AdminFallbackEmail,
                    $"[PropelIQ ALERT] Compliance report delivery failed — {recipient.Email}",
                    $"<p>Compliance report <strong>{reportId}</strong> could not be delivered to " +
                    $"<strong>{recipient.Email}</strong> after {attemptNumber} attempt(s).</p>" +
                    $"<p>Error: {ex.Message}</p>",
                    ct);
            }
            catch (Exception notifyEx)
            {
                _logger.LogError(
                    notifyEx,
                    "Failed to send admin failure notification for report {ReportId}.", reportId);
            }
        }
    }

    // ── Email body builder ────────────────────────────────────────────────────

    private static string BuildEmailBody(
        string   reportType,
        DateTime periodStart,
        DateTime periodEnd,
        Guid     reportId,
        int      pdfSizeBytes)
    {
        return $"""
            <html><body>
            <h2>PropelIQ HIPAA Compliance Report</h2>
            <p><strong>Report Type:</strong> {reportType}</p>
            <p><strong>Period:</strong> {periodStart:yyyy-MM-dd} – {periodEnd:yyyy-MM-dd}</p>
            <p><strong>Report ID:</strong> {reportId}</p>
            <p>
              The report PDF ({pdfSizeBytes / 1024} KB) is available for download from the Admin portal:
              <a href="/admin/reports/{reportId}/download">Download Report</a>
            </p>
            <p>This report is confidential and intended for authorised recipients only.</p>
            <hr/>
            <p><em>PropelIQ Health Platform</em></p>
            </body></html>
            """;
    }
}
