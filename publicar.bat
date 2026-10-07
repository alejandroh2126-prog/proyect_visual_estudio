@echo off
chcp 65001 >nul
echo ==============================================
echo   SGAPE - Publicando la aplicacion de escritorio
echo ==============================================
echo.

REM Genera UN SOLO archivo SGAPE.exe que ya trae .NET adentro.
REM El computador donde se instale NO necesita instalar .NET.
dotnet publish Presentacion\Presentacion.csproj -c Release -r win-x64 --self-contained true ^
  -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true ^
  -p:EnableCompressionInSingleFile=true -o Publicado

if errorlevel 1 (
  echo.
  echo [ERROR] Fallo la publicacion. Revisa los mensajes de arriba.
  pause
  exit /b 1
)

echo.
echo [OK] Listo. Tu aplicacion esta en:  %CD%\Publicado\SGAPE.exe
echo      Siguiente paso: abrir Instalador\SGAPE.iss con Inno Setup y pulsar Compilar.
echo.
pause