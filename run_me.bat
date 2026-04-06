@echo off
setlocal
chcp 65001 >nul

if /I not "%~1"=="__hidden__" (
    powershell -NoProfile -ExecutionPolicy Bypass -WindowStyle Hidden -Command "Start-Process -FilePath '%~f0' -ArgumentList '__hidden__' -WindowStyle Hidden"
    exit /b
)

cd /d "%~dp0"

for /f "tokens=5" %%a in ('netstat -ano 2^>nul ^| findstr ":5000 "') do (
    taskkill /F /PID %%a >nul 2>&1
)
taskkill /F /IM WebToDesk.exe >nul 2>&1

set "PROJECT_DIR=%~dp0src\desktop\Desktop.App\WebToDesk"
set "PUBLISH_EXE=%PROJECT_DIR%\publish\WebToDesk.exe"
set "RELEASE_EXE=%PROJECT_DIR%\bin\Release\net10.0-windows\WebToDesk.exe"
set "DEBUG_EXE=%PROJECT_DIR%\bin\Debug\net10.0-windows\WebToDesk.exe"

if exist "%PUBLISH_EXE%" (
    start "" "%PUBLISH_EXE%"
    exit /b 0
)

if exist "%RELEASE_EXE%" (
    start "" "%RELEASE_EXE%"
    exit /b 0
)

if exist "%DEBUG_EXE%" (
    start "" "%DEBUG_EXE%"
    exit /b 0
)

powershell -NoProfile -ExecutionPolicy Bypass -WindowStyle Hidden -Command "Set-Location -LiteralPath '%PROJECT_DIR%'; dotnet run"
