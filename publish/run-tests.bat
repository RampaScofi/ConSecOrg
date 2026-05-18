@echo off
title ConSecOrg Tests

set TESTS=%~dp0Tests

echo.
echo ==========================================
echo  ConSecOrg - Running all tests (41 total)
echo ==========================================
echo.

dotnet vstest "%TESTS%\ConSecOrg.Infrastructure.Tests.dll" "%TESTS%\ConSecOrg.Application.Tests.dll" --logger:"console;verbosity=normal"

if %ERRORLEVEL% EQU 0 goto ALLPASSED

echo.
echo ==========================================
echo  RESULT: SOME TESTS FAILED - see above
echo ==========================================
echo.
pause
exit /b 1

:ALLPASSED
echo.
echo ==========================================
echo  RESULT: ALL 41 TESTS PASSED
echo ==========================================
echo.
pause
exit /b 0
