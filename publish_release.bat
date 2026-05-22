@echo off
echo ==========================================
echo  ConSecOrg - Publish Release Builds
echo ==========================================
echo.

cd /d "%~dp0"

echo [1/3] Publishing Client (WPF)...
dotnet publish src\ConSecOrg.Client -c Release -r win-x64 --self-contained false -o publish\Client
if %errorlevel% neq 0 (
    echo [ERROR] Client publish failed
    pause
    exit /b 1
)
echo    Done: publish\Client\
echo.

echo [2/3] Publishing Server (ASP.NET Core)...
dotnet publish src\ConSecOrg.Server -c Release -r win-x64 --self-contained false -o publish\Server
if %errorlevel% neq 0 (
    echo [ERROR] Server publish failed
    pause
    exit /b 1
)
echo    Done: publish\Server\
echo.

echo [3/3] Copying auxiliary files...
if exist "INSTRUKTSIYA.txt" (
    copy "INSTRUKTSIYA.txt" "publish\INSTRUKTSIYA.txt" > nul
    echo    Copied.
) else (
    echo    INSTRUKTSIYA.txt not found - skipped
)
echo.

echo ==========================================
echo  Publish complete!
echo  Next step: run installer\build_installer.bat
echo ==========================================
echo.
pause
