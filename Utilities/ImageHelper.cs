using System;
using System.Drawing;
using System.Drawing.Imaging;

namespace TrOCR.Helper
{
    public static class ImageHelper
    {
        public static Bitmap CloneBitmap(Image source)
        {
            if (source == null)
            {
                throw new ArgumentNullException(nameof(source));
            }

            var clone = new Bitmap(source.Width, source.Height, PixelFormat.Format32bppArgb);
            using (var graphics = Graphics.FromImage(clone))
            {
                graphics.DrawImage(source, 0, 0, source.Width, source.Height);
            }

            return clone;
        }

        public static bool IsUsable(Image image)
        {
            if (image == null)
            {
                return false;
            }

            try
            {
                return image.Width > 0 && image.Height > 0;
            }
            catch (ArgumentException)
            {
                return false;
            }
            catch (ObjectDisposedException)
            {
                return false;
            }
        }
    }
}
