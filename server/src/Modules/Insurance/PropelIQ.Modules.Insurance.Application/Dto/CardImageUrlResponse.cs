namespace PropelIQ.Modules.Insurance.Application.Dto;

/// <summary>
/// Response returned by <c>GET /api/v1/insurance/{patientId}/card-image/{side}</c>
/// (EP-005 US_038 AC-1).  The client uses <see cref="Url"/> to fetch the card image
/// directly from Cloudflare R2 without routing through the API.
/// </summary>
public sealed class CardImageUrlResponse
{
    /// <summary>Time-limited pre-signed R2 URL (5-minute expiry).</summary>
    public required string Url { get; init; }

    /// <summary>UTC timestamp when the pre-signed URL expires.</summary>
    public required DateTimeOffset ExpiresAt { get; init; }

    /// <summary><c>front</c> or <c>back</c>.</summary>
    public required string Side { get; init; }
}
