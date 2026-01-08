using System;
using System.IO;
using System.Security.Cryptography;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace Flow.Launcher.Infrastructure.Image
{
    public static class ImageHashGenerator
    {
        public static string? GetHashFromImage(ImageSource imageSource)
        {
            if (imageSource is not BitmapSource image)
            {
                return null;
            }

            try
            {
                using var outStream = new MemoryStream();
                var enc = new JpegBitmapEncoder();
                var bitmapFrame = BitmapFrame.Create(image, null, null, null);
                bitmapFrame.Freeze();
                enc.Frames.Add(bitmapFrame);
                enc.Save(outStream);
                var byteArray = outStream.GetBuffer();
                var hash = Convert.ToBase64String(SHA1.HashData(byteArray));
                return hash;
            }
            catch
            {
                return null;
            }

        }
    }
}
