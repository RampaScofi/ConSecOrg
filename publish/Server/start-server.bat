@echo off
title ConSecOrg Server
cd /d "%~dp0"
echo ============================================================
echo  ConSecOrg Server — Защищённый электронный органайзер
echo  http://localhost:5205
echo ============================================================
echo.
ConSecOrg.Server.exe
echo.
echo === Сервер завершил работу ===
pause
