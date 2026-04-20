namespace PropelIQ.Modules.SharedServices.Domain.Entities;

public sealed class AuditDetails
{
    public string? IpAddress { get; set; }
    public string? UserAgent { get; set; }
    public string? ChangeDescription { get; set; }
    public Dictionary<string, string> Metadata { get; set; } = new();
}
