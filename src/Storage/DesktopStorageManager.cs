using System;
using System.Collections.Generic;
using System.IO;
using MultiVirtDesk.Shell;

namespace MultiVirtDesk.Storage
{
    public class DesktopStorageManager
    {
        private readonly string _desktopPath;
        private readonly string _appDataRoot;
        private readonly string _vaultsRoot;
        private readonly string _pinnedFilePath;
        private readonly string _activeFilePath;
        private readonly string _metadataFilePath;
        private readonly object _swapLock = new object();

        private HashSet<string> _pinnedItems;

        public string DesktopPath { get { return _desktopPath; } }
        public string AppDataRoot { get { return _appDataRoot; } }
        public string VaultsRoot { get { return _vaultsRoot; } }

        public DesktopStorageManager()
        {
            _desktopPath = Environment.GetFolderPath(Environment.SpecialFolder.Desktop);
            _appDataRoot = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "MultiVirtDesk");
            _vaultsRoot = Path.Combine(_appDataRoot, "Desktops");
            _pinnedFilePath = Path.Combine(_appDataRoot, "pinned.txt");
            _activeFilePath = Path.Combine(_appDataRoot, "active_desktop.txt");
            _metadataFilePath = Path.Combine(_appDataRoot, "desktops.ini");

            EnsureDirectories();
            LoadPinnedItems();
        }

        private void EnsureDirectories()
        {
            if (!Directory.Exists(_appDataRoot)) Directory.CreateDirectory(_appDataRoot);
            if (!Directory.Exists(_vaultsRoot)) Directory.CreateDirectory(_vaultsRoot);
        }

        public string GetVaultDirectory(Guid desktopId)
        {
            string path = Path.Combine(_vaultsRoot, desktopId.ToString("B"));
            if (!Directory.Exists(path)) Directory.CreateDirectory(path);
            return path;
        }

        public Guid GetActiveDesktopId()
        {
            try
            {
                if (File.Exists(_activeFilePath))
                {
                    string text = File.ReadAllText(_activeFilePath).Trim();
                    if (!string.IsNullOrEmpty(text))
                    {
                        string[] parts = text.Split('|');
                        Guid id;
                        if (Guid.TryParse(parts[0], out id))
                        {
                            return id;
                        }
                    }
                }
            }
            catch { }
            return Guid.Empty;
        }

        public void SetActiveDesktopId(Guid id, string name)
        {
            try
            {
                File.WriteAllText(_activeFilePath, id.ToString() + "|" + (name ?? ""));
            }
            catch { }
        }

        public void LoadPinnedItems()
        {
            _pinnedItems = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            try
            {
                if (File.Exists(_pinnedFilePath))
                {
                    string[] lines = File.ReadAllLines(_pinnedFilePath);
                    foreach (string line in lines)
                    {
                        string trimmed = line.Trim();
                        if (!string.IsNullOrEmpty(trimmed) && !trimmed.StartsWith("#"))
                        {
                            _pinnedItems.Add(trimmed);
                        }
                    }
                }
            }
            catch { }
        }

        public void SavePinnedItems()
        {
            try
            {
                List<string> lines = new List<string>(_pinnedItems);
                File.WriteAllLines(_pinnedFilePath, lines.ToArray());
            }
            catch { }
        }

        public bool IsItemPinned(string filename)
        {
            if (string.IsNullOrEmpty(filename)) return false;
            return _pinnedItems.Contains(filename);
        }

        public bool PinItem(string filenameOrPath)
        {
            string name = Path.GetFileName(filenameOrPath);
            if (string.IsNullOrEmpty(name)) return false;

            lock (_swapLock)
            {
                if (_pinnedItems.Add(name))
                {
                    SavePinnedItems();
                    return true;
                }
            }
            return false;
        }

        public bool UnpinItem(string filenameOrPath)
        {
            string name = Path.GetFileName(filenameOrPath);
            if (string.IsNullOrEmpty(name)) return false;

            lock (_swapLock)
            {
                if (_pinnedItems.Remove(name))
                {
                    SavePinnedItems();
                    return true;
                }
            }
            return false;
        }

        public List<string> GetPinnedItems()
        {
            lock (_swapLock)
            {
                return new List<string>(_pinnedItems);
            }
        }

        public void SwitchDesktop(Guid oldId, string oldName, Guid newId, string newName)
        {
            lock (_swapLock)
            {
                if (oldId == newId && oldId != Guid.Empty) return;

                Guid currentRecorded = GetActiveDesktopId();
                if (oldId == Guid.Empty && currentRecorded != Guid.Empty)
                {
                    oldId = currentRecorded;
                }

                // 0. Save outgoing icon positions before moving files
                if (oldId != Guid.Empty)
                {
                    string oldVault = GetVaultDirectory(oldId);
                    IconLayoutManager.SavePositions(oldVault);
                }

                // 1. Stash outgoing items
                if (oldId != Guid.Empty && Directory.Exists(_desktopPath))
                {
                    string oldVault = GetVaultDirectory(oldId);

                    // Move files
                    string[] files = Directory.GetFiles(_desktopPath);
                    foreach (string file in files)
                    {
                        string name = Path.GetFileName(file);
                        if (string.Equals(name, "desktop.ini", StringComparison.OrdinalIgnoreCase)) continue;
                        if (IsItemPinned(name)) continue;

                        string dest = Path.Combine(oldVault, name);
                        ShellHelper.MoveFileEx(file, dest, ShellHelper.MOVEFILE_REPLACE_EXISTING | ShellHelper.MOVEFILE_WRITE_THROUGH);
                    }

                    // Move directories
                    string[] dirs = Directory.GetDirectories(_desktopPath);
                    foreach (string dir in dirs)
                    {
                        string name = Path.GetFileName(dir);
                        if (IsItemPinned(name)) continue;

                        string dest = Path.Combine(oldVault, name);
                        try
                        {
                            if (Directory.Exists(dest)) Directory.Delete(dest, true);
                            Directory.Move(dir, dest);
                        }
                        catch { }
                    }
                }

                // 2. Unstash incoming items
                if (newId != Guid.Empty)
                {
                    string newVault = GetVaultDirectory(newId);
                    if (Directory.Exists(newVault))
                    {
                        // Move files
                        string[] files = Directory.GetFiles(newVault);
                        foreach (string file in files)
                        {
                            string name = Path.GetFileName(file);
                            if (string.Equals(name, "desktop.ini", StringComparison.OrdinalIgnoreCase)) continue;

                            string dest = Path.Combine(_desktopPath, name);
                            ShellHelper.MoveFileEx(file, dest, ShellHelper.MOVEFILE_REPLACE_EXISTING | ShellHelper.MOVEFILE_WRITE_THROUGH);
                        }

                        // Move directories
                        string[] dirs = Directory.GetDirectories(newVault);
                        foreach (string dir in dirs)
                        {
                            string name = Path.GetFileName(dir);
                            string dest = Path.Combine(_desktopPath, name);
                            try
                            {
                                if (Directory.Exists(dest)) Directory.Delete(dest, true);
                                Directory.Move(dir, dest);
                            }
                            catch { }
                        }
                    }
                }

                // 3. Record active desktop
                SetActiveDesktopId(newId, newName);

                // 4. Instant Explorer view update
                ShellHelper.RefreshDesktop(_desktopPath);

                // 5. Restore incoming icon positions
                if (newId != Guid.Empty)
                {
                    string newVault = GetVaultDirectory(newId);
                    IconLayoutManager.RestorePositions(newVault);
                }
            }
        }

        public void MoveItemToDesktop(string itemPath, Guid targetDesktopId)
        {
            lock (_swapLock)
            {
                if (!File.Exists(itemPath) && !Directory.Exists(itemPath)) return;
                string filename = Path.GetFileName(itemPath);
                if (string.Equals(filename, "desktop.ini", StringComparison.OrdinalIgnoreCase)) return;

                Guid currentId = GetActiveDesktopId();
                if (currentId == targetDesktopId) return; // Already on this desktop

                string targetVault = GetVaultDirectory(targetDesktopId);
                string targetPath = Path.Combine(targetVault, filename);

                if (File.Exists(itemPath))
                {
                    ShellHelper.MoveFileEx(itemPath, targetPath, ShellHelper.MOVEFILE_REPLACE_EXISTING | ShellHelper.MOVEFILE_WRITE_THROUGH);
                }
                else if (Directory.Exists(itemPath))
                {
                    if (Directory.Exists(targetPath)) Directory.Delete(targetPath, true);
                    Directory.Move(itemPath, targetPath);
                }

                ShellHelper.RefreshDesktop(_desktopPath);
            }
        }

        public void RestoreAllToDesktop()
        {
            lock (_swapLock)
            {
                if (!Directory.Exists(_vaultsRoot)) return;

                string[] vaultDirs = Directory.GetDirectories(_vaultsRoot);
                foreach (string vault in vaultDirs)
                {
                    string vaultName = Path.GetFileName(vault);

                    // Move all files back
                    string[] files = Directory.GetFiles(vault);
                    foreach (string file in files)
                    {
                        string name = Path.GetFileName(file);
                        if (string.Equals(name, "desktop.ini", StringComparison.OrdinalIgnoreCase)) continue;

                        string dest = Path.Combine(_desktopPath, name);
                        if (File.Exists(dest))
                        {
                            string ext = Path.GetExtension(name);
                            string baseName = Path.GetFileNameWithoutExtension(name);
                            dest = Path.Combine(_desktopPath, string.Format("{0} (from {1}){2}", baseName, vaultName, ext));
                        }
                        ShellHelper.MoveFileEx(file, dest, ShellHelper.MOVEFILE_REPLACE_EXISTING | ShellHelper.MOVEFILE_WRITE_THROUGH);
                    }

                    // Move all directories back
                    string[] dirs = Directory.GetDirectories(vault);
                    foreach (string dir in dirs)
                    {
                        string name = Path.GetFileName(dir);
                        string dest = Path.Combine(_desktopPath, name);
                        if (Directory.Exists(dest))
                        {
                            dest = Path.Combine(_desktopPath, string.Format("{0} (from {1})", name, vaultName));
                        }
                        try { Directory.Move(dir, dest); } catch { }
                    }
                }

                try
                {
                    if (File.Exists(_activeFilePath)) File.Delete(_activeFilePath);
                }
                catch { }

                ShellHelper.RefreshDesktop(_desktopPath);
            }
        }

        public void AdoptPublicShortcuts()
        {
            lock (_swapLock)
            {
                string publicDesktop = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.CommonDesktopDirectory));
                if (!Directory.Exists(publicDesktop)) return;

                string[] files = Directory.GetFiles(publicDesktop);
                foreach (string file in files)
                {
                    string name = Path.GetFileName(file);
                    if (string.Equals(name, "desktop.ini", StringComparison.OrdinalIgnoreCase)) continue;

                    string dest = Path.Combine(_desktopPath, name);
                    try
                    {
                        // Copy to user desktop, then delete from public
                        File.Copy(file, dest, true);
                        File.Delete(file);
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine("[MultiVirtDesk] Could not adopt public shortcut " + name + ": " + ex.Message);
                    }
                }

                ShellHelper.RefreshDesktop(_desktopPath);
                ShellHelper.RefreshDesktop(publicDesktop);
            }
        }
    }
}
