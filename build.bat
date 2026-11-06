@echo off
setlocal
echo ===================================================
echo Building MultiVirtDesk (Native Windows Executable)
echo ===================================================

set CSC_PATH=C:\Windows\Microsoft.NET\Framework64\v4.0.30319\csc.exe

if not exist "%CSC_PATH%" (
    echo Error: csc.exe not found at %CSC_PATH%
    exit /b 1
)

set SOURCES=src\Program.cs src\VirtualDesktop\ComInterfaces.cs src\VirtualDesktop\VirtualDesktopService.cs src\Storage\DesktopStorageManager.cs src\Shell\ShellHelper.cs src\Shell\ShellContextMenu.cs src\UI\TrayApplication.cs src\UI\MoveDialog.cs
set REFS=/r:System.dll /r:System.Windows.Forms.dll /r:System.Drawing.dll

echo [1/2] Compiling MultiVirtDesk.exe (Background Tray Engine)...
"%CSC_PATH%" /target:winexe /platform:x64 /optimize+ /out:MultiVirtDesk.exe %REFS% %SOURCES%
if %ERRORLEVEL% NEQ 0 exit /b %ERRORLEVEL%

echo [2/2] Compiling MultiVirtDesk-cli.exe (Command Line Interface)...
"%CSC_PATH%" /target:exe /platform:x64 /optimize+ /out:MultiVirtDesk-cli.exe %REFS% %SOURCES%
if %ERRORLEVEL% NEQ 0 exit /b %ERRORLEVEL%

echo.
echo ===================================================
echo BUILD SUCCESSFUL:
echo   - MultiVirtDesk.exe     (36 KB - Silent Background Daemon)
echo   - MultiVirtDesk-cli.exe (36 KB - Console Tool)
echo ===================================================
