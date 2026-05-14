@echo off
chcp 65001 > nul
title ConSecOrg — Создание администратора

echo.
echo  Создание первого администратора...
echo  (сервер должен быть запущен на localhost:5205)
echo.

powershell -ExecutionPolicy Bypass -Command ^
  "try {" ^
  "  $r = Invoke-RestMethod -Uri 'http://localhost:5205/api/v1/auth/bootstrap'" ^
  "    -Method POST -ContentType 'application/json'" ^
  "    -Body '{\"username\":\"admin\",\"email\":\"admin@company.ru\",\"password\":\"Admin1234!@#$\",\"roleId\":\"\"}';" ^
  "  Write-Host '  Администратор создан!' -ForegroundColor Green;" ^
  "  Write-Host \"  Логин:  admin\" -ForegroundColor Cyan;" ^
  "  Write-Host \"  Пароль: Admin1234!@#$\" -ForegroundColor Cyan;" ^
  "  Start-Process 'http://localhost:5205/swagger'" ^
  "} catch {" ^
  "  $code = $_.Exception.Response.StatusCode.value__;" ^
  "  if ($code -eq 409) {" ^
  "    Write-Host '  Пользователи уже существуют — повторное создание не требуется.' -ForegroundColor Yellow" ^
  "  } elseif ($code -eq $null) {" ^
  "    Write-Host '  Ошибка: сервер недоступен. Запустите start-server.bat.' -ForegroundColor Red" ^
  "  } else {" ^
  "    Write-Host \"  Ошибка $code : $_\" -ForegroundColor Red" ^
  "  }" ^
  "}"

echo.
pause
