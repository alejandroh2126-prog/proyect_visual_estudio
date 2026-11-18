@echo off
chcp 65001 >nul
echo ==============================================
echo   SGAPE - Subir el bot a la VM de Oracle Cloud
echo ==============================================
echo  Necesitas la IP publica de la VM y el archivo .key que descargaste al crearla.
echo  Toma el token y la clave de SGAPE_BOT de BotTelegram\Properties\launchSettings.json.
echo  Repite este archivo cada vez que quieras ACTUALIZAR el bot.
echo.
powershell -NoProfile -ExecutionPolicy Bypass -File "%~dp0subir-bot-a-vm.ps1"
if errorlevel 1 ( echo. & echo [ERROR] Revisa el mensaje de arriba. & pause & exit /b 1 )
pause
