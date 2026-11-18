@echo off
chcp 65001 >nul
cd /d "%~dp0"
echo ==============================================
echo   SGAPE - Crear el instalador para cualquier PC
echo ==============================================
echo  Antes, una sola vez, en SQL Developer como ADMIN:
echo    1. Ejecuta sql\sgape_v2_usuario_app.sql
echo    2. ALTER USER sgape_app IDENTIFIED BY "TuClaveSegura2026" ACCOUNT UNLOCK;
echo.

REM 1) Conexion + wallet que viajan dentro del instalador (carpeta Distribucion, fuera de Git)
powershell -NoProfile -ExecutionPolicy Bypass -File "Instalador\preparar.ps1"
if errorlevel 1 ( echo [ERROR] No se preparo la conexion. & pause & exit /b 1 )

REM 2) SGAPE.exe autocontenido (no necesita .NET instalado)
dotnet publish Presentacion\Presentacion.csproj -c Release -r win-x64 --self-contained true ^
  -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true ^
  -p:EnableCompressionInSingleFile=true -o Publicado
if errorlevel 1 ( echo [ERROR] Fallo la publicacion. & pause & exit /b 1 )

REM 3) Version portable (zip): SGAPE.exe + carpeta conexion. Funciona en cualquier PC sin instalar nada.
if not exist Instalador\Salida mkdir Instalador\Salida
powershell -NoProfile -Command "$t='Instalador\Salida\portable\SGAPE'; Remove-Item 'Instalador\Salida\portable' -Recurse -Force -ErrorAction SilentlyContinue; New-Item -ItemType Directory -Force -Path \"$t\conexion\" | Out-Null; Copy-Item 'Publicado\SGAPE.exe' $t; Copy-Item 'Distribucion\*' \"$t\conexion\" -Recurse; Compress-Archive -Path 'Instalador\Salida\portable\SGAPE' -DestinationPath 'Instalador\Salida\SGAPE-portable.zip' -Force; Remove-Item 'Instalador\Salida\portable' -Recurse -Force"
echo [OK] Portable: %CD%\Instalador\Salida\SGAPE-portable.zip

REM 4) Instalador con Inno Setup (si esta instalado)
set ISCC=%ProgramFiles(x86)%\Inno Setup 6\ISCC.exe
if not exist "%ISCC%" set ISCC=%ProgramFiles%\Inno Setup 6\ISCC.exe
if not exist "%ISCC%" set ISCC=%LOCALAPPDATA%\Programs\Inno Setup 6\ISCC.exe
if not exist "%ISCC%" (
  echo.
  echo [AVISO] No encontre Inno Setup 6. Instalalo desde https://jrsoftware.org/isdl.php
  echo         y luego abre Instalador\SGAPE.iss y pulsa Compilar.
  pause & exit /b 0
)
"%ISCC%" Instalador\SGAPE.iss
if errorlevel 1 ( echo [ERROR] Fallo Inno Setup. & pause & exit /b 1 )

echo.
echo [OK] Instalador listo: %CD%\Instalador\Salida\SGAPE-Instalador-2.0.0.exe
echo      Instalalo en cualquier computador: se conecta solo a Oracle (usuario SGAPE_APP).
echo      NO lo subas a Git ni lo publiques: lleva la conexion a la base de datos.
pause
