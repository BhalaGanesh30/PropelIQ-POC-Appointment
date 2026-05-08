using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using PropelIQ.Modules.Insurance.Application.Abstractions;
using PropelIQ.Modules.Insurance.Application.Dto;
using PropelIQ.Modules.Insurance.Infrastructure.Validation;
using PropelIQ.Modules.SharedServices.Infrastructure.Data;

namespace PropelIQ.Api.Controllers;

/// <summary>
/// Insurance Soft Validation API (EP-005 US_037, US_038).
///
/// POST /api/v1/insurance/validate — performs non-blocking soft validation of insurance
/// details against format rules and a provider reference database within 500ms
/// (AC-1, NFR-002).  Returns a categorised <see cref="InsuranceValidateResponse"/>
/// with advisory warnings.  A ValidationPending, Warning, or ValidationFailed result
/// NEVER blocks the booking (AC-2).
///
/// POST /api/v1/insurance — persists the insurance profile with the resolved validation
/// status for staff review (AC-3, AC-4).  Sensitive fields are encrypted at rest (US_038 AC-1).
///
/// GET /api/v1/insurance/{patientId} — retrieves decrypted profiles for a patient (US_038 AC-2).
/// Patient callers may only access their own records; Staff / Admin may access any.
/// </summary>
[Authorize(Roles = "Patient,Staff")]
[ApiController]
[Produces("application/json")]
public sealed class InsuranceController : BaseApiController
{
    private readonly IInsuranceValidationService _validationService;
    private readonly IInsuranceProfileService _profileService;
    private readonly ICardImageStorageService _cardImageStorage;
    private readonly AppDbContext _db;

    public InsuranceController(
        IInsuranceValidationService validationService,
        IInsuranceProfileService profileService,
        ICardImageStorageService cardImageStorage,
        AppDbContext db)
    {
        _validationService = validationService;
        _profileService = profileService;
        _cardImageStorage = cardImageStorage;
        _db = db;
    }

    /// <summary>
    /// Soft-validates insurance details against format rules and the provider
    /// reference database.  Writes an audit record to <c>insurance_validation_results</c>
    /// regardless of outcome.  Never blocks the booking.
    /// </summary>
    /// <param name="request">Insurance details to validate.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>Categorised validation response with advisory warnings.</returns>
    /// <response code="200">Validation completed; status indicates outcome.</response>
    /// <response code="400">Request model validation failed.</response>
    /// <response code="401">JWT bearer token missing or invalid.</response>
    /// <response code="403">Caller does not have Patient or Staff role.</response>
    [HttpPost("api/v1/insurance/validate")]
    [ProducesResponseType(typeof(InsuranceValidateResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> Validate(
        [FromBody] InsuranceValidateRequest request,
        CancellationToken ct)
    {
        var response = await _validationService.ValidateAsync(request, ct);
        return Ok(response);
    }

    /// <summary>
    /// Persists an insurance profile for the specified patient with the validation
    /// status resolved by the preceding validate call.
    /// </summary>
    /// <param name="request">Profile data including resolved validation status.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>Summary of the persisted profile.</returns>
    /// <response code="201">Profile created or updated successfully.</response>
    /// <response code="400">Request model validation failed.</response>
    /// <response code="401">JWT bearer token missing or invalid.</response>
    /// <response code="403">Caller does not have Patient or Staff role.</response>
    [HttpPost("api/v1/insurance")]
    [ProducesResponseType(typeof(InsuranceSaveResponse), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> Save(
        [FromBody] InsuranceSaveRequest request,
        CancellationToken ct)
    {
        var response = await _profileService.SaveAsync(request, ct);
        return CreatedAtAction(nameof(Save), new { profileId = response.ProfileId }, response);
    }

    /// <summary>
    /// Retrieves decrypted insurance profiles for the specified patient (US_038 AC-2, AC-4).
    ///
    /// Authorization rules:
    ///   - Patients with role <c>Patient</c> may only retrieve their own records.
    ///     The caller's JWT <c>sub</c> claim is resolved to a Patient row via
    ///     <c>Patients.UserId</c>; a 403 is returned when the resolved PatientId does
    ///     not match the route parameter.
    ///   - Staff and Admin roles may retrieve records for any patientId.
    /// </summary>
    /// <param name="patientId">UUID of the patient whose insurance profiles to retrieve.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>List of decrypted insurance profile DTOs (may be empty).</returns>
    /// <response code="200">Profiles returned (empty list if none on file).</response>
    /// <response code="401">JWT bearer token missing or invalid.</response>
    /// <response code="403">Patient caller attempted to access another patient's records.</response>
    /// <response code="404">Patient not found.</response>
    [HttpGet("api/v1/insurance/{patientId:guid}")]
    [Authorize]
    [ProducesResponseType(typeof(IReadOnlyList<InsuranceProfileDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetByPatientId(
        Guid patientId,
        CancellationToken ct)
    {
        var ownershipResult = await VerifyOwnershipAsync(patientId, ct);
        if (ownershipResult is not null)
            return ownershipResult;

        var profiles = await _profileService.GetByPatientIdAsync(patientId, ct);
        return Ok(profiles);
    }

    /// <summary>
    /// Uploads an insurance card image for the specified patient profile
    /// (EP-005 US_038 AC-1, AC-3, AC-4).
    ///
    /// The file is validated for type (JPEG, PNG, PDF) and size (max 10 MB) using
    /// magic-byte detection before being streamed to Cloudflare R2 with SSE-S3
    /// server-side encryption.
    ///
    /// When <paramref name="file"/> is null or empty the call is treated as a no-op
    /// and <c>200 OK</c> is returned — card images are optional (Edge Case 2).
    ///
    /// Ownership rule: Patients may only upload images for their own profiles;
    /// Staff and Admin may upload for any patient (AC-4).
    /// </summary>
    /// <param name="patientId">UUID of the patient who owns the profile.</param>
    /// <param name="profileId">UUID of the specific insurance profile to attach the image to.</param>
    /// <param name="side"><c>front</c> or <c>back</c>.</param>
    /// <param name="file">Card image file (JPG/PNG/PDF, max 10 MB).  Optional.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <response code="201">File uploaded and key persisted.</response>
    /// <response code="200">No file supplied; no-op (Edge Case 2).</response>
    /// <response code="400">File type or size validation failed.</response>
    /// <response code="401">JWT missing or invalid.</response>
    /// <response code="403">Caller attempted to upload to another patient's profile.</response>
    /// <response code="404">Patient or profile not found.</response>
    [HttpPost("api/v1/insurance/{patientId:guid}/card-image")]
    [Authorize]
    [Consumes("multipart/form-data")]
    [ProducesResponseType(typeof(CardImageUploadResponse), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> UploadCardImage(
        Guid patientId,
        [FromQuery] Guid profileId,
        [FromQuery] string side,
        IFormFile? file,
        CancellationToken ct)
    {
        // Normalise side.
        side = side?.ToLowerInvariant() ?? "front";
        if (side is not "front" and not "back")
            return BadRequest(new { Error = "side must be 'front' or 'back'." });

        // Ownership check (same pattern as GetByPatientId — OWASP A01).
        var ownershipResult = await VerifyOwnershipAsync(patientId, ct);
        if (ownershipResult is not null)
            return ownershipResult;

        // Verify the profile exists and belongs to this patient.
        var profileExists = await _db.InsuranceProfiles
            .AsNoTracking()
            .AnyAsync(p => p.Id == profileId && p.PatientId == patientId, ct);

        if (!profileExists)
            return NotFound(new { Error = "Insurance profile not found for this patient." });

        // Edge Case 2: null / empty file is allowed — return 200 no-op.
        if (file is null || file.Length == 0)
            return Ok(new { Message = "No file provided; card image not updated." });

        // Guard against DoS before copying to memory.
        if (file.Length > CardImageValidator.MaxFileSizeBytes)
            return BadRequest(new
            {
                Error = $"File exceeds the maximum allowed size of " +
                        $"{CardImageValidator.MaxFileSizeBytes / (1024 * 1024)} MB.",
            });

        // Copy to MemoryStream to allow seeking (magic-byte check then upload).
        await using var memStream = new MemoryStream((int)file.Length);
        await file.CopyToAsync(memStream, ct);

        // Validate magic bytes (AC-3 — do NOT rely on extension or Content-Type).
        var header = new byte[8];
        memStream.Position = 0;
        _ = await memStream.ReadAsync(header.AsMemory(0, header.Length), ct);
        var validation = CardImageValidator.Validate(file.Length, header);

        if (!validation.IsValid)
            return BadRequest(new { Error = validation.Error });

        // Upload to R2 with SSE-S3 encryption.
        memStream.Position = 0;
        var objectKey = await _cardImageStorage.UploadAsync(
            patientId, profileId, side,
            memStream, file.ContentType, validation.DetectedExtension!,
            ct);

        // Persist the R2 key to the profile row.
        await _profileService.UpdateCardImageKeyAsync(profileId, side, objectKey, ct);

        var response = new CardImageUploadResponse
        {
            ObjectKey = objectKey,
            Side = side,
            ProfileId = profileId,
        };

        return CreatedAtAction(nameof(GetCardImageUrl),
            new { patientId, side, profileId },
            response);
    }

    /// <summary>
    /// Returns a time-limited pre-signed Cloudflare R2 URL for a card image
    /// (EP-005 US_038 AC-1, AC-4).  URL expires in 5 minutes.
    ///
    /// Ownership rule: Patients may only retrieve images for their own profiles;
    /// Staff and Admin may retrieve for any patient (AC-4).
    /// </summary>
    /// <param name="patientId">UUID of the patient who owns the profile.</param>
    /// <param name="side"><c>front</c> or <c>back</c>.</param>
    /// <param name="profileId">UUID of the specific insurance profile.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <response code="200">Pre-signed URL returned.</response>
    /// <response code="401">JWT missing or invalid.</response>
    /// <response code="403">Caller attempted to access another patient's image.</response>
    /// <response code="404">Patient, profile, or card image not found.</response>
    [HttpGet("api/v1/insurance/{patientId:guid}/card-image/{side}")]
    [Authorize]
    [ProducesResponseType(typeof(CardImageUrlResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetCardImageUrl(
        Guid patientId,
        string side,
        [FromQuery] Guid profileId,
        CancellationToken ct)
    {
        side = side?.ToLowerInvariant() ?? "front";
        if (side is not "front" and not "back")
            return BadRequest(new { Error = "side must be 'front' or 'back'." });

        // Ownership check.
        var ownershipResult = await VerifyOwnershipAsync(patientId, ct);
        if (ownershipResult is not null)
            return ownershipResult;

        // Retrieve the stored R2 key from the profile.
        var profile = await _db.InsuranceProfiles
            .AsNoTracking()
            .Where(p => p.Id == profileId && p.PatientId == patientId)
            .Select(p => new { p.CardImageFrontKey, p.CardImageBackKey })
            .FirstOrDefaultAsync(ct);

        if (profile is null)
            return NotFound(new { Error = "Insurance profile not found for this patient." });

        // Use dedicated R2 key column (task_003).
        var objectKey = side == "front" ? profile.CardImageFrontKey : profile.CardImageBackKey;

        if (string.IsNullOrEmpty(objectKey))
            return NotFound(new { Error = $"No {side} card image on file for this profile." });

        var (url, expiresAt) = await _cardImageStorage.GetPreSignedUrlAsync(objectKey, ct);

        return Ok(new CardImageUrlResponse
        {
            Url = url,
            ExpiresAt = expiresAt,
            Side = side,
        });
    }

    // ── Private helpers ────────────────────────────────────────────────────────

    /// <summary>
    /// Verifies that the calling user may act on behalf of <paramref name="patientId"/>.
    /// Staff and Admin bypass the check.  Returns a non-null <see cref="IActionResult"/>
    /// when the check fails (caller should return it immediately).
    /// </summary>
    private async Task<IActionResult?> VerifyOwnershipAsync(Guid patientId, CancellationToken ct)
    {
        var isPrivileged = User.IsInRole("Staff") || User.IsInRole("Admin");
        if (isPrivileged)
        {
            var exists = await _db.Patients.AsNoTracking().AnyAsync(p => p.Id == patientId, ct);
            return exists ? null : NotFound();
        }

        var callerId = TryGetCurrentUserId();
        if (callerId is null)
            return Unauthorized();

        var callerPatient = await _db.Patients
            .AsNoTracking()
            .FirstOrDefaultAsync(p => p.UserId == callerId.Value, ct);

        if (callerPatient is null)
            return NotFound();

        return callerPatient.Id == patientId ? null : Forbid();
    }
}
