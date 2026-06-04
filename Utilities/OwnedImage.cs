using System;
using System.Drawing;

namespace TrOCR.Helper
{
    public sealed class OwnedImage : IDisposable
    {
        private Bitmap bitmap;

        public OwnedImage(Image source)
        {
            bitmap = ImageHelper.CloneBitmap(source);
        }

        public Bitmap Bitmap
        {
            get
            {
                if (bitmap == null)
                {
                    throw new ObjectDisposedException(nameof(OwnedImage));
                }

                return bitmap;
            }
        }

        public Size Size => Bitmap.Size;

        public int Width => Bitmap.Width;

        public int Height => Bitmap.Height;

        public bool IsUsable => ImageHelper.IsUsable(bitmap);

        public Bitmap CloneBitmap()
        {
            return ImageHelper.CloneBitmap(Bitmap);
        }

        public void Dispose()
        {
            var image = bitmap;
            bitmap = null;
            image?.Dispose();
        }
    }
}
