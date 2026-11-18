# =====================================================================
#  SGAPE - Compila el bot para Linux, arma el paquete y lo instala en la VM
#  de Oracle Cloud por SSH (Windows ya trae ssh y scp). Lo llama subir-bot-a-vm.bat.
#  Se puede repetir para ACTUALIZAR el bot cuando cambies el codigo.
#  Los secretos van solo en el paquete temporal y en /etc/sgape-bot.env de la VM.
# =====================================================================
$ErrorActionPreference = 'Stop'
$aqui = $PSScriptRoot
$raiz = Split-Path -Parent (Split-Path -Parent $aqui)
$paquete = Join-Path $raiz 'PublicadoBot\paquete'
$recordar = Join-Path $raiz 'PublicadoBot\vm.txt'   # IP, llave y arquitectura (sin secretos)

function Preguntar($texto, $defecto) {
    $v = Read-Host "$texto [$defecto]"
    if ([string]::IsNullOrWhiteSpace($v)) { return $defecto } else { return $v.Trim().Trim('"') }
}
function Plano($segura) {
    [Runtime.InteropServices.Marshal]::PtrToStringAuto([Runtime.InteropServices.Marshal]::SecureStringToBSTR($segura))
}

# ---------- datos de la VM ----------
$previo = @{ ip = ''; llave = ''; arq = 'linux-x64' }
if (Test-Path $recordar) { try { $previo = Get-Content $recordar -Raw | ConvertFrom-Json } catch { } }
$ip = Preguntar 'IP publica de la VM' $previo.ip
$llave = Preguntar 'Archivo de la llave SSH (.key)' $previo.llave
$arq = Preguntar 'Arquitectura: linux-x64 (E2.1.Micro) o linux-arm64 (A1.Flex)' $previo.arq
if (-not (Test-Path $llave)) { throw "No existe la llave: $llave" }
New-Item -ItemType Directory -Force -Path (Split-Path $recordar) | Out-Null
@{ ip = $ip; llave = $llave; arq = $arq } | ConvertTo-Json | Set-Content $recordar -Encoding UTF8

# ssh exige que la llave solo la pueda leer tu usuario
icacls $llave /inheritance:r /grant:r "$($env:USERNAME):R" | Out-Null

# ---------- secretos (se escriben solo en el paquete temporal) ----------
$ajustes = Join-Path $raiz 'BotTelegram\Properties\launchSettings.json'
$env0 = @{}
if (Test-Path $ajustes) {
    try { ((Get-Content $ajustes -Raw | ConvertFrom-Json).profiles.BotTelegram.environmentVariables).psobject.Properties |
          ForEach-Object { $env0[$_.Name] = "$($_.Value)".Trim() } } catch { }
}
$token = $env0['TELEGRAM_BOT_TOKEN']
if (-not $token) { $token = Plano (Read-Host 'Token de Telegram (@BotFather)' -AsSecureString) }
$claveBot = $env0['SGAPE_DB_CLAVE']
if (-not $claveBot) { $claveBot = Plano (Read-Host 'Clave de SGAPE_BOT' -AsSecureString) }
$dataSource = if ($env0['SGAPE_DB_DATASOURCE']) { $env0['SGAPE_DB_DATASOURCE'] } else { 'sgape_low' }
$wallet = if ($env0['SGAPE_DB_WALLET']) { $env0['SGAPE_DB_WALLET'] } else { Preguntar 'Carpeta del wallet descomprimido' '' }
if (-not (Test-Path (Join-Path $wallet 'tnsnames.ora'))) { throw "En '$wallet' no esta tnsnames.ora." }

# ---------- compilar y armar ----------
Write-Host 'Compilando el bot para Linux...'
dotnet publish (Join-Path $raiz 'BotTelegram\BotTelegram.csproj') -c Release -r $arq --self-contained true `
    -p:PublishSingleFile=true -o (Join-Path $raiz 'PublicadoBot') | Out-Host
if ($LASTEXITCODE -ne 0) { throw 'Fallo la compilacion del bot.' }

if (Test-Path $paquete) { Remove-Item $paquete -Recurse -Force }
New-Item -ItemType Directory -Path (Join-Path $paquete 'wallet') | Out-Null
Copy-Item (Join-Path $raiz 'PublicadoBot\sgape-bot') $paquete
Copy-Item (Join-Path $wallet '*') (Join-Path $paquete 'wallet') -Recurse
Copy-Item (Join-Path $aqui 'sgape-bot.service') $paquete
Copy-Item (Join-Path $aqui 'instalar-en-vm.sh') $paquete

# La clave de Groq normalmente la lee de Oracle (sg_configuracion); si la tienes en launchSettings tambien va
$lineas = @(
    "TELEGRAM_BOT_TOKEN=$token",
    'SGAPE_DB_USUARIO=SGAPE_BOT',
    "SGAPE_DB_CLAVE=$claveBot",
    "SGAPE_DB_DATASOURCE=$dataSource",
    'SGAPE_DB_WALLET=/opt/sgape-bot/wallet'
)
if ($env0['GROQ_API_KEY']) { $lineas += "GROQ_API_KEY=$($env0['GROQ_API_KEY'])" }
if ($env0['GROQ_MODELO']) { $lineas += "GROQ_MODELO=$($env0['GROQ_MODELO'])" }
# Formato Linux (LF) y sin BOM
[IO.File]::WriteAllText((Join-Path $paquete 'sgape-bot.env'), (($lineas -join "`n") + "`n"))
$sh = Join-Path $paquete 'instalar-en-vm.sh'
[IO.File]::WriteAllText($sh, ((Get-Content $sh -Raw) -replace "`r`n", "`n"))

# ---------- subir e instalar ----------
$opc = @('-i', $llave, '-o', 'StrictHostKeyChecking=accept-new')
Write-Host "Subiendo a ubuntu@$ip ..."
ssh @opc "ubuntu@$ip" 'rm -rf /tmp/sgape-bot-paquete'
scp @opc -r $paquete "ubuntu@${ip}:/tmp/sgape-bot-paquete"
if ($LASTEXITCODE -ne 0) { throw 'No se pudo copiar a la VM (revisa la IP, la llave y que el puerto 22 este abierto).' }
ssh @opc "ubuntu@$ip" 'bash /tmp/sgape-bot-paquete/instalar-en-vm.sh'
if ($LASTEXITCODE -ne 0) { throw 'Fallo la instalacion en la VM.' }

Remove-Item $paquete -Recurse -Force   # no dejar secretos en el PC
Write-Host ''
Write-Host '[OK] El bot quedo corriendo 24/7 en la VM. Ya puedes cerrar el bot de Visual Studio.'
