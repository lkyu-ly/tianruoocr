using System.Drawing;

namespace TrOCR.Tests.TestSupport
{
    internal static class TestImageFactory
    {
        public static Bitmap CreateSolidBitmap(int width, int height, Color color)
        {
            var bitmap = new Bitmap(width, height);
            using (var graphics = Graphics.FromImage(bitmap))
            {
                graphics.Clear(color);
            }

            return bitmap;
        }
    }
}
