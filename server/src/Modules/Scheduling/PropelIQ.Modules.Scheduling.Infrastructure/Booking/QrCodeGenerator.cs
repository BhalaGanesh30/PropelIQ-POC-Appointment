using QRCoder;

namespace PropelIQ.Modules.Scheduling.Infrastructure.Booking;

/// <summary>
/// Generates a PNG QR code that uniquely identifies the appointment.
/// AC-2: QR code encodes <c>{appointmentId}|{confirmationCode}</c> for scanner verification.
/// </summary>
public sealed class QrCodeGenerator
{
    public byte[] GenerateQrCode(string confirmationCode, Guid appointmentId)
    {
        // Payload uniquely identifies both the record and its confirmation code.
        var payload = $"{appointmentId}|{confirmationCode}";

        using var qrGenerator = new QRCodeGenerator();
        using var qrCodeData = qrGenerator.CreateQrCode(payload, QRCodeGenerator.ECCLevel.M);
        using var qrCode = new PngByteQRCode(qrCodeData);

        // 10 pixels per module — produces a ~330×330px image at typical module counts.
        return qrCode.GetGraphic(10);
    }
}
