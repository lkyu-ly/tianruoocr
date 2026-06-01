namespace TrOCR.Helper
{
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
