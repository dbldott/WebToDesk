@echo off
set MINIO_ROOT_USER=minioadmin
set MINIO_ROOT_PASSWORD=minioadmin

echo --- 1. ЗАПУСК СЕРВЕРА ---
:: Запускаем сервер в отдельном окне, чтобы он не мешал выполнять команды дальше
start "MinIO Server" minio.exe server C:\MinIO_Server\data --console-address ":9001"

echo Ждем 5 секунд, пока сервер проснется...
timeout /t 5

echo --- 2. НАСТРОЙКА КЛИЕНТА (MC) ---
:: Привязываем клиент к нашему серверу
mc.exe alias set myminio http://127.0.0.1:9000/ minioadmin minioadmin

echo --- 3. СОЗДАНИЕ БАКЕТА ---

mc.exe mb myminio/bucket

echo --- 4. ОТКЛЮЧЕНИЕ АВТОРИЗАЦИИ (PUBLIC MODE) ---
:: Делаем бакет полностью публичным, чтобы код не спрашивал пароли
mc.exe anonymous set public myminio/bucket

echo --------------------------------------------------
echo ВСЁ ГОТОВО! 
echo Консоль (UI): http://127.0.0.1:9001/
echo API (для кода): http://127.0.0.1:9000/
echo Бакет 'bucket' открыт для чтения и записи.
echo --------------------------------------------------
pause