# MultiVirtDesk

- Inspired by [MScholtes' VirtualDesktop](https://github.com/MScholtes/VirtualDesktop)

**MultiVirtDesk** brings true workspace isolation to Windows 10 and Windows 11. It gives every Virtual Desktop its own distinct set of desktop icons, shortcuts, and folders, integrating natively into Windows with near-zero resource usage and zero code modifications required for customization.

---

## Key Features

- **Unique Icons per Virtual Desktop**: Dedicate one workspace to **Studying** (AI tools, assignment documents, research papers), another to **Gaming** (Steam, game shortcuts, Discord), and another to **Development**—all with completely different desktop icons and files.
- **100% Dynamic & Zero Hardcoding**: Create, name, or rename desktops in Windows Task View (`Win + Tab`) at any time. MultiVirtDesk automatically detects the names (e.g. *"Studying"*, *"Gaming"*, *"Work"*) and synchronizes their storage vaults automatically.
- **0% Idle CPU & Low Footprint**: Uses Windows internal COM push-event notifications (`IVirtualDesktopNotification`). Sleeps waiting on Windows shell events. No background polling, no battery drain, and uses under 15 MB of RAM.
- **Instant NTFS Metadata Swaps**: File swapping uses atomic NTFS metadata relocation (`MoveFileExW`). 50 desktop icons swap in less than 10 milliseconds, synchronized with the Windows virtual desktop slide animation.
- **Native Explorer Context Menu**: Right-click any file or shortcut on the desktop to:
  - **Pin to All Desktops**: For universal items (Recycle Bin, Browser, Terminal) that should stay visible across every workspace.
  - **Move to Virtual Desktop...**: Instantly send a shortcut to another virtual desktop without switching spaces.
- **System Tray Integration**: A sleek system tray icon next to the taskbar clock displays your current active workspace name and lets you switch spaces, open vaults, or manage settings with one click.
- **Failsafe & Non-Destructive**: All files remain standard user files on your drive. At any time, one click on *"Restore All Files to Desktop"* moves all shortcuts from every workspace back to your main desktop.

---

## Quick Start

### 1. Build and Install
Run the provided installer script:
```cmd
install.bat
```
This will:
1. Compile the native executables using the built-in Windows C# compiler (`csc.exe`).
2. Register the Windows Explorer right-click context menu.
3. Add MultiVirtDesk to Windows Startup (launches silently upon boot).
4. Start the background tray daemon.

### 2. Verification
Check the status of your current virtual workspaces:
```cmd
MultiVirtDesk-cli.exe --status
```
Example output:
```text
=== MultiVirtDesk Status ===
Active Desktop: Desktop 2 (c8ae54a7-d3ad-4b3c-a062-0820f0b43ad7)

All Desktops:
  [1] Studying (ID: 97967cc9-906d-44ca-b54b-503903c7e36c)
  [2] Gaming (ID: c8ae54a7-d3ad-4b3c-a062-0820f0b43ad7)
  [3] Development (ID: c9d33f6f-eb43-4482-b1df-462e5454a2e7)

Pinned Items (Visible on all desktops):
  - Recycle Bin.lnk
============================
```

---

## How to Use

### Creating & Customizing Desktops
1. Press `Win + Tab` to open Windows Task View.
2. Click **New desktop** or click on an existing desktop name to rename it (e.g., to *"Studying"*, *"Gaming"*, or *"Music"*).
3. Switch to that desktop (`Ctrl + Win + Left/Right`).
4. Place the icons, files, and shortcuts you want for that workspace onto your desktop.
5. Switch to your other desktop—the icons for that space will automatically appear, while the previous workspace's icons are safely stashed away!

### Pinning Items to All Desktops
If there are shortcuts you want visible on every virtual desktop (such as the Recycle Bin, a browser, or a communication tool):
- **Method 1**: Right-click the desktop file $\rightarrow$ **MultiVirtDesk** $\rightarrow$ **Pin to All Desktops**.
- **Method 2**: Right-click the system tray icon $\rightarrow$ **Manage Pinned Icons...** and add the filename to `pinned.txt`.
- **Method 3**: Run:
  ```cmd
  MultiVirtDesk-cli.exe --pin "MyShortcut.lnk"
  ```

### Moving Icons Between Desktops
To send an icon directly to another virtual workspace:
- Right-click the file $\rightarrow$ **MultiVirtDesk** $\rightarrow$ **Move to Virtual Desktop...**
- Select the target workspace from the dialog and click **Move**.

### Adopting Public Desktop Shortcuts
By default, Windows places many app installers (e.g., Chrome, Steam, VLC) in the shared `C:\Users\Public\Desktop` folder, forcing them to show across all virtual desktops.
- To organize these per desktop, click the MultiVirtDesk tray icon $\rightarrow$ **Adopt Public Desktop Shortcuts**.
- This moves public shortcuts into your current workspace profile so you can organize or remove them on specific desktops.

---

## Architecture & How It Works

```
Windows Shell (Explorer.exe)
      │
      │ (IVirtualDesktopNotification - Event-Driven Callback)
      ▼
MultiVirtDesk Daemon
      │
      ├─► 1. Detect switch event (old desktop ID -> new desktop ID)
      │
      ├─► 2. Stash outgoing icons:
      │      %USERPROFILE%\Desktop  ─►  %LOCALAPPDATA%\MultiVirtDesk\Desktops\<OldID>\
      │
      ├─► 3. Unstash incoming icons:
      │      %LOCALAPPDATA%\MultiVirtDesk\Desktops\<NewID>\  ─►  %USERPROFILE%\Desktop
      │
      └─► 4. Signal Explorer to refresh view:
             SHChangeNotify(SHCNE_UPDATEDIR, ...) + PostMessage(SHELLDLL_DefView, WM_COMMAND, 0x7103)
```

### Storage Locations
- **Active Desktop**: `%USERPROFILE%\Desktop`
- **Workspaces Vault**: `%LOCALAPPDATA%\MultiVirtDesk\Desktops\<Desktop_GUID>\`
- **Pinned Items List**: `%LOCALAPPDATA%\MultiVirtDesk\pinned.txt`
- **Configuration & Active ID**: `%LOCALAPPDATA%\MultiVirtDesk\active_desktop.txt`

---

## Multi-User & Shared Device Support

If multiple people share the same computer with separate Windows accounts:

### 1. 100% Privacy & Data Isolation
MultiVirtDesk is engineered with strict per-user boundaries:
- **Separate Vaults**: Files are stored inside each user's personal profile (`C:\Users\<Username>\AppData\Local\MultiVirtDesk\`). User B can never see, modify, or accidentally touch User A's virtual desktop files.
- **Independent Configurations**: Each user defines their own desktop names (e.g. User A has *"Studying"* & *"Gaming"*; User B can have *"Coding"* & *"Editing"*).
- **Independent Pinned Items**: Pinning an icon only applies to that specific user's account.

### 2. Does Another User Have to Install It?
- **By Default (Per-User Install)**:
  `install.bat` registers MultiVirtDesk in `HKEY_CURRENT_USER` (requiring zero administrator privileges). This means **another user will NOT have the tool running on their account unless they want it**. Their Windows desktop will continue to behave normally.
- **If Another User Wants It Too**:
  They simply log in to their Windows account, navigate to the folder, and run `install.bat`. MultiVirtDesk will immediately set up their private vaults and context menus for their profile.

### 3. Setting Up MultiVirtDesk Globally for All Users (Optional)
If you want MultiVirtDesk to automatically run for every user on the PC:
1. Place the `MultiVirtDesk` folder in a shared directory (e.g., `C:\Program Files\MultiVirtDesk` or `C:\Tools\MultiVirtDesk`).
2. Press `Win + R`, type `shell:common startup`, and press Enter.
3. Create a shortcut to `MultiVirtDesk.exe` inside that folder.

Whenever any user logs in, MultiVirtDesk will launch automatically, dynamically detect who is logged in, and isolate that user's personal desktop files completely.

---

## Command Line Interface (CLI)

You can manage MultiVirtDesk directly from PowerShell or Command Prompt:

| Command | Description |
| :--- | :--- |
| `MultiVirtDesk-cli.exe --status` | Shows the active desktop, list of all workspaces, and pinned items |
| `MultiVirtDesk-cli.exe --pin "<filename>"` | Pins an item so it appears on all virtual desktops |
| `MultiVirtDesk-cli.exe --unpin "<filename>"` | Unpins an item |
| `MultiVirtDesk-cli.exe --dialog-move "<file>"` | Opens the interactive desktop destination picker |
| `MultiVirtDesk-cli.exe --adopt-public` | Adopts shared shortcuts from `C:\Users\Public\Desktop` |
| `MultiVirtDesk-cli.exe --restore-all` | Failsafe: Restores all icons from all workspaces back to Desktop |
| `MultiVirtDesk.exe --install-startup` | Adds MultiVirtDesk to Windows Startup |
| `MultiVirtDesk.exe --uninstall-startup` | Removes MultiVirtDesk from Windows Startup |
| `MultiVirtDesk.exe --install-context-menu` | Registers Explorer right-click context menu |
| `MultiVirtDesk.exe --uninstall-context-menu` | Unregisters Explorer context menu |

---

## Safe Uninstallation & Rollback

To uninstall and revert your system at any time:
```cmd
uninstall.bat
```
This stops the background daemon, restores all shortcuts from all virtual workspaces back to your main Desktop, and removes the startup and context menu registry entries.

---

##100% AI-Generated project. I made this for myself to use. I have reviewed the code and found it satisfactory for daily use. If you do not trust the code, please do not use the project. Thanks :D