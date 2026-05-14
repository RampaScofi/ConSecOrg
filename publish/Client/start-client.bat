@echo off
title ConSecOrg Client
cd /d "%~dp0"
ConSecOrg.Client.exe
if %errorlevel% neq 0 (
    echo.
    echo === Клиент завершился с ошибкой (код %errorlevel%) ===
    pause
)
