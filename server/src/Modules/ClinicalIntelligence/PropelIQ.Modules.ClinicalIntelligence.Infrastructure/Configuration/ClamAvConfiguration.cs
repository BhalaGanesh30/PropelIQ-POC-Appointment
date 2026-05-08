namespace PropelIQ.Modules.ClinicalIntelligence.Infrastructure.Configuration;

/// <summary>
/// Configuration options for the ClamAV daemon connection.
/// Bound from <c>appsettings.json</c> section <c>"ClamAv"</c>.
/// </summary>
public sealed class ClamAvConfiguration
{
    public string Host { get; set; } = "localhost";
    public int Port { get; set; } = 3310;
    public int TimeoutSeconds { get; set; } = 30;
}
