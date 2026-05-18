@echo off
chcp 65001 > nul
title ConSecOrg - Testy

echo.
echo  ==========================================
echo   ConSecOrg -- Zapusk testov
echo  ==========================================
echo.
echo  Trebuetsya: .NET Runtime (dotnet.exe v PATH)
echo.

set TESTS_DIR=%~dp0Tests

echo  [1/1] ConSecOrg.Infrastructure.Tests
echo  ------------------------------------------
echo.

dotnet vstest "%TESTS_DIR%\ConSecOrg.Infrastructure.Tests.dll" --logger:"console;verbosity=normal"

if %ERRORLEVEL% EQU 0 (
    echo.
    echo  ==========================================
    echo   REZULTAT: VSE TESTY PROSHLI USPESHNO
    echo  ==========================================
) else (
    echo.
    echo  ==========================================
    echo   REZULTAT: EST OSHIBKI - smotrите vыshe
    echo  ==========================================
)

echo.
pause
