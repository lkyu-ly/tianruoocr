using System;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Threading;
using System.Windows.Forms;

namespace TrOCR.Helper
{
    public static class ClipboardHelper
    {
        private const int DefaultRetryTimes = 8;
        private const int DefaultRetryDelayMs = 120;

        [DllImport("user32.dll", SetLastError = true)]
        private static extern IntPtr GetOpenClipboardWindow();

        [DllImport("user32.dll", SetLastError = true)]
        private static extern uint GetWindowThreadProcessId(IntPtr hWnd, out uint processId);

        public static bool TrySetDataObject(object data, out string errorMessage)
        {
            errorMessage = null;
            if (data == null)
            {
                return true;
            }

            try
            {
                Clipboard.SetDataObject(data, true, DefaultRetryTimes, DefaultRetryDelayMs);
                return true;
            }
            catch (Exception ex) when (IsClipboardException(ex))
            {
                errorMessage = BuildClipboardErrorMessage(ex);
                Debug.WriteLine(errorMessage);
                return false;
            }
        }

        public static bool TrySetData(string format, object data, out string errorMessage)
        {
            errorMessage = null;
            if (string.IsNullOrEmpty(format) || data == null)
            {
                return true;
            }

            var dataObject = new DataObject();
            dataObject.SetData(format, data);
            return TrySetDataObject(dataObject, out errorMessage);
        }

        public static bool TryGetText(out string text, out string errorMessage)
        {
            text = null;
            errorMessage = null;

            Exception lastException = null;
            for (var attempt = 0; attempt < DefaultRetryTimes; attempt++)
            {
                try
                {
                    if (!Clipboard.ContainsText())
                    {
                        return true;
                    }

                    text = Clipboard.GetText();
                    return true;
                }
                catch (Exception ex) when (IsClipboardException(ex))
                {
                    lastException = ex;
                    Thread.Sleep(DefaultRetryDelayMs);
                }
            }

            errorMessage = BuildClipboardErrorMessage(lastException);
            Debug.WriteLine(errorMessage);
            return false;
        }

        public static bool TryClear(out string errorMessage)
        {
            errorMessage = null;

            Exception lastException = null;
            for (var attempt = 0; attempt < DefaultRetryTimes; attempt++)
            {
                try
                {
                    Clipboard.Clear();
                    return true;
                }
                catch (Exception ex) when (IsClipboardException(ex))
                {
                    lastException = ex;
                    Thread.Sleep(DefaultRetryDelayMs);
                }
            }

            errorMessage = BuildClipboardErrorMessage(lastException);
            Debug.WriteLine(errorMessage);
            return false;
        }

        public static string GetOpenClipboardOwnerDescription()
        {
            var handle = GetOpenClipboardWindow();
            if (handle == IntPtr.Zero)
            {
                return "未能识别占用剪贴板的窗口";
            }

            GetWindowThreadProcessId(handle, out var processId);
            if (processId == 0)
            {
                return $"剪贴板窗口句柄: 0x{handle.ToInt64():X}";
            }

            try
            {
                using (var process = Process.GetProcessById((int)processId))
                {
                    return $"占用进程: {process.ProcessName} (PID {processId})";
                }
            }
            catch
            {
                return $"占用进程 PID: {processId}";
            }
        }

        public static string BuildClipboardErrorMessage(Exception ex)
        {
            var detail = ex == null ? "未知错误" : ex.Message;
            return "剪贴板暂时不可用，" + GetOpenClipboardOwnerDescription() + "。原始错误: " + detail;
        }

        private static bool IsClipboardException(Exception ex)
        {
            return ex is ExternalException ||
                   ex is ThreadStateException ||
                   ex is InvalidOperationException;
        }
    }
}
