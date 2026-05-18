@echo off
title ConSecOrg Tests

echo.
echo ==========================================
echo  ConSecOrg - Infrastructure Tests
echo ==========================================
echo.

dotnet vstest "%~dp0Tests\ConSecOrg.Infrastructure.Tests.dll" --logger:"console;verbosity=normal"

if %ERRORLEVEL% EQU 0 goto PASSED

echo.
echo ==========================================
echo  RESULT: SOME TESTS FAILED - see above
echo ==========================================
echo.
pause
exit /b 1

:PASSED
echo.
echo ==========================================
echo  RESULT: ALL TESTS PASSED
echo ==========================================
echo.
pause
exit /b 0
