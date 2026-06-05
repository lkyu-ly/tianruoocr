using System.Drawing;
using NUnit.Framework;
using TrOCR.Services.ScreenCapture;

namespace TrOCR.Tests
{
    [TestFixture]
    public class ScreenCaptureResultTests
    {
        [Test]
        public void Constructor_NormalizesNullModeFlag()
        {
            var result = new ScreenCaptureResult(null, null, Point.Empty, null);

            Assert.That(result.ModeFlag, Is.EqualTo(string.Empty));
        }

        [Test]
        public void Constructor_NormalizesNullRectangles()
        {
            var result = new ScreenCaptureResult(null, "截图", Point.Empty, null);

            Assert.That(result.SelectedRectangles, Is.Not.Null);
            Assert.That(result.SelectedRectangles, Is.Empty);
        }

        [Test]
        public void Constructor_PreservesImageAndPointAndRectangles()
        {
            using (var image = new Bitmap(10, 10))
            {
                var point = new Point(3, 4);
                var rectangles = new[] { new Rectangle(1, 2, 3, 4) };

                var result = new ScreenCaptureResult(image, "区域多选", point, rectangles);

                Assert.That(result.Image, Is.SameAs(image));
                Assert.That(result.ModeFlag, Is.EqualTo("区域多选"));
                Assert.That(result.FlagLocation, Is.EqualTo(point));
                Assert.That(result.SelectedRectangles, Is.EqualTo(rectangles));
            }
        }
    }
}
