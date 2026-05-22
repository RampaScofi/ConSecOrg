@echo off
echo ==========================================
echo  ConSecOrg - Build Windows Installer
echo ==========================================
echo.

set ISS_COMPILER=
if exist "C:\Program Files (x86)\Inno Setup 6\iscc.exe" (
    set ISS_COMPILER="C:\Program Files (x86)\Inno Setup 6\iscc.exe"
) else if exist "C:\Program Files\Inno Setup 6\iscc.exe" (
    set ISS_COMPILER="C:\Program Files\Inno Setup 6\iscc.exe"
) else (
    echo [ERROR] Inno Setup 6 not found!
    echo.
    echo Download and install from: https://jrsoftware.org/isdl.php
    echo.
    pause
    exit /b 1
)

if not exist "..\publish\Client" (
    echo [ERROR] Folder publish\Client not found!
    echo.
    echo Run publish_release.bat first.
    echo.
    pause
    exit /b 1
)

if not exist "..\publish\Server" (
    echo [WARNING] Folder publish\Server not found - server component will be skipped
    echo.
)

if not exist "ConSecOrg_Icon.ico" (
    echo [WARNING] ConSecOrg_Icon.ico not found - default icon will be used
    echo.
)

echo Compiling installer...
echo.
%ISS_COMPILER% ConSecOrg_Setup.iss

if %errorlevel% == 0 (
    echo.
    echo ==========================================
    echo  Installer created successfully!
    echo  File: installer\Output\ConSecOrg_Setup_v1.0.0.exe
    echo ==========================================
) else (
    echo.
    echo [ERROR] Build failed with error code %errorlevel%
)

echo.
pause
