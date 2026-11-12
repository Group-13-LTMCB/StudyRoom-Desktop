using System.IO;
using System.Windows.Media.Imaging;
using QRCoder;

namespace StudyRoom.UI.Helpers;

public static class QrCodeHelper
{
    public static BitmapImage GenerateQrCodeBitmap(string payload, int pixelsPerModule = 10)
    {
        using var qrGenerator = new QRCodeGenerator();
        using var qrCodeData = qrGenerator.CreateQrCode(payload, QRCodeGenerator.ECCLevel.M);
        var qrCode = new PngByteQRCode(qrCodeData);
        byte[] qrCodeBytes = qrCode.GetGraphic(pixelsPerModule);

        var image = new BitmapImage();
        using var mem = new MemoryStream(qrCodeBytes);
        image.BeginInit();
        image.CacheOption = BitmapCacheOption.OnLoad;
        image.StreamSource = mem;
        image.EndInit();
        image.Freeze();
        return image;
    }
}
