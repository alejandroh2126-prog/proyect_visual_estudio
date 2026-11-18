; ============================================================
;  SGAPE - Script del instalador (Inno Setup 6)
;  Lo mas facil: ejecuta crear_instalador.bat (hace todo).
;  A mano: 1) Instalador\preparar.ps1 (carpeta Distribucion con la conexion)
;          2) publicar.bat (genera Publicado\SGAPE.exe)
;          3) Abre este archivo con Inno Setup y pulsa "Compile" (Ctrl+F9)
;  3) El instalador queda en Instalador\Salida\SGAPE-Instalador-2.0.0.exe
; ============================================================

#define NombreApp "SGAPE"
#define VersionApp "2.0.0"
#define Editor "Universidad Popular del Cesar"

[Setup]
AppId={{B7E2A1F4-6C3D-4E8A-9A55-5D1F0C2E7A10}
AppName={#NombreApp}
AppVersion={#VersionApp}
AppPublisher={#Editor}
DefaultDirName={autopf}\{#NombreApp}
DefaultGroupName={#NombreApp}
DisableProgramGroupPage=yes
OutputDir=Salida
OutputBaseFilename=SGAPE-Instalador-{#VersionApp}
Compression=lzma
SolidCompression=yes
WizardStyle=modern
ArchitecturesAllowed=x64compatible
ArchitecturesInstallIn64BitMode=x64compatible
PrivilegesRequired=lowest
PrivilegesRequiredOverridesAllowed=dialog
UninstallDisplayIcon={app}\SGAPE.exe

[Languages]
Name: "spanish"; MessagesFile: "compiler:Languages\Spanish.isl"

[Tasks]
Name: "iconoescritorio"; Description: "Crear un acceso directo en el escritorio"; GroupDescription: "Accesos directos:"

[Files]
Source: "..\Publicado\SGAPE.exe"; DestDir: "{app}"; Flags: ignoreversion
; Conexion a Oracle (usuario limitado SGAPE_APP) y wallet: asi SGAPE se conecta solo en cualquier computador
Source: "..\Distribucion\conexion.json"; DestDir: "{app}\conexion"; Flags: ignoreversion
Source: "..\Distribucion\wallet\*"; DestDir: "{app}\conexion\wallet"; Flags: ignoreversion recursesubdirs

[Icons]
Name: "{autoprograms}\{#NombreApp}"; Filename: "{app}\SGAPE.exe"
Name: "{autodesktop}\{#NombreApp}"; Filename: "{app}\SGAPE.exe"; Tasks: iconoescritorio

[Run]
Filename: "{app}\SGAPE.exe"; Description: "Abrir SGAPE ahora"; Flags: nowait postinstall skipifsilent