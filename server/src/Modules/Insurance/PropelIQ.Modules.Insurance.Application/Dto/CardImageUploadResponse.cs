namespace PropelIQ.Modules.Insurance.Application.Dto;

/// <summary>
/// Response returned by <c>POST /api/v1/insurance/{patientId}/card-image</c>
/// (EP-005 US_038 AC-1).
/// </summary>
public sealed class CardImageUploadResponse
{
    /// <summary>R2 object key under which the image was stored.</summary>
    public required string ObjectKey { get; init; }

    /// <summary><c>front</c> or <c>back</c>.</summary>
    public required string Side { get; init; }

    /// <summary>UUID of the insurance profile the image is attached to.</summary>
    public required Guid ProfileId { get; init; }
}
