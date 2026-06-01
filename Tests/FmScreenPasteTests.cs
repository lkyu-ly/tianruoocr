using System.Drawing;
using System.Threading;
using System.Windows.Forms;
using NUnit.Framework;
using TrOCR.Tests.TestSupport;

namespace TrOCR.Tests
{
    [TestFixture]
    public class FmScreenPasteTests
    {
        [Test]
        [Apartment(ApartmentState.STA)]
        public void OnPaint_SourceImageDisposedAfterConstruction_RendersOwnedCopy()
        {
            var expectedColor = Color.FromArgb(255, 201, 44, 90);
            var source = TestImageFactory.CreateSolidBitmap(16, 12, expectedColor);

            using (var form = new TestableFmScreenPaste(source))
            {
                source.Dispose();

                using (var target = new Bitmap(form.Width, form.Height))
                using (var graphics = Graphics.FromImage(target))
                {
                    Assert.DoesNotThrow(() => form.PaintForTest(graphics));
                    Assert.That(target.GetPixel(8, 6).ToArgb(), Is.EqualTo(expectedColor.ToArgb()));
                }
            }
        }

        private sealed class TestableFmScreenPaste : FmScreenPaste
        {
            public TestableFmScreenPaste(Image image)
                : base(image, Point.Empty)
            {
            }

            public void PaintForTest(Graphics graphics)
            {
                using (var args = new PaintEventArgs(graphics, new Rectangle(Point.Empty, Size)))
                {
                    base.OnPaint(args);
                }
            }
        }
    }
}
