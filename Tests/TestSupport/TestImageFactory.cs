using System.Drawing;

namespace TrOCR.Tests.TestSupport
{
    /// <summary>
    /// 测试辅助工厂，生成指定颜色的纯色位图用于图像相关单元测试的像素级断言。
    /// </summary>
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
