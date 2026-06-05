using System.Drawing;
using NUnit.Framework;
using ZXing;
using ZXing.Common;
using ZXing.QrCode;

namespace TrOCR.Tests
{
    [TestFixture]
    public class ZxingCompatibilityTests
    {
        [Test]
        public void QrCode_CanRoundTripUsingCurrentReaderPath()
        {
            const string expected = "https://github.com/tianruoocr/test";

            var writer = new BarcodeWriter
            {
                Format = BarcodeFormat.QR_CODE,
                Options = new EncodingOptions
                {
                    Width = 180,
                    Height = 180,
                    Margin = 1
                }
            };

            using (var bitmap = writer.Write(expected))
            {
                var binaryBitmap = new BinaryBitmap(new HybridBinarizer(new BitmapLuminanceSource(bitmap)));
                var result = new QRCodeReader().decode(binaryBitmap);

                Assert.That(result, Is.Not.Null);
                Assert.That(result.Text, Is.EqualTo(expected));
            }
        }

        [Test]
        public void Zxing_AssemblyVersionIsModern()
        {
            var assemblyName = typeof(QRCodeReader).Assembly.GetName();

            Assert.That(assemblyName.Name, Is.EqualTo("zxing"));
            Assert.That(assemblyName.Version.Major, Is.EqualTo(0));
            Assert.That(assemblyName.Version.Minor, Is.EqualTo(16));
            Assert.That(assemblyName.Version.Build, Is.GreaterThanOrEqualTo(11));
        }
    }
}
