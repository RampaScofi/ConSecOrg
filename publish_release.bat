@echo off
chcp 65001 > nul
echo ==========================================
echo  ConSecOrg — Публикация Release сборок
echo ==========================================
echo.

cd /d "%~dp0"

:: Публикация клиента
echo [1/3] Публикация клиента (WPF)...
dotnet publish src\ConSecOrg.Client -c Release -r win-x64 --self-contained false -o publish\Client
if %errorlevel% neq 0 (
    echo [ОШИБКА] Публикация клиента завершилась с ошибкой
    pause
    exit /b 1
)
echo    Клиент опубликован в publish\Client\
echo.

:: Публикация сервера
echo [2/3] Публикация сервера (ASP.NET Core)...
dotnet publish src\ConSecOrg.Server -c Release -r win-x64 --self-contained false -o publish\Server
if %errorlevel% neq 0 (
    echo [ОШИБКА] Публикация сервера завершилась с ошибкой
    pause
    exit /b 1
)
echo    Сервер опубликован в publish\Server\
echo.

:: Копируем инструкцию если есть
echo [3/3] Копирование вспомогательных файлов...
if exist "ИНСТРУКЦИЯ.txt" (
    copy "ИНСТРУКЦИЯ.txt" "publish\ИНСТРУКЦИЯ.txt" > nul
    echo    Инструкция скопирована
) else (
    echo    ИНСТРУКЦИЯ.txt не найдена — создайте файл или установщик выдаст предупреждение
)
echo.

echo ==========================================
echo  Публикация завершена!
echo  Теперь запустите: installer\build_installer.bat
echo ==========================================
echo.
pause
