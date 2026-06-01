using System;
using System.Drawing;
using NUnit.Framework;
using TrOCR.Helper;
using TrOCR.Tests.TestSupport;

namespace TrOCR.Tests
{
    [TestFixture]
    public class ImageHelperTests
    {
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

        [Test]
        public void CloneBitmap_NullSource_ThrowsArgumentNullException()
        {
            Assert.Throws<ArgumentNullException>(() => ImageHelper.CloneBitmap(null));
        }

        [Test]
        public void IsUsable_DisposedImage_ReturnsFalse()
        {
            var image = TestImageFactory.CreateSolidBitmap(4, 3, Color.Red);
            image.Dispose();

            Assert.That(ImageHelper.IsUsable(image), Is.False);
        }

        [Test]
        public void IsUsable_NullImage_ReturnsFalse()
        {
            Assert.That(ImageHelper.IsUsable(null), Is.False);
        }
    }
}
