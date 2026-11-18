# =====================================================================
#  SGAPE - Prepara la conexion que viaja DENTRO del instalador
#  (lo ejecuta crear_instalador.bat; no hace falta abrirlo a mano)
#
#  Crea la carpeta Distribucion\ (esta en .gitignore: NUNCA va a Git) con:
#    conexion.json   usuario SGAPE_APP (limitado), su clave y "wallet" relativo
#    wallet\         copia del wallet de la Autonomous Database
#  El instalador copia esa carpeta junto a SGAPE.exe, asi la aplicacion se
#  conecta sola en cualquier computador.
# =====================================================================
$ErrorActionPreference = 'Stop'
$raiz = Split-Path -Parent $PSScriptRoot
$destino = Join-Path $raiz 'Distribucion'

# Si ya esta preparada (conexion.json + wallet), se reutiliza sin preguntar nada.
# Para cambiar la clave o el wallet, borra la carpeta Distribucion y vuelve a ejecutar.
if ((Test-Path (Join-Path $destino 'conexion.json')) -and (Test-Path (Join-Path $destino 'wallet\tnsnames.ora'))) {
    Write-Host "[OK] Se usa la conexion que ya esta en $destino"
    exit 0
}

# Valores por defecto: los de tu conexion de desarrollo (%LOCALAPPDATA%\SGAPE\conexion.json)
$walletDefecto = ''
$dataSource = 'sgape_low'
$miConexion = Join-Path $env:LOCALAPPDATA 'SGAPE\conexion.json'
if (Test-Path $miConexion) {
    try {
        $c = Get-Content $miConexion -Raw | ConvertFrom-Json
        if ($c.carpetaWallet) { $walletDefecto = $c.carpetaWallet }
        if ($c.dataSource) { $dataSource = $c.dataSource }
    } catch { }
}

Write-Host ''
$wallet = Read-Host "Carpeta del wallet descomprimido [$walletDefecto]"
if ([string]::IsNullOrWhiteSpace($wallet)) { $wallet = $walletDefecto }
$wallet = $wallet.Trim().Trim('"')
if (-not (Test-Path (Join-Path $wallet 'tnsnames.ora'))) {
    throw "En '$wallet' no esta tnsnames.ora. Indica la carpeta del wallet DESCOMPRIMIDO."
}

$segura = Read-Host 'Clave de SGAPE_APP (la que pusiste en ALTER USER sgape_app ...)' -AsSecureString
$clave = [Runtime.InteropServices.Marshal]::PtrToStringAuto([Runtime.InteropServices.Marshal]::SecureStringToBSTR($segura))
if ($clave.Length -lt 12) { throw 'La clave de SGAPE_APP debe tener al menos 12 caracteres.' }

if (Test-Path $destino) { Remove-Item $destino -Recurse -Force }
New-Item -ItemType Directory -Path (Join-Path $destino 'wallet') | Out-Null
Copy-Item (Join-Path $wallet '*') (Join-Path $destino 'wallet') -Recurse -Force

[ordered]@{
    usuario       = 'SGAPE_APP'
    clave         = $clave
    dataSource    = $dataSource
    carpetaWallet = 'wallet'
} | ConvertTo-Json | Set-Content -Path (Join-Path $destino 'conexion.json') -Encoding UTF8

Write-Host "[OK] Conexion lista en $destino (usuario SGAPE_APP, dataSource $dataSource)."
