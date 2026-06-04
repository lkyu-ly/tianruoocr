using System;
using System.ComponentModel;
using System.Runtime.InteropServices;
using System.Threading;
using NUnit.Framework;
using TrOCR.Helper;

namespace TrOCR.Tests
{
    /// <summary>
    /// 验证 <see cref="ClipboardHelper"/> 在剪贴板被占用、参数异常等边界条件下的行为。
    /// 确保重试机制和诊断消息按预期工作，不会因外部剪贴板竞争导致未处理异常。
    /// </summary>
    [TestFixture]
    public class ClipboardHelperTests
    {
        [DllImport("user32.dll", SetLastError = true)]
        private static extern bool OpenClipboard(IntPtr hWndNewOwner);

        [DllImport("user32.dll", SetLastError = true)]
        private static extern bool CloseClipboard();

        /// <summary>
        /// 当传入 null 数据时，TrySetDataObject 应视为无操作并返回成功，不产生错误消息。
        /// 防御调用方未做空值检查就透传给 ClipboardHelper 的场景。
        /// </summary>
        [Test]
        public void TrySetDataObject_NullData_ReturnsTrueWithoutError()
        {
            var result = ClipboardHelper.TrySetDataObject(null, out var errorMessage);

            Assert.That(result, Is.True);
            Assert.That(errorMessage, Is.Null);
        }

        /// <summary>
        /// 验证诊断消息构建方法包含用户友好提示和原始异常信息，
        /// 确保开发者和用户都能从消息中获得有效信息。
        /// </summary>
        [Test]
        public void BuildClipboardErrorMessage_IncludesOriginalErrorMessage()
        {
            var message = ClipboardHelper.BuildClipboardErrorMessage(new InvalidOperationException("open clipboard failed"));

            Assert.That(message, Does.Contain("剪贴板暂时不可用"));
            Assert.That(message, Does.Contain("open clipboard failed"));
        }

        /// <summary>
        /// 模拟另一线程持有剪贴板锁的场景，验证 TrySetDataObject 在重试耗尽后
        /// 返回 false 并提供包含诊断信息的错误消息，而非抛出异常。
        /// 标记为 Explicit：使用全局 Windows 剪贴板，需手动运行。
        /// </summary>
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
