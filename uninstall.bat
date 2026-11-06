@echo off
setlocal
echo ===================================================
echo MultiVirtDesk - Safe Uninstallation & Rollback
echo ===================================================

cd /d "%~dp0"

echo [1/4] Stopping MultiVirtDesk background process...
taskkill /F /IM MultiVirtDesk.exe >nul 2>&1

echo [2/4] Restoring all icons from all virtual workspaces back to your Desktop...
MultiVirtDesk-cli.exe --restore-all

echo [3/4] Removing Windows Explorer context menus...
MultiVirtDesk.exe --uninstall-context-menu

echo [4/4] Removing from Windows Startup...
MultiVirtDesk.exe --uninstall-startup

echo.
echo ===================================================
echo MultiVirtDesk has been safely uninstalled!
echo All your files and shortcuts are back on your main desktop.
echo ===================================================
pause
