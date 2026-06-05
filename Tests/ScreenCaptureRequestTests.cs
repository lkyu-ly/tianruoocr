using NUnit.Framework;
using TrOCR.Services.ScreenCapture;

namespace TrOCR.Tests
{
    [TestFixture]
    public class ScreenCaptureRequestTests
    {
        [Test]
        public void ForOcr_ReturnsExpectedMagnifierSettings()
        {
            var request = ScreenCaptureRequest.ForOcr();

            Assert.That(request.ShowMagnifier, Is.False);
            Assert.That(request.UseSquareMagnifier, Is.False);
            Assert.That(request.MagnifierPixelCount, Is.EqualTo(15));
            Assert.That(request.MagnifierPixelSize, Is.EqualTo(10));
        }
    }
}
