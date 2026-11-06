using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Reflection;
using System.Windows.Forms;
using Microsoft.Win32;
using MultiVirtDesk.Shell;
using MultiVirtDesk.Storage;
using MultiVirtDesk.VirtualDesktop;

namespace MultiVirtDesk.UI
{
    public class TrayApplication : ApplicationContext
    {
        private NotifyIcon _notifyIcon;
        private ContextMenuStrip _contextMenu;
        private VirtualDesktopService _vdService;
        private DesktopStorageManager _storage;
        private DesktopInfo _currentDesktop;

        private const string RunRegistryKey = @"Software\Microsoft\Windows\CurrentVersion\Run";
        private const string AppRegistryName = "MultiVirtDesk";

        public TrayApplication(VirtualDesktopService vdService, DesktopStorageManager storage)
        {
            _vdService = vdService;
            _storage = storage;

            InitializeTray();
            HookEvents();

            // Set initial state
            _currentDesktop = _vdService.GetCurrentDesktop();
            if (_currentDesktop != null)
            {
                Guid recorded = _storage.GetActiveDesktopId();
                if (recorded == Guid.Empty)
                {
                    _storage.SetActiveDesktopId(_currentDesktop.Id, _currentDesktop.Name);
                }
                UpdateTrayState(_currentDesktop.Name);
            }
        }

        private void InitializeTray()
        {
            _contextMenu = new ContextMenuStrip();
            _contextMenu.Opening += (s, e) => BuildMenu();

            _notifyIcon = new NotifyIcon
            {
                Text = "MultiVirtDesk",
                Icon = CreateAppIcon(),
                ContextMenuStrip = _contextMenu,
                Visible = true
            };

            _notifyIcon.DoubleClick += (s, e) =>
            {
                // Open vault folder on double-click
                OpenCurrentVault();
            };
        }

        private void HookEvents()
        {
            _vdService.DesktopChanged += OnDesktopChanged;
            _vdService.DesktopNameChanged += OnDesktopNameChanged;
            _vdService.DesktopCreated += (id, name) => UpdateMenu();
            _vdService.DesktopDestroyed += (destId, fallbackId) => UpdateMenu();
        }

        private void OnDesktopChanged(Guid oldId, string oldName, Guid newId, string newName)
        {
            try
            {
                _currentDesktop = _vdService.GetCurrentDesktop();

                // Perform the atomic icon switch
                _storage.SwitchDesktop(oldId, oldName, newId, newName);

                // Update tray UI
                UpdateTrayState(newName);
            }
            catch (Exception ex)
            {
                Console.WriteLine("[MultiVirtDesk] OnDesktopChanged handler error: " + ex.Message);
            }
        }

        private void OnDesktopNameChanged(Guid id, string newName)
        {
            if (_currentDesktop != null && _currentDesktop.Id == id)
            {
                _currentDesktop.Name = newName;
                UpdateTrayState(newName);
            }
            else
            {
                UpdateMenu();
            }
        }

        private void UpdateTrayState(string desktopName)
        {
            if (_notifyIcon == null) return;

            string text = string.Format("MultiVirtDesk: {0}", string.IsNullOrEmpty(desktopName) ? "Active" : desktopName);
            if (text.Length > 63) text = text.Substring(0, 60) + "..."; // NotifyIcon text max 63 chars

            _notifyIcon.Text = text;
            UpdateMenu();
        }

        private void UpdateMenu()
        {
            if (_contextMenu != null && _contextMenu.InvokeRequired)
            {
                _contextMenu.BeginInvoke(new Action(BuildMenu));
            }
            else
            {
                BuildMenu();
            }
        }

        private void BuildMenu()
        {
            _contextMenu.Items.Clear();

            // 1. Header (Active Desktop info)
            string activeName = _currentDesktop != null ? _currentDesktop.Name : "Virtual Desktop";
            ToolStripMenuItem header = new ToolStripMenuItem(string.Format("Workspace: {0}", activeName))
            {
                Enabled = false,
                Font = new Font(SystemFonts.DefaultFont, FontStyle.Bold)
            };
            _contextMenu.Items.Add(header);
            _contextMenu.Items.Add(new ToolStripSeparator());

            // 2. Dynamic Desktop list
            List<DesktopInfo> desktops = _vdService.GetDesktops();
            if (desktops.Count > 0)
            {
                foreach (DesktopInfo desk in desktops)
                {
                    DesktopInfo target = desk;
                    ToolStripMenuItem item = new ToolStripMenuItem(string.Format("{0}. {1}", target.Index + 1, target.Name));
                    if (_currentDesktop != null && target.Id == _currentDesktop.Id)
                    {
                        item.Checked = true;
                    }

                    item.Click += (s, e) =>
                    {
                        _vdService.SwitchToDesktop(target.Id);
                    };

                    _contextMenu.Items.Add(item);
                }
            }
            else
            {
                ToolStripMenuItem none = new ToolStripMenuItem("No virtual desktops detected") { Enabled = false };
                _contextMenu.Items.Add(none);
            }

            _contextMenu.Items.Add(new ToolStripSeparator());

            // 3. Vault & file management
            ToolStripMenuItem openVault = new ToolStripMenuItem("Open Desktop Vault Folder", null, (s, e) => OpenCurrentVault());
            ToolStripMenuItem managePinned = new ToolStripMenuItem("Manage Pinned Icons (All Desktops)...", null, (s, e) => OpenPinnedFile());
            ToolStripMenuItem adoptPublic = new ToolStripMenuItem("Adopt Public Desktop Shortcuts", null, (s, e) => PromptAdoptPublic());
            ToolStripMenuItem refresh = new ToolStripMenuItem("Refresh Desktop Icons", null, (s, e) => ShellHelper.RefreshDesktop(_storage.DesktopPath));

            _contextMenu.Items.Add(openVault);
            _contextMenu.Items.Add(managePinned);
            _contextMenu.Items.Add(adoptPublic);
            _contextMenu.Items.Add(refresh);

            _contextMenu.Items.Add(new ToolStripSeparator());

            // 4. Windows Integration Toggles
            ToolStripMenuItem startupItem = new ToolStripMenuItem("Start with Windows")
            {
                Checked = IsStartupEnabled()
            };
            startupItem.Click += (s, e) => ToggleStartup(!startupItem.Checked);
            _contextMenu.Items.Add(startupItem);

            ToolStripMenuItem contextMenuItem = new ToolStripMenuItem("Explorer Context Menu")
            {
                Checked = ShellContextMenu.IsContextMenuRegistered()
            };
            contextMenuItem.Click += (s, e) => ToggleContextMenu(!contextMenuItem.Checked);
            _contextMenu.Items.Add(contextMenuItem);

            _contextMenu.Items.Add(new ToolStripSeparator());

            // 5. Emergency Failsafe & Exit
            ToolStripMenuItem restoreAll = new ToolStripMenuItem("Restore All Files to Desktop (Failsafe)", null, (s, e) => PromptRestoreAll());
            ToolStripMenuItem exitItem = new ToolStripMenuItem("Exit MultiVirtDesk", null, (s, e) => ExitThread());

            _contextMenu.Items.Add(restoreAll);
            _contextMenu.Items.Add(exitItem);
        }

        private void OpenCurrentVault()
        {
            if (_currentDesktop == null) return;
            string vault = _storage.GetVaultDirectory(_currentDesktop.Id);
            try
            {
                Process.Start("explorer.exe", vault);
            }
            catch { }
        }

        private void OpenPinnedFile()
        {
            string pinnedPath = Path.Combine(_storage.AppDataRoot, "pinned.txt");
            if (!File.Exists(pinnedPath))
            {
                File.WriteAllText(pinnedPath, "# List files or shortcuts here that should appear on ALL desktops (one per line)\n# Example:\n# Recycle Bin.lnk\n# Terminal.lnk\n");
            }
            try
            {
                Process.Start("notepad.exe", pinnedPath);
            }
            catch { }
        }

        private void PromptAdoptPublic()
        {
            DialogResult res = MessageBox.Show(
                "This will move all shared shortcuts from 'C:\\Users\\Public\\Desktop' into your active virtual desktop.\n\n" +
                "This allows you to organize Public shortcuts (like Chrome, Steam, VLC) per virtual desktop rather than having them forced onto every screen.\n\n" +
                "Do you want to proceed?",
                "Adopt Public Desktop Shortcuts",
                MessageBoxButtons.YesNo,
                MessageBoxIcon.Question
            );

            if (res == DialogResult.Yes)
            {
                _storage.AdoptPublicShortcuts();
                MessageBox.Show("Public shortcuts successfully adopted into your active workspace!", "MultiVirtDesk", MessageBoxButtons.OK, MessageBoxIcon.Information);
            }
        }

        private void PromptRestoreAll()
        {
            DialogResult res = MessageBox.Show(
                "Are you sure you want to restore all icons from every virtual workspace back onto your main Desktop?\n\n" +
                "Use this if you are planning to uninstall or want all shortcuts visible together again.",
                "Restore All Desktop Files",
                MessageBoxButtons.YesNo,
                MessageBoxIcon.Warning
            );

            if (res == DialogResult.Yes)
            {
                _storage.RestoreAllToDesktop();
                MessageBox.Show("All files have been restored to your Desktop.", "MultiVirtDesk", MessageBoxButtons.OK, MessageBoxIcon.Information);
            }
        }

        private bool IsStartupEnabled()
        {
            try
            {
                using (RegistryKey key = Registry.CurrentUser.OpenSubKey(RunRegistryKey, false))
                {
                    if (key != null)
                    {
                        return key.GetValue(AppRegistryName) != null;
                    }
                }
            }
            catch { }
            return false;
        }

        private void ToggleStartup(bool enable)
        {
            try
            {
                using (RegistryKey key = Registry.CurrentUser.OpenSubKey(RunRegistryKey, true))
                {
                    if (key != null)
                    {
                        if (enable)
                        {
                            string exePath = Assembly.GetExecutingAssembly().Location;
                            key.SetValue(AppRegistryName, string.Format("\"{0}\" --silent", exePath));
                        }
                        else
                        {
                            key.DeleteValue(AppRegistryName, false);
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show("Failed to change startup setting: " + ex.Message);
            }
            UpdateMenu();
        }

        private void ToggleContextMenu(bool enable)
        {
            string exePath = Assembly.GetExecutingAssembly().Location;
            if (enable)
            {
                ShellContextMenu.RegisterContextMenu(exePath);
            }
            else
            {
                ShellContextMenu.UnregisterContextMenu();
            }
            UpdateMenu();
        }

        private Icon CreateAppIcon()
        {
            // Dynamically generate a clean, modern 32x32 icon of multiple virtual desktops
            using (Bitmap bmp = new Bitmap(32, 32))
            using (Graphics g = Graphics.FromImage(bmp))
            {
                g.Clear(Color.Transparent);
                g.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;

                // Back screen
                using (Pen pen = new Pen(Color.FromArgb(140, 160, 190), 2f))
                using (SolidBrush brush = new SolidBrush(Color.FromArgb(40, 60, 90)))
                {
                    g.FillRectangle(brush, 8, 4, 18, 12);
                    g.DrawRectangle(pen, 8, 4, 18, 12);
                }

                // Front screen (active)
                using (Pen pen = new Pen(Color.FromArgb(0, 150, 255), 2f))
                using (SolidBrush brush = new SolidBrush(Color.FromArgb(20, 30, 45)))
                {
                    g.FillRectangle(brush, 4, 12, 20, 14);
                    g.DrawRectangle(pen, 4, 12, 20, 14);
                }

                // Inner grid dots (representing independent icons)
                using (SolidBrush dotBrush = new SolidBrush(Color.FromArgb(0, 210, 255)))
                {
                    g.FillEllipse(dotBrush, 7, 15, 3, 3);
                    g.FillEllipse(dotBrush, 12, 15, 3, 3);
                    g.FillEllipse(dotBrush, 7, 20, 3, 3);
                }

                return Icon.FromHandle(bmp.GetHicon());
            }
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                if (_notifyIcon != null)
                {
                    _notifyIcon.Visible = false;
                    _notifyIcon.Dispose();
                }
                if (_vdService != null)
                {
                    _vdService.Dispose();
                }
            }
            base.Dispose(disposing);
        }
    }
}
