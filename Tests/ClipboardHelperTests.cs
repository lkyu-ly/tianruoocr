using System;
using System.ComponentModel;
using System.Runtime.InteropServices;
using System.Threading;
using NUnit.Framework;
using TrOCR.Helper;

namespace TrOCR.Tests
{
    [TestFixture]
    public class ClipboardHelperTests
    {
        [DllImport("user32.dll", SetLastError = true)]
        private static extern bool OpenClipboard(IntPtr hWndNewOwner);

        [DllImport("user32.dll", SetLastError = true)]
        private static extern bool CloseClipboard();

        [Test]
        public void TrySetDataObject_NullData_ReturnsTrueWithoutError()
        {
            var result = ClipboardHelper.TrySetDataObject(null, out var errorMessage);

            Assert.That(result, Is.True);
            Assert.That(errorMessage, Is.Null);
        }

        [Test]
        public void BuildClipboardErrorMessage_IncludesOriginalErrorMessage()
        {
            var message = ClipboardHelper.BuildClipboardErrorMessage(new InvalidOperationException("open clipboard failed"));

            Assert.That(message, Does.Contain("剪贴板暂时不可用"));
            Assert.That(message, Does.Contain("open clipboard failed"));
        }

        [Test]
        [Explicit("Uses the global Windows clipboard. Run manually when validating clipboard-lock behavior.")]
        [Apartment(ApartmentState.STA)]
        public void TrySetDataObject_WhenClipboardHeldByAnotherThread_ReturnsFalseWithDiagnostic()
        {
            using (var clipboardOpened = new ManualResetEventSlim(false))
            using (var releaseClipboard = new ManualResetEventSlim(false))
            {
                Exception holderException = null;
                var holderThread = new Thread(() =>
                {
                    var opened = false;
                    try
                    {
                        opened = OpenClipboard(IntPtr.Zero);
                        if (!opened)
                        {
                            throw new Win32Exception(Marshal.GetLastWin32Error());
                        }

                        clipboardOpened.Set();
                        releaseClipboard.Wait(TimeSpan.FromSeconds(10));
                    }
                    catch (Exception ex)
                    {
                        holderException = ex;
                        clipboardOpened.Set();
                    }
                    finally
                    {
                        if (opened)
                        {
                            CloseClipboard();
                        }
                    }
                });

                holderThread.SetApartmentState(ApartmentState.STA);
                holderThread.Start();

                Assert.That(clipboardOpened.Wait(TimeSpan.FromSeconds(3)), Is.True, "clipboard holder thread did not start");
                if (holderException != null)
                {
                    Assert.Fail("clipboard holder thread failed: " + holderException);
                }

                try
                {
                    var result = ClipboardHelper.TrySetDataObject("clipboard-lock-test", out var errorMessage);

                    Assert.That(result, Is.False);
                    Assert.That(errorMessage, Does.Contain("剪贴板暂时不可用"));
                }
                finally
                {
                    releaseClipboard.Set();
                    Assert.That(holderThread.Join(TimeSpan.FromSeconds(3)), Is.True, "clipboard holder thread did not exit");
                }
            }
        }
    }
}
