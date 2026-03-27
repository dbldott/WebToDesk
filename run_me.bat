@echo off
chcp 65001 >nul
cd /d "%~dp0"

set MINIO_ROOT_USER=minioadmin
set MINIO_ROOT_PASSWORD=minioadmin

echo ==============================================
echo  WebToDesk — запуск
echo ==============================================

:: --- 1. MINIO ---
echo.
echo [1/3] Запуск MinIO...
if not exist "%~dp0data" mkdir "%~dp0data"
start "MinIO Server" "%~dp0minio.exe" server "%~dp0data" --console-address ":9001"
echo Ждём 5 секунд...
timeout /t 5 /nobreak >nul

:: --- 2. НАСТРОЙКА БАКЕТА ---
echo.
echo [2/3] Настройка бакета...
"%~dp0mc.exe" alias set myminio http://127.0.0.1:9000/ minioadmin minioadmin
"%~dp0mc.exe" mb --ignore-existing myminio/bucket
"%~dp0mc.exe" anonymous set public myminio/bucket

:: --- 3. ЗАПУСК ПРИЛОЖЕНИЯ ---
echo.
echo [3/3] Запуск приложения...

:: Завершаем старые процессы WebToDesk если есть
for /f "tokens=5" %%a in ('netstat -ano 2^>nul ^| findstr ":5000 "') do (
    taskkill /F /PID %%a >nul 2>&1
)
taskkill /F /IM WebToDesk.exe >nul 2>&1

start "WebToDesk" cmd /c "cd /d "%~dp0src\desktop\Desktop.App\WebToDesk" && dotnet run"

echo Ждём запуска сервера...
timeout /t 15 /nobreak >nul

echo.
echo ==============================================
echo  Готово!
echo  Браузер:   http://localhost:5000
echo  MinIO UI:  http://localhost:9001
echo ==============================================
start http://localhost:5000
