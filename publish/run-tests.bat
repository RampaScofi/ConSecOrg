@echo off
chcp 65001 > nul
title ConSecOrg — Тесты

echo.
echo  ╔══════════════════════════════════════════╗
echo  ║       ConSecOrg — Запуск тестов          ║
echo  ╚══════════════════════════════════════════╝
echo.
echo  Требуется: .NET Runtime (dotnet.exe в PATH)
echo.

set TESTS_DIR=%~dp0Tests

:: ── ConSecOrg.Infrastructure.Tests ───────────────────────────────────────────
echo  [1/1] ConSecOrg.Infrastructure.Tests
echo  ─────────────────────────────────────────────────────────────────
dotnet vstest "%TESTS_DIR%\ConSecOrg.Infrastructure.Tests.dll" --logger:"console;verbosity=normal"

if %ERRORLEVEL% EQU 0 (
    set RESULT=ПРОШЛИ УСПЕШНО
    set EXITCODE=0
) else (
    set RESULT=ЕСТЬ ОШИБКИ
    set EXITCODE=1
)

echo.
echo  ═════════════════════════════════════════════════════════════════
echo  Результат: %RESULT%
echo  ═════════════════════════════════════════════════════════════════
echo.
pause
exit /b %EXITCODE%
