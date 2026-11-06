using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Threading;
using System.Windows.Forms;
using Microsoft.Win32;
using MultiVirtDesk.Shell;
using MultiVirtDesk.Storage;
using MultiVirtDesk.UI;
using MultiVirtDesk.VirtualDesktop;

namespace MultiVirtDesk
{
    static class Program
    {
        [DllImport("kernel32.dll", SetLastError = true)]
        static extern bool AttachConsole(int dwProcessId);

        [DllImport("kernel32.dll", SetLastError = true)]
        static extern IntPtr GetStdHandle(int nStdHandle);

        private const int ATTACH_PARENT_PROCESS = -1;
        private const int STD_OUTPUT_HANDLE = -11;

        static void EnsureConsoleOutput()
        {
            AttachConsole(ATTACH_PARENT_PROCESS);
            try
            {
                IntPtr stdHandle = GetStdHandle(STD_OUTPUT_HANDLE);
                if (stdHandle != IntPtr.Zero && stdHandle != new IntPtr(-1))
                {
                    Microsoft.Win32.SafeHandles.SafeFileHandle safeFileHandle = new Microsoft.Win32.SafeHandles.SafeFileHandle(stdHandle, false);
                    FileStream fileStream = new FileStream(safeFileHandle, FileAccess.Write);
                    StreamWriter standardOutput = new StreamWriter(fileStream, System.Text.Encoding.Default);
                    standardOutput.AutoFlush = true;
                    Console.SetOut(standardOutput);
                    Console.SetError(standardOutput);
                }
            }
            catch { }
        }

        private const string MutexName = "Global\\MultiVirtDesk_SingleInstanceMutex";

        [STAThread]
        static void Main(string[] args)
        {
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);

            DesktopStorageManager storage = new DesktopStorageManager();

            if (args.Length > 0)
            {
                EnsureConsoleOutput();
                string cmd = args[0].ToLowerInvariant();

                if (cmd == "--pin" && args.Length > 1)
                {
                    string target = args[1];
                    storage.PinItem(target);
                    return;
                }
                else if (cmd == "--unpin" && args.Length > 1)
                {
                    string target = args[1];
                    storage.UnpinItem(target);
                    return;
                }
                else if (cmd == "--dialog-move" && args.Length > 1)
                {
                    string target = args[1];
                    VirtualDesktopService vd = new VirtualDesktopService();
                    Application.Run(new MoveDialog(target, storage, vd));
                    return;
                }
                else if (cmd == "--save-layout")
                {
                    using (VirtualDesktopService vd = new VirtualDesktopService())
                    {
                        DesktopInfo cur = vd.GetCurrentDesktop();
                        if (cur != null)
                        {
                            string vault = storage.GetVaultDirectory(cur.Id);
                            IconLayoutManager.SavePositions(vault);
                            Console.WriteLine("Icon positions saved for workspace: " + cur.Name);
                        }
                    }
                    return;
                }
                else if (cmd == "--restore-layout")
                {
                    using (VirtualDesktopService vd = new VirtualDesktopService())
                    {
                        DesktopInfo cur = vd.GetCurrentDesktop();
                        if (cur != null)
                        {
                            string vault = storage.GetVaultDirectory(cur.Id);
                            IconLayoutManager.RestorePositions(vault);
                            Console.WriteLine("Icon positions restored for workspace: " + cur.Name);
                        }
                    }
                    return;
                }
                else if (cmd == "--status")
                {
                    Console.WriteLine("\n=== MultiVirtDesk Status ===");
                    try
                    {
                        using (VirtualDesktopService vd = new VirtualDesktopService())
                        {
                            DesktopInfo cur = vd.GetCurrentDesktop();
                            Console.WriteLine("Active Desktop: " + (cur != null ? cur.Name + " (" + cur.Id + ")" : "Unknown"));
                            Console.WriteLine("\nAll Desktops:");
                            foreach (DesktopInfo desk in vd.GetDesktops())
                            {
                                Console.WriteLine(string.Format("  [{0}] {1} (ID: {2}) {3}",
                                    desk.Index + 1, desk.Name, desk.Id, desk.IsVisible ? "<-- CURRENT" : ""));
                            }
                        }

                        Console.WriteLine("\nPinned Items (Visible on all desktops):");
                        List<string> pinned = storage.GetPinnedItems();
                        if (pinned.Count == 0)
                        {
                            Console.WriteLine("  (None)");
                        }
                        else
                        {
                            foreach (string p in pinned)
                            {
                                Console.WriteLine("  - " + p);
                            }
                        }
                        Console.WriteLine("============================");
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine("Error querying status: " + ex.Message);
                    }
                    return;
                }
                else if (cmd == "--restore-all")
                {
                    Console.WriteLine("Restoring all items to main desktop...");
                    storage.RestoreAllToDesktop();
                    Console.WriteLine("All items restored.");
                    return;
                }
                else if (cmd == "--adopt-public")
                {
                    Console.WriteLine("Adopting Public Desktop shortcuts...");
                    storage.AdoptPublicShortcuts();
                    Console.WriteLine("Public shortcuts adopted.");
                    return;
                }
                else if (cmd == "--install-startup")
                {
                    string exePath = Assembly.GetExecutingAssembly().Location;
                    using (RegistryKey key = Registry.CurrentUser.OpenSubKey(@"Software\Microsoft\Windows\CurrentVersion\Run", true))
                    {
                        if (key != null)
                        {
                            key.SetValue("MultiVirtDesk", string.Format("\"{0}\" --silent", exePath));
                            Console.WriteLine("MultiVirtDesk added to Windows Startup.");
                        }
                    }
                    return;
                }
                else if (cmd == "--uninstall-startup")
                {
                    using (RegistryKey key = Registry.CurrentUser.OpenSubKey(@"Software\Microsoft\Windows\CurrentVersion\Run", true))
                    {
                        if (key != null)
                        {
                            key.DeleteValue("MultiVirtDesk", false);
                            Console.WriteLine("MultiVirtDesk removed from Windows Startup.");
                        }
                    }
                    return;
                }
                else if (cmd == "--install-context-menu")
                {
                    ShellContextMenu.RegisterContextMenu(Assembly.GetExecutingAssembly().Location);
                    return;
                }
                else if (cmd == "--uninstall-context-menu")
                {
                    ShellContextMenu.UnregisterContextMenu();
                    return;
                }
            }

            // Normal background daemon execution
            bool createdNew;
            using (Mutex mutex = new Mutex(true, MutexName, out createdNew))
            {
                if (!createdNew)
                {
                    // Already running
                    return;
                }

                VirtualDesktopService vdService = null;
                try
                {
                    vdService = new VirtualDesktopService();
                    Application.Run(new TrayApplication(vdService, storage));
                }
                finally
                {
                    if (vdService != null)
                    {
                        vdService.Dispose();
                    }
                }
            }
        }
    }
}
