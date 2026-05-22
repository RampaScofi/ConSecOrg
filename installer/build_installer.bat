@echo off
chcp 65001 > nul
echo ==========================================
echo  ConSecOrg — Сборка установщика Windows
echo ==========================================
echo.

:: Пути к Inno Setup (проверяем стандартные места установки)
set ISS_COMPILER=
if exist "C:\Program Files (x86)\Inno Setup 6\iscc.exe" (
    set ISS_COMPILER="C:\Program Files (x86)\Inno Setup 6\iscc.exe"
) else if exist "C:\Program Files\Inno Setup 6\iscc.exe" (
    set ISS_COMPILER="C:\Program Files\Inno Setup 6\iscc.exe"
) else (
    echo [ОШИБКА] Inno Setup 6 не найден!
    echo.
    echo Скачайте и установите с: https://jrsoftware.org/isdl.php
    echo.
    pause
    exit /b 1
)

:: Проверяем наличие publish папки
if not exist "..\publish\Client" (
    echo [ОШИБКА] Папка publish\Client не найдена!
    echo.
    echo Сначала выполните публикацию проекта:
    echo   dotnet publish src\ConSecOrg.Client -c Release -o publish\Client
    echo   dotnet publish src\ConSecOrg.Server -c Release -o publish\Server
    echo.
    pause
    exit /b 1
)

if not exist "..\publish\Server" (
    echo [ПРЕДУПРЕЖДЕНИЕ] Папка publish\Server не найдена — компонент сервера будет пропущен
    echo.
)

:: Проверяем иконку
if not exist "ConSecOrg_Icon.ico" (
    echo [ПРЕДУПРЕЖДЕНИЕ] Файл ConSecOrg_Icon.ico не найден — будет использована иконка по умолчанию
    echo Рекомендуется добавить иконку в папку installer\
    echo.
)

echo Компиляция установщика...
echo.
%ISS_COMPILER% ConSecOrg_Setup.iss

if %errorlevel% == 0 (
    echo.
    echo ==========================================
    echo  Установщик успешно создан!
    echo  Файл: installer\Output\ConSecOrg_Setup_v1.0.0.exe
    echo ==========================================
) else (
    echo.
    echo [ОШИБКА] Сборка установщика завершилась с ошибкой (код %errorlevel%)
)

echo.
pause
