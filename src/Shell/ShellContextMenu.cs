using System;
using System.IO;
using System.Reflection;
using Microsoft.Win32;

namespace MultiVirtDesk.Shell
{
    public static class ShellContextMenu
    {
        private const string RootVerb = "MultiVirtDesk";

        public static void RegisterContextMenu(string exePath)
        {
            try
            {
                if (string.IsNullOrEmpty(exePath))
                {
                    exePath = Assembly.GetExecutingAssembly().Location;
                }

                RegisterForTarget(@"*\shell\MultiVirtDesk", exePath);
                RegisterForTarget(@"Directory\shell\MultiVirtDesk", exePath);
                Console.WriteLine("[MultiVirtDesk] Context menu registered successfully.");
            }
            catch (Exception ex)
            {
                Console.WriteLine("[MultiVirtDesk] RegisterContextMenu error: " + ex.Message);
            }
        }

        private static void RegisterForTarget(string subKeyPath, string exePath)
        {
            using (RegistryKey baseKey = Registry.CurrentUser.CreateSubKey(@"Software\Classes\" + subKeyPath))
            {
                if (baseKey != null)
                {
                    baseKey.SetValue("MUIVerb", "MultiVirtDesk");
                    baseKey.SetValue("SubCommands", "");

                    using (RegistryKey shellKey = baseKey.CreateSubKey("shell"))
                    {
                        // 1. Pin
                        using (RegistryKey pinKey = shellKey.CreateSubKey("Pin"))
                        {
                            pinKey.SetValue("MUIVerb", "Pin to All Desktops");
                            using (RegistryKey cmdKey = pinKey.CreateSubKey("command"))
                            {
                                cmdKey.SetValue("", string.Format("\"{0}\" --pin \"%1\"", exePath));
                            }
                        }

                        // 2. Unpin
                        using (RegistryKey unpinKey = shellKey.CreateSubKey("Unpin"))
                        {
                            unpinKey.SetValue("MUIVerb", "Unpin from All Desktops");
                            using (RegistryKey cmdKey = unpinKey.CreateSubKey("command"))
                            {
                                cmdKey.SetValue("", string.Format("\"{0}\" --unpin \"%1\"", exePath));
                            }
                        }

                        // 3. Move to Desktop
                        using (RegistryKey moveKey = shellKey.CreateSubKey("Move"))
                        {
                            moveKey.SetValue("MUIVerb", "Move to Virtual Desktop...");
                            using (RegistryKey cmdKey = moveKey.CreateSubKey("command"))
                            {
                                cmdKey.SetValue("", string.Format("\"{0}\" --dialog-move \"%1\"", exePath));
                            }
                        }
                    }
                }
            }
        }

        public static void UnregisterContextMenu()
        {
            try
            {
                Registry.CurrentUser.DeleteSubKeyTree(@"Software\Classes\*\shell\MultiVirtDesk", false);
                Registry.CurrentUser.DeleteSubKeyTree(@"Software\Classes\Directory\shell\MultiVirtDesk", false);
                Console.WriteLine("[MultiVirtDesk] Context menu unregistered.");
            }
            catch (Exception ex)
            {
                Console.WriteLine("[MultiVirtDesk] UnregisterContextMenu error: " + ex.Message);
            }
        }

        public static bool IsContextMenuRegistered()
        {
            try
            {
                using (RegistryKey key = Registry.CurrentUser.OpenSubKey(@"Software\Classes\*\shell\MultiVirtDesk"))
                {
                    return key != null;
                }
            }
            catch
            {
                return false;
            }
        }
    }
}
