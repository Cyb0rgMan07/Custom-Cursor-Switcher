@echo off
:: Change working directory to the folder this script lives in.
:: Without this, 'Run as Administrator' silently switches to System32.
cd /d "%~dp0"
setlocal
echo.
echo  ============================================================
echo    CursorSwitcher  -  Build Script
echo    Requires: .NET Framework 4.5+ (already on Windows 10)
echo  ============================================================
echo.

:: ── Locate csc.exe ─────────────────────────────────────────────
set "CSC="
if exist "C:\Windows\Microsoft.NET\Framework64\v4.0.30319\csc.exe" (
    set "CSC=C:\Windows\Microsoft.NET\Framework64\v4.0.30319\csc.exe"
    goto :FOUND_CSC
)
if exist "C:\Windows\Microsoft.NET\Framework\v4.0.30319\csc.exe" (
    set "CSC=C:\Windows\Microsoft.NET\Framework\v4.0.30319\csc.exe"
    goto :FOUND_CSC
)
echo  [ERROR] csc.exe not found.
echo  Please make sure .NET Framework 4.x is installed on this PC.
echo.
pause
exit /b 1

:FOUND_CSC
echo  Compiler : %CSC%
echo.

:: ── Check required files ────────────────────────────────────────
if not exist "CursorSwitcher.cs" (
    echo  [ERROR] CursorSwitcher.cs not found in this folder.
    pause & exit /b 1
)
if not exist "app.manifest" (
    echo  [ERROR] app.manifest not found in this folder.
    pause & exit /b 1
)
if not exist "Cursor.zip" (
    echo  [ERROR] Cursor.zip not found in this folder.
    echo  Please place your Cursor.zip in the same folder as these files.
    pause & exit /b 1
)

:: ── Compile ─────────────────────────────────────────────────────
echo  Compiling CursorSwitcher.exe...
echo.

"%CSC%" ^
  /target:winexe ^
  /optimize+ ^
  /nologo ^
  /win32manifest:app.manifest ^
  /res:Cursor.zip,CursorSwitcher.Cursors ^
  /r:System.Windows.Forms.dll ^
  /r:System.Drawing.dll ^
  /r:System.IO.Compression.dll ^
  /r:System.Core.dll ^
  /out:CursorSwitcher.exe ^
  CursorSwitcher.cs

echo.
if %errorlevel%==0 (
    echo  ============================================================
    echo   SUCCESS!  CursorSwitcher.exe has been created.
    echo.
    echo   NOTE: The exe bundles all cursor files inside itself.
    echo   On first launch it will ask you where to install them.
    echo   Right-click the exe and "Run as administrator" is NOT
    echo   needed  -  it auto-elevates via its UAC manifest.
    echo  ============================================================
) else (
    echo  ============================================================
    echo   BUILD FAILED.  See the errors listed above.
    echo  ============================================================
)
echo.
pause
