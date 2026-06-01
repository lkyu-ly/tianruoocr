using System;
using System.Drawing;
using NUnit.Framework;
using TrOCR.Helper;
using TrOCR.Tests.TestSupport;

namespace TrOCR.Tests
{
    [TestFixture]
    public class OwnedImageTests
    {
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
