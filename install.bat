@echo off
setlocal
echo ===================================================
echo MultiVirtDesk - Native Windows Installation
echo ===================================================

cd /d "%~dp0"

if not exist "MultiVirtDesk.exe" (
    echo Building MultiVirtDesk...
    call build.bat
    if %ERRORLEVEL% NEQ 0 (
        echo Build failed. Exiting.
        pause
        exit /b %ERRORLEVEL%
    )
)

echo.
echo [1/3] Enabling Windows Explorer Right-Click Context Menu...
MultiVirtDesk.exe --install-context-menu

echo [2/3] Enabling Automatic Launch at Windows Startup...
MultiVirtDesk.exe --install-startup

echo [3/3] Starting MultiVirtDesk Background Daemon...
start "" "%~dp0MultiVirtDesk.exe"

echo.
echo ===================================================
echo MultiVirtDesk is now running and fully integrated!
echo.
echo - Look for the MultiVirtDesk icon in your System Tray (near the clock).
echo - Try switching virtual desktops (Ctrl + Win + Left/Right).
echo - Rename your virtual desktops in Windows Task View (Win + Tab).
echo - Right-click any desktop file to "Pin to All Desktops" or "Move to Virtual Desktop...".
echo ===================================================
pause
