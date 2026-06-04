namespace TrOCR.Helper
{
    /// <summary>
    /// CommonHelper 的测试桩实现。测试项目通过 Compile Include 直接编译 FmScreenPaste 等生产源码，
    /// 这些代码引用了 CommonHelper.ShowHelpMsg，此桩提供无副作用的替代实现以满足编译依赖。
    /// </summary>
    public static class CommonHelper
    {
        public static string LastMessage { get; private set; }

        public static uint LastDurationMs { get; private set; }

        public static void ShowHelpMsg(string msg)
        {
            ShowHelpMsg(msg, 600u);
        }

        public static void ShowHelpMsg(string msg, uint durationMs)
        {
            LastMessage = msg;
            LastDurationMs = durationMs;
        }

        public static void Reset()
        {
            LastMessage = null;
            LastDurationMs = 0u;
        }
    }
}
