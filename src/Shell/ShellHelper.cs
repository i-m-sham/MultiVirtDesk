using System;
using System.Runtime.InteropServices;
using System.Text;

namespace MultiVirtDesk.Shell
{
    public static class ShellHelper
    {
        [DllImport("shell32.dll", CharSet = CharSet.Unicode)]
        public static extern void SHChangeNotify(int wEventId, int uFlags, string dwItem1, string dwItem2);

        [DllImport("shell32.dll", CharSet = CharSet.Unicode)]
        public static extern void SHChangeNotify(int wEventId, int uFlags, IntPtr dwItem1, IntPtr dwItem2);

        [DllImport("user32.dll", SetLastError = true)]
        public static extern IntPtr FindWindow(string lpClassName, string lpWindowName);

        [DllImport("user32.dll", SetLastError = true)]
        public static extern IntPtr FindWindowEx(IntPtr hwndParent, IntPtr hwndChildAfter, string lpszClass, string lpszWindow);

        [DllImport("user32.dll")]
        public static extern bool EnumWindows(EnumWindowsProc lpEnumFunc, IntPtr lParam);

        [DllImport("user32.dll", CharSet = CharSet.Auto)]
        public static extern IntPtr SendMessage(IntPtr hWnd, uint Msg, IntPtr wParam, IntPtr lParam);

        [DllImport("user32.dll", CharSet = CharSet.Auto)]
        public static extern bool PostMessage(IntPtr hWnd, uint Msg, IntPtr wParam, IntPtr lParam);

        [DllImport("kernel32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
        public static extern bool MoveFileEx(string lpExistingFileName, string lpNewFileName, int dwFlags);

        public delegate bool EnumWindowsProc(IntPtr hWnd, IntPtr lParam);

        public const int MOVEFILE_REPLACE_EXISTING = 0x00000001;
        public const int MOVEFILE_WRITE_THROUGH = 0x00000008;

        public const int SHCNE_UPDATEDIR = 0x00001000;
        public const int SHCNE_ALLEVENTS = 0x7FFFFFFF;
        public const int SHCNF_PATHW = 0x0005;
        public const int SHCNF_FLUSH = 0x0001;
        public const uint WM_COMMAND = 0x0111;
        public const int CMD_REFRESH = 0x7103; // Desktop F5 refresh command ID

        public static IntPtr GetShellDefView()
        {
            // 1. Try finding in Progman
            IntPtr progman = FindWindow("Progman", "Program Manager");
            if (progman != IntPtr.Zero)
            {
                IntPtr shellDef = FindWindowEx(progman, IntPtr.Zero, "SHELLDLL_DefView", null);
                if (shellDef != IntPtr.Zero)
                {
                    return shellDef;
                }
            }

            // 2. Look in WorkerW windows (standard in Windows 10/11)
            IntPtr found = IntPtr.Zero;
            EnumWindows(delegate (IntPtr hwnd, IntPtr lParam)
            {
                IntPtr shell = FindWindowEx(hwnd, IntPtr.Zero, "SHELLDLL_DefView", null);
                if (shell != IntPtr.Zero)
                {
                    found = shell;
                    return false; // Stop enumeration
                }
                return true;
            }, IntPtr.Zero);

            return found;
        }

        public static void RefreshDesktop(string desktopPath)
        {
            try
            {
                // Notify shell of directory change
                SHChangeNotify(SHCNE_UPDATEDIR, SHCNF_PATHW | SHCNF_FLUSH, desktopPath, null);

                // Post F5 command directly to Explorer Desktop view
                IntPtr shellDefView = GetShellDefView();
                if (shellDefView != IntPtr.Zero)
                {
                    PostMessage(shellDefView, WM_COMMAND, (IntPtr)CMD_REFRESH, IntPtr.Zero);
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine("[MultiVirtDesk] RefreshDesktop error: " + ex.Message);
            }
        }
    }
}
