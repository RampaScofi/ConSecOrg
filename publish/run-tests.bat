@echo off
title ConSecOrg Tests

set TESTS=%~dp0Tests
set PASS=0
set FAIL=0

echo.
echo ==========================================
echo  ConSecOrg - Running all tests (41 total)
echo ==========================================
echo.

echo [1/2] Infrastructure.Tests (Crypto + HashChain)
echo --------------------------------------------------
dotnet vstest "%TESTS%\ConSecOrg.Infrastructure.Tests.dll" --logger:"console;verbosity=normal"
if %ERRORLEVEL% EQU 0 (set /a PASS+=1) else (set /a FAIL+=1)

echo.
echo [2/2] Application.Tests (LoginCommandHandler)
echo --------------------------------------------------
dotnet vstest "%TESTS%\ConSecOrg.Application.Tests.dll" --logger:"console;verbosity=normal"
if %ERRORLEVEL% EQU 0 (set /a PASS+=1) else (set /a FAIL+=1)

echo.
echo ==========================================
if %FAIL% EQU 0 goto ALLPASSED

echo  RESULT: FAILED suites=%FAIL% passed=%PASS%
echo ==========================================
echo.
pause
exit /b 1

:ALLPASSED
echo  RESULT: ALL TESTS PASSED (suites=%PASS%)
echo ==========================================
echo.
pause
exit /b 0
