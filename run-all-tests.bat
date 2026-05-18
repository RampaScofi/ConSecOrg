@echo off
title ConSecOrg - All Tests
cd /d "%~dp0"

set FAILED=0

echo.
echo ============================================================
echo  ConSecOrg - Запуск ВСЕХ тестов
echo  Domain(1) + Application(10) + Infrastructure(31) + Server(22)
echo ============================================================

echo.
echo [1/4] Domain.Tests...
dotnet test tests\ConSecOrg.Domain.Tests --configuration Debug --logger "console;verbosity=minimal" --nologo
if %ERRORLEVEL% NEQ 0 set FAILED=1

echo.
echo [2/4] Application.Tests...
dotnet test tests\ConSecOrg.Application.Tests --configuration Debug --logger "console;verbosity=minimal" --nologo
if %ERRORLEVEL% NEQ 0 set FAILED=1

echo.
echo [3/4] Infrastructure.Tests...
dotnet test tests\ConSecOrg.Infrastructure.Tests --configuration Debug --logger "console;verbosity=minimal" --nologo
if %ERRORLEVEL% NEQ 0 set FAILED=1

echo.
echo [4/4] Server.Tests (integration)...
dotnet test tests\ConSecOrg.Server.Tests --configuration Debug --logger "console;verbosity=minimal" --nologo
if %ERRORLEVEL% NEQ 0 set FAILED=1

echo.
if %FAILED% EQU 0 goto PASSED

echo ============================================================
echo  РЕЗУЛЬТАТ: ЕСТЬ УПАВШИЕ ТЕСТЫ - см. вывод выше
echo ============================================================
echo.
pause
exit /b 1

:PASSED
echo ============================================================
echo  РЕЗУЛЬТАТ: ВСЕ 64 ТЕСТА ПРОШЛИ УСПЕШНО
echo ============================================================
echo.
pause
exit /b 0
