using System.Drawing;
using System.Threading;
using System.Windows.Forms;
using NUnit.Framework;
using TrOCR.Tests.TestSupport;

namespace TrOCR.Tests
{
    /// <summary>
    /// 验证贴图窗口 <see cref="FmScreenPaste"/> 持有源图像的独立副本，
    /// 即使外部调用方释放了传入的 Image 实例，窗口绘制仍能正常工作而不抛出异常。
    /// 回归保护：commit 6e05465 修复的"贴图红叉"崩溃。
    /// </summary>
    [TestFixture]
    public class FmScreenPasteTests
    {
        /// <summary>
        /// 构造贴图窗口后立即释放源图像，随后触发 OnPaint。
        /// 验证窗口内部持有深拷贝，渲染输出像素与原始颜色一致。
        /// </summary>
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
