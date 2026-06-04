using System;
using System.Drawing;
using NUnit.Framework;
using TrOCR.Helper;
using TrOCR.Tests.TestSupport;

namespace TrOCR.Tests
{
    /// <summary>
    /// 验证 <see cref="ImageHelper"/> 提供的图像深拷贝和可用性检测工具方法。
    /// 确保 CloneBitmap 生成完全独立的副本，IsUsable 能正确识别已释放或空引用的图像。
    /// </summary>
    [TestFixture]
    public class ImageHelperTests
    {
        /// <summary>
        /// 克隆后释放源图像，验证克隆副本的尺寸和像素数据仍完整可用，
        /// 且可被 GDI+ 正常绘制（无 InvalidOperationException）。
        /// </summary>
        [Test]
        public void CloneBitmap_SourceDisposed_CloneRemainsUsable()
        {
            var expectedColor = Color.FromArgb(255, 17, 34, 51);
            var source = TestImageFactory.CreateSolidBitmap(12, 8, expectedColor);

            using (var clone = ImageHelper.CloneBitmap(source))
            {
                source.Dispose();

                Assert.That(clone.Width, Is.EqualTo(12));
                Assert.That(clone.Height, Is.EqualTo(8));
                Assert.That(clone.GetPixel(6, 4).ToArgb(), Is.EqualTo(expectedColor.ToArgb()));

                using (var redrawTarget = new Bitmap(clone.Width, clone.Height))
                using (var graphics = Graphics.FromImage(redrawTarget))
                {
                    Assert.DoesNotThrow(() => graphics.DrawImage(clone, 0, 0));
                }
            }
        }

        /// <summary>
        /// 传入 null 时应抛出 ArgumentNullException，防止空引用在后续流程中延迟暴露。
        /// </summary>
        [Test]
        public void CloneBitmap_NullSource_ThrowsArgumentNullException()
        {
            Assert.Throws<ArgumentNullException>(() => ImageHelper.CloneBitmap(null));
        }

        /// <summary>
        /// 已释放的 Image 实例应被判定为不可用，返回 false。
        /// 用于绘制前的安全检查，避免 GDI+ 报错。
        /// </summary>
        [Test]
        public void IsUsable_DisposedImage_ReturnsFalse()
        {
            var image = TestImageFactory.CreateSolidBitmap(4, 3, Color.Red);
            image.Dispose();

            Assert.That(ImageHelper.IsUsable(image), Is.False);
        }

        /// <summary>
        /// null 引用应被判定为不可用，返回 false。
        /// </summary>
        [Test]
        public void IsUsable_NullImage_ReturnsFalse()
        {
            Assert.That(ImageHelper.IsUsable(null), Is.False);
        }
    }
}
