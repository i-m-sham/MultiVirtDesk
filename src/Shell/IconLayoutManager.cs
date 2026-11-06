using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;

namespace MultiVirtDesk.Shell
{
    [StructLayout(LayoutKind.Sequential)]
    public struct POINT
    {
        public int x;
        public int y;
        public POINT(int x, int y) { this.x = x; this.y = y; }
    }

    [ComImport]
    [Guid("9BA05972-F6A8-11CF-A442-00A0C90A8F39")]
    internal class CShellWindows { }

    [ComImport]
    [Guid("85CB6900-4D95-11CF-960C-0080C7F4EE85")]
    [InterfaceType(ComInterfaceType.InterfaceIsIDispatch)]
    internal interface IShellWindows
    {
        [return: MarshalAs(UnmanagedType.IDispatch)]
        object FindWindowSW(
            [MarshalAs(UnmanagedType.Struct)] ref object pvarloc,
            [MarshalAs(UnmanagedType.Struct)] ref object pvarlocRoot,
            int swClass,
            out int pHWND,
            int swfwOptions
        );
    }

    [ComImport, InterfaceType(ComInterfaceType.InterfaceIsIUnknown), Guid("6d5140c1-7436-11ce-8034-00aa006009fa")]
    internal interface IServiceProvider
    {
        [return: MarshalAs(UnmanagedType.IUnknown)]
        object QueryService(ref Guid guidService, ref Guid riid);
    }

    [ComImport, InterfaceType(ComInterfaceType.InterfaceIsIUnknown), Guid("000214E2-0000-0000-C000-000000000046")]
    internal interface IShellBrowser
    {
        void _VtblGap1_12();
        [return: MarshalAs(UnmanagedType.IUnknown)]
        object QueryActiveShellView();
    }

    [ComImport, InterfaceType(ComInterfaceType.InterfaceIsIUnknown), Guid("cde725b0-ccc9-4519-917e-325d72fab4ce")]
    internal interface IFolderView
    {
        [PreserveSig] int GetCurrentViewMode(out uint pViewMode);
        [PreserveSig] int SetCurrentViewMode(uint ViewMode);
        [PreserveSig] int GetFolder(ref Guid riid, [MarshalAs(UnmanagedType.Interface)] out object ppv);
        [PreserveSig] int Item(int iItemIndex, out IntPtr ppidl);
        [PreserveSig] int ItemCount(uint uFlags, out int pcItems);
        [PreserveSig] int Items(uint uFlags, ref Guid riid, [MarshalAs(UnmanagedType.Interface)] out object ppv);
        [PreserveSig] int GetSelectionMarkedItem(out int piItem);
        [PreserveSig] int GetFocusedItem(out int piItem);
        [PreserveSig] int GetItemPosition(IntPtr pidl, out POINT ppt);
        [PreserveSig] int GetSpacing(out POINT ppt);
        [PreserveSig] int GetDefaultSpacing(out POINT ppt);
        [PreserveSig] int GetAutoArrange();
        [PreserveSig] int SelectItem(int iItem, uint dwFlags);
        [PreserveSig] int SelectAndPositionItems(uint cidl, [MarshalAs(UnmanagedType.LPArray)] IntPtr[] apidl, [MarshalAs(UnmanagedType.LPArray)] POINT[] apt, uint dwFlags);
    }

    public static class IconLayoutManager
    {
        private const uint SVSI_POSITIONITEM = 0x00000002;
        private const string PositionsFileName = "icon_positions.txt";

        [DllImport("shell32.dll", CharSet = CharSet.Unicode)]
        private static extern bool SHGetPathFromIDList(IntPtr pidl, StringBuilder pszPath);

        [DllImport("shell32.dll", SetLastError = true)]
        private static extern int SHGetNameFromIDList(IntPtr pidl, uint sigdnName, out IntPtr ppszName);

        private static IFolderView GetDesktopFolderView()
        {
            try
            {
                IShellWindows shellWindows = (IShellWindows)new CShellWindows();
                object vtLoc = 0; // CSIDL_DESKTOP
                object vtEmpty = Type.Missing;
                int hwnd;

                object dispObj = shellWindows.FindWindowSW(ref vtLoc, ref vtEmpty, 8 /* SWC_DESKTOP */, out hwnd, 1 /* SWFO_NEEDDISPATCH */);
                if (dispObj == null) return null;

                IServiceProvider serviceProvider = (IServiceProvider)dispObj;
                Guid SID_STopLevelBrowser = new Guid("4C96BE40-915C-11CF-99D3-00AA004AE837");
                Guid IID_IShellBrowser = new Guid("000214E2-0000-0000-C000-000000000046");
                object browserObj = serviceProvider.QueryService(ref SID_STopLevelBrowser, ref IID_IShellBrowser);

                IShellBrowser shellBrowser = (IShellBrowser)browserObj;
                object viewObj = shellBrowser.QueryActiveShellView();

                return (IFolderView)viewObj;
            }
            catch (Exception ex)
            {
                Console.WriteLine("[MultiVirtDesk] GetDesktopFolderView error: " + ex.Message);
                return null;
            }
        }

        public static void SavePositions(string vaultDirectory)
        {
            if (string.IsNullOrEmpty(vaultDirectory)) return;

            try
            {
                IFolderView folderView = GetDesktopFolderView();
                if (folderView == null) return;

                int count;
                folderView.ItemCount(0, out count);
                if (count <= 0) return;

                List<string> lines = new List<string>();

                for (int i = 0; i < count; i++)
                {
                    IntPtr pidl;
                    if (folderView.Item(i, out pidl) == 0 && pidl != IntPtr.Zero)
                    {
                        string name = ResolveItemName(pidl);
                        if (!string.IsNullOrEmpty(name) && !string.Equals(name, "desktop.ini", StringComparison.OrdinalIgnoreCase))
                        {
                            POINT pt;
                            if (folderView.GetItemPosition(pidl, out pt) == 0)
                            {
                                lines.Add(string.Format("{0}={1},{2}", name, pt.x, pt.y));
                            }
                        }
                        Marshal.FreeCoTaskMem(pidl);
                    }
                }

                if (!Directory.Exists(vaultDirectory)) Directory.CreateDirectory(vaultDirectory);
                string filePath = Path.Combine(vaultDirectory, PositionsFileName);
                File.WriteAllLines(filePath, lines.ToArray());
            }
            catch (Exception ex)
            {
                Console.WriteLine("[MultiVirtDesk] SavePositions error: " + ex.Message);
            }
        }

        public static void RestorePositions(string vaultDirectory)
        {
            if (string.IsNullOrEmpty(vaultDirectory)) return;
            string filePath = Path.Combine(vaultDirectory, PositionsFileName);
            if (!File.Exists(filePath)) return;

            Dictionary<string, POINT> savedPositions = new Dictionary<string, POINT>(StringComparer.OrdinalIgnoreCase);
            try
            {
                string[] lines = File.ReadAllLines(filePath);
                foreach (string line in lines)
                {
                    string trimmed = line.Trim();
                    if (string.IsNullOrEmpty(trimmed) || trimmed.StartsWith("#")) continue;

                    int eq = trimmed.IndexOf('=');
                    if (eq > 0)
                    {
                        string name = trimmed.Substring(0, eq).Trim();
                        string coords = trimmed.Substring(eq + 1).Trim();
                        string[] parts = coords.Split(',');
                        if (parts.Length == 2)
                        {
                            int x, y;
                            if (int.TryParse(parts[0], out x) && int.TryParse(parts[1], out y))
                            {
                                savedPositions[name] = new POINT(x, y);
                            }
                        }
                    }
                }
            }
            catch { return; }

            if (savedPositions.Count == 0) return;

            // Give Explorer a brief moment to finish re-indexing items
            ThreadPool.QueueUserWorkItem(delegate
            {
                Thread.Sleep(80);

                try
                {
                    IFolderView folderView = GetDesktopFolderView();
                    if (folderView == null) return;

                    int count;
                    folderView.ItemCount(0, out count);

                    for (int i = 0; i < count; i++)
                    {
                        IntPtr pidl;
                        if (folderView.Item(i, out pidl) == 0 && pidl != IntPtr.Zero)
                        {
                            string name = ResolveItemName(pidl);
                            POINT targetPt;
                            if (!string.IsNullOrEmpty(name) && savedPositions.TryGetValue(name, out targetPt))
                            {
                                folderView.SelectAndPositionItems(1, new IntPtr[] { pidl }, new POINT[] { targetPt }, SVSI_POSITIONITEM);
                            }
                            Marshal.FreeCoTaskMem(pidl);
                        }
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine("[MultiVirtDesk] RestorePositions thread error: " + ex.Message);
                }
            });
        }

        private static string ResolveItemName(IntPtr pidl)
        {
            StringBuilder sb = new StringBuilder(520);
            if (SHGetPathFromIDList(pidl, sb) && sb.Length > 0)
            {
                return Path.GetFileName(sb.ToString());
            }

            IntPtr pName;
            if (SHGetNameFromIDList(pidl, 0 /* SIGDN_NORMALDISPLAY */, out pName) == 0 && pName != IntPtr.Zero)
            {
                string display = Marshal.PtrToStringUni(pName);
                Marshal.FreeCoTaskMem(pName);
                return display;
            }

            return null;
        }
    }
}
