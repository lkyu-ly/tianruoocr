using System;
using System.Runtime.InteropServices;
using System.Diagnostics;


namespace TrOCR.Helper
{
    public static class TrOCRUtils
    {
        // 定义 LOAD_LIBRARY_SEARCH_DEFAULT_DIRS flag
        // 这个flag告诉系统在搜索DLL时包含标准的搜索路径（如System32）
        private const uint LOAD_LIBRARY_SEARCH_DEFAULT_DIRS = 0x00001000;
        private const uint LOAD_LIBRARY_SEARCH_USER_DIRS = 0x00000400;

        // 导入 SetDefaultDllDirectories 函数
        [DllImport("kernel32.dll", CharSet = CharSet.Auto, SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        public static extern bool SetDefaultDllDirectories(uint DirectoryFlags);

        // 导入 AddDllDirectory 函数
        [DllImport("kernel32.dll", CharSet = CharSet.Auto, SetLastError = true)]
        public static extern IntPtr AddDllDirectory(string NewDirectory);

        [DllImport("kernel32.dll")]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool SetProcessWorkingSetSize(IntPtr process,
            IntPtr minimumWorkingSetSize, IntPtr maximumWorkingSetSize);

         public static void CleanMemory(){
			//垃圾回收
			GC.Collect();
            GC.WaitForPendingFinalizers();
			//释放工作集内存
            if (Environment.OSVersion.Platform == PlatformID.Win32NT)
            {
                SetProcessWorkingSetSize(Process.GetCurrentProcess().Handle, (IntPtr)(-1), (IntPtr)(-1));
            }
		}


    }
}