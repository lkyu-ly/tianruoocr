using System;
using System.Drawing;
using NUnit.Framework;
using TrOCR.Helper;
using TrOCR.Tests.TestSupport;

namespace TrOCR.Tests
{
    /// <summary>
    /// 验证 <see cref="OwnedImage"/> 的所有权语义：
    /// 构造时深拷贝源图像使其独立于外部生命周期，Dispose 后禁止访问内部位图。
    /// 这是贴图窗口和截图流程中图像安全管理的基础设施。
    /// </summary>
    [TestFixture]
    public class OwnedImageTests
    {
        /// <summary>
        /// 构造 OwnedImage 后释放源图像，验证内部副本仍可用，
        /// 尺寸、像素数据正确，且 CloneBitmap 能再次生成独立副本。
        /// </summary>
        [Test]
        public void Constructor_SourceDisposed_OwnedBitmapRemainsUsable()
        {
            var expectedColor = Color.FromArgb(255, 80, 120, 160);
            var source = TestImageFactory.CreateSolidBitmap(10, 7, expectedColor);

            using (var owned = new OwnedImage(source))
            {
                source.Dispose();

                Assert.That(owned.IsUsable, Is.True);
                Assert.That(owned.Width, Is.EqualTo(10));
                Assert.That(owned.Height, Is.EqualTo(7));
                Assert.That(owned.Bitmap.GetPixel(5, 3).ToArgb(), Is.EqualTo(expectedColor.ToArgb()));

                using (var copy = owned.CloneBitmap())
                {
                    Assert.That(copy.Width, Is.EqualTo(10));
                    Assert.That(copy.Height, Is.EqualTo(7));
                    Assert.That(copy.GetPixel(5, 3).ToArgb(), Is.EqualTo(expectedColor.ToArgb()));
                }
            }
        }

        /// <summary>
        /// Dispose 后 IsUsable 应返回 false，访问 Bitmap 属性应抛出 ObjectDisposedException。
        /// 确保已释放的 OwnedImage 不会被误用于绘制。
        /// </summary>
        [Test]
        public void Dispose_AccessingBitmap_ThrowsObjectDisposedException()
        {
            var source = TestImageFactory.CreateSolidBitmap(3, 2, Color.Blue);
            var owned = new OwnedImage(source);

            owned.Dispose();
            source.Dispose();

            Assert.That(owned.IsUsable, Is.False);
            Assert.Throws<ObjectDisposedException>(() =>
            {
                var ignored = owned.Bitmap;
            });
        }
    }
}
