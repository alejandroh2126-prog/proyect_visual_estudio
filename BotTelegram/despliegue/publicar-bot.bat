@echo off
chcp 65001 >nul
REM Compila el bot para Linux (VM de Oracle Cloud). x64 para VM.Standard.E2.1.Micro;
REM si tu VM es Ampere (VM.Standard.A1.Flex) cambia linux-x64 por linux-arm64.
dotnet publish ..\BotTelegram.csproj -c Release -r linux-x64 --self-contained true -p:PublishSingleFile=true -o ..\..\PublicadoBot
if errorlevel 1 ( echo [ERROR] Fallo la publicacion. & pause & exit /b 1 )
echo [OK] Bot listo en PublicadoBot\sgape-bot  (copialo a la VM con scp)
pause
