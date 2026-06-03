; Instalador tradicional .exe para Ritmo (Inno Setup).
; Envuelve el MSIX: confía en el certificado, instala el runtime de Windows App SDK
; y registra el paquete, todo de un doble clic. NOTA: el propio RitmoSetup.exe NO va
; firmado, así que SmartScreen mostrará "editor desconocido" (pulsar "Más info → Ejecutar
; de todas formas"). Para quitar TODOS los avisos hace falta Store o Azure Trusted Signing.
;
; Se compila en el CI (release.yml) con:
;   ISCC.exe /DMyAppVersion=<version> tools\installer\Ritmo.iss
; Los 3 ficheros que empaqueta (msix, cer, runtime) se copian junto al .iss antes de compilar.

#ifndef MyAppVersion
  #define MyAppVersion "0.0.0"
#endif

[Setup]
AppId={{1C50E89F-FF4B-4943-95BB-2A162633C5D2}
AppName=Ritmo
AppVersion={#MyAppVersion}
AppPublisher=Luis Hidalgo
AppPublisherURL=https://luishidalgoa.vercel.app/
DefaultDirName={autopf}\Ritmo
CreateAppDir=no
DisableProgramGroupPage=yes
DisableDirPage=yes
PrivilegesRequired=admin
ArchitecturesAllowed=x64compatible
ArchitecturesInstallIn64BitMode=x64compatible
OutputBaseFilename=RitmoSetup
Compression=lzma2/max
SolidCompression=yes
WizardStyle=modern
UninstallDisplayName=Ritmo
UninstallDisplayIcon={sys}\shell32.dll,13

[Languages]
Name: "es"; MessagesFile: "compiler:Languages\Spanish.isl"

[Files]
Source: "Ritmo-x64.msix"; DestDir: "{tmp}"; Flags: deleteafterinstall
Source: "Ritmo-signing.cer"; DestDir: "{tmp}"; Flags: deleteafterinstall
Source: "WindowsAppRuntimeInstall-x64.exe"; DestDir: "{tmp}"; Flags: deleteafterinstall

[Run]
; 1) Confiar en el certificado (raíz de equipo local) -> resuelve el 0x800B010A del MSIX.
Filename: "certutil.exe"; Parameters: "-addstore -f ""Root"" ""{tmp}\Ritmo-signing.cer"""; \
  Flags: runhidden waituntilterminated; StatusMsg: "Confiando en el certificado de Ritmo..."
; 2) Instalar el runtime de Windows App SDK (idempotente; si ya está, no hace nada).
Filename: "{tmp}\WindowsAppRuntimeInstall-x64.exe"; Parameters: "--quiet"; \
  Flags: waituntilterminated; StatusMsg: "Instalando el runtime de Windows App SDK..."
; 3) Registrar el paquete MSIX para el usuario.
Filename: "powershell.exe"; \
  Parameters: "-NoProfile -ExecutionPolicy Bypass -Command ""Add-AppxPackage -Path '{tmp}\Ritmo-x64.msix'"""; \
  Flags: runhidden waituntilterminated; StatusMsg: "Instalando Ritmo..."

[UninstallRun]
; Al desinstalar, quita el paquete MSIX (el certificado se deja por si hay otra versión).
Filename: "powershell.exe"; \
  Parameters: "-NoProfile -ExecutionPolicy Bypass -Command ""Get-AppxPackage -Name '1C50E89F-FF4B-4943-95BB-2A162633C5D2' | Remove-AppxPackage"""; \
  Flags: runhidden waituntilterminated; RunOnceId: "RemoveRitmoMsix"
