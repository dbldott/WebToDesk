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
echo [1/4] Запуск MinIO...
if not exist "%~dp0data" mkdir "%~dp0data"
start "MinIO Server" "%~dp0minio.exe" server "%~dp0data" --console-address ":9001"
echo Ждём 5 секунд...
timeout /t 5 /nobreak >nul

:: --- 2. НАСТРОЙКА БАКЕТА ---
echo.
echo [2/4] Настройка бакета...
"%~dp0mc.exe" alias set myminio http://127.0.0.1:9000/ minioadmin minioadmin
"%~dp0mc.exe" mb --ignore-existing myminio/bucket
"%~dp0mc.exe" anonymous set public myminio/bucket

:: --- 3. СБОРКА BLAZOR ---
echo.
echo [3/4] Сборка веб-интерфейса...

set WEB_SRC=%~dp0src\web\WebToDesk.Web
set DESKTOP_WWWROOT=%~dp0src\desktop\Desktop.App\WebToDesk\wwwroot
set INDEX_TEMPLATE=%~dp0src\desktop\Desktop.App\WebToDesk\index.template.html

dotnet publish "%WEB_SRC%" -c Release -o "%WEB_SRC%\publish" --nologo -v quiet

if errorlevel 1 (
    echo ОШИБКА сборки! Проверьте что установлен .NET SDK.
    pause
    exit /b 1
)

echo Копирование файлов интерфейса...
xcopy /E /Y /I /Q "%WEB_SRC%\publish\wwwroot\*" "%DESKTOP_WWWROOT%\"

:: Копируем наш index.html с JS-функциями поверх publish-версии
echo Применяем index.html с JS-функциями...
copy /Y "%INDEX_TEMPLATE%" "%DESKTOP_WWWROOT%\index.html" >nul

:: --- 4. ЗАПУСК ПРИЛОЖЕНИЯ ---
echo.
echo [4/4] Запуск приложения...
start "WebToDesk" cmd /c "cd /d "%~dp0src\desktop\Desktop.App\WebToDesk" && dotnet run"

echo.
echo ==============================================
echo  Готово!
echo  Браузер:   http://localhost:5000
echo  MinIO UI:  http://localhost:9001
echo ==============================================
timeout /t 4 /nobreak >nul
start http://localhost:5000
