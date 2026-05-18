@echo off
title ConSecOrg - Server Integration Tests
cd /d "%~dp0"

echo.
echo ============================================================
echo  ConSecOrg - Server Integration Tests (22 теста)
echo  WebApplicationFactory, InMemory DB, JWT, GOST crypto
echo ============================================================
echo.
echo  Покрытые контроллеры:
echo    AuthController      - register, login, /me, change-password
echo    AuditController     - logs, verify chain, CSV export, PDF export
echo    NotesController     - CRUD, security levels, RBAC
echo    TasksController     - CRUD, board move
echo    ContactsController  - CRUD контактов
echo    UsersController     - Admin: список, lock/unlock, role
echo ============================================================
echo.

dotnet test tests\ConSecOrg.Server.Tests ^
    --configuration Debug ^
    --logger "console;verbosity=detailed" ^
    --nologo

if %ERRORLEVEL% EQU 0 goto PASSED

echo.
echo ============================================================
echo  РЕЗУЛЬТАТ: ТЕСТЫ НЕ ПРОШЛИ - см. вывод выше
echo ============================================================
echo.
pause
exit /b 1

:PASSED
echo.
echo ============================================================
echo  РЕЗУЛЬТАТ: ВСЕ 22 SERVER ТЕСТА ПРОШЛИ УСПЕШНО
echo ============================================================
echo.
pause
exit /b 0
