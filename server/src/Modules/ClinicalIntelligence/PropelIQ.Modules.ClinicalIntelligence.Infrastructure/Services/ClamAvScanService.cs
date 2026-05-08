using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using nClam;
using PropelIQ.Modules.ClinicalIntelligence.Application.Abstractions;
using PropelIQ.Modules.ClinicalIntelligence.Application.Enums;
using PropelIQ.Modules.ClinicalIntelligence.Infrastructure.Configuration;
using System.Net.Sockets;

namespace PropelIQ.Modules.ClinicalIntelligence.Infrastructure.Services;

/// <summary>
/// ClamAV-based malware scan service using the nClam .NET client library.
///
/// On <see cref="SocketException"/> or any other network failure the method returns
/// <see cref="ScanResult.ScannerUnavailable"/> so the caller can quarantine the file
/// and schedule a retry (Edge Case 1).  On confirmed threat, a security event is
/// logged at <c>Warning</c> level for audit purposes (AC-3).
/// </summary>
public sealed class ClamAvScanService : IMalwareScanService
{
    private readonly ClamAvConfiguration _config;
    private readonly ILogger<ClamAvScanService> _logger;

    public ClamAvScanService(
        IOptions<ClamAvConfiguration> config,
        ILogger<ClamAvScanService> logger)
    {
        _config = config.Value;
        _logger = logger;
    }

    public async Task<ScanResult> ScanAsync(Stream fileStream, CancellationToken ct = default)
    {
        try
        {
            var client = new ClamClient(_config.Host, _config.Port)
            {
                MaxStreamSize = 100 * 1024 * 1024  // 100 MB upper limit for stream size
            };

            fileStream.Seek(0, SeekOrigin.Begin);
            using var cts = CancellationTokenSource.CreateLinkedTokenSource(ct);
            cts.CancelAfter(TimeSpan.FromSeconds(_config.TimeoutSeconds));

            var scanResult = await client.SendAndScanFileAsync(fileStream, cts.Token);

            return scanResult.Result switch
            {
                ClamScanResults.Clean => ScanResult.Clean,
                ClamScanResults.VirusDetected => LogAndReturnThreat(scanResult),
                _ => ScanResult.ScannerUnavailable
            };
        }
        catch (SocketException ex)
        {
            _logger.LogWarning(ex, "ClamAV daemon unreachable at {Host}:{Port}. File will be quarantined.",
                _config.Host, _config.Port);
            return ScanResult.ScannerUnavailable;
        }
        catch (OperationCanceledException) when (!ct.IsCancellationRequested)
        {
            _logger.LogWarning("ClamAV scan timed out after {Timeout}s. File will be quarantined.",
                _config.TimeoutSeconds);
            return ScanResult.ScannerUnavailable;
        }
    }

    private ScanResult LogAndReturnThreat(ClamScanResult scanResult)
    {
        _logger.LogWarning(
            "SECURITY EVENT: Malware detected. Threat={Threat} InfectedFiles={Count}",
            scanResult.InfectedFiles?.FirstOrDefault()?.VirusName ?? "unknown",
            scanResult.InfectedFiles?.Count ?? 0);

        return ScanResult.ThreatDetected;
    }
}
