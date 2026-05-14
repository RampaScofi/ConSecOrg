@echo off
chcp 65001 > nul
title ConSecOrg Server
echo ============================================
echo   ConSecOrg Server — запуск...
echo   http://localhost:5205
echo   http://localhost:5205/swagger
echo ============================================
cd /d "%~dp0Server"
ConSecOrg.Server.exe
pause
