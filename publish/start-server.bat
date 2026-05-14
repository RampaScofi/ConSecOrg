@echo off
chcp 65001 > nul
title ConSecOrg Server
cd /d "%~dp0Server"

:: ── Генерация JWT-секрета при первом запуске ─────────────────────────────────
powershell -ExecutionPolicy Bypass -Command ^
  "$f='appsettings.json';" ^
  "$json=Get-Content $f -Raw;" ^
  "$j=$json|ConvertFrom-Json;" ^
  "if([string]::IsNullOrWhiteSpace($j.JwtSettings.Secret)){" ^
  "  $rng=[System.Security.Cryptography.RandomNumberGenerator]::Create();" ^
  "  $bytes=New-Object byte[] 48;" ^
  "  $rng.GetBytes($bytes);" ^
  "  $secret=[Convert]::ToBase64String($bytes);" ^
  "  $repl='\"Secret\": \"'+$secret+'\"';" ^
  "  $json=$json -replace '\"Secret\":\s*\"\"',$repl;" ^
  "  [System.IO.File]::WriteAllText((Resolve-Path $f),$json,[System.Text.Encoding]::UTF8);" ^
  "  Write-Host '  JWT-секрет сгенерирован и сохранён.' -ForegroundColor Green" ^
  "} else {" ^
  "  Write-Host '  JWT-секрет уже настроен.' -ForegroundColor Cyan" ^
  "}"

echo.
echo ============================================
echo   ConSecOrg Server
echo   http://localhost:5205
echo   http://localhost:5205/swagger
echo ============================================
echo.
ConSecOrg.Server.exe
pause
