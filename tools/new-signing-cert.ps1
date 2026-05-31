<#
.SYNOPSIS
  Genera el certificado AUTO-FIRMADO con el que se firma el MSIX de Ritmo en la CD.
  Subject = "CN=AppPublisher" (DEBE coincidir con el Publisher de Package.appxmanifest).

.DESCRIPTION
  Crea un cert de firma de código, lo exporta a .pfx (privado, va a un GitHub Secret) y a
  .cer (público, se reparte para confiar en él una vez), e imprime el .pfx en base64 listo
  para pegar como secret. Ejecútalo UNA vez en tu máquina; guarda el .pfx en lugar seguro.

.NOTES
  Tras ejecutarlo, en GitHub → repo → Settings → Secrets and variables → Actions, crea:
    - SIGNING_PFX_BASE64    = contenido de Ritmo-signing.pfx.base64.txt
    - SIGNING_PFX_PASSWORD  = la contraseña que elijas aquí
  NO subas el .pfx ni el base64 al repo (están en .gitignore por seguridad).
#>
param(
    [string] $Subject = "CN=AppPublisher",
    [string] $OutDir  = "$PSScriptRoot\out"
)

$ErrorActionPreference = "Stop"
New-Item -ItemType Directory -Force -Path $OutDir | Out-Null

Write-Host "Generando certificado de firma de código ($Subject)..."
$cert = New-SelfSignedCertificate `
    -Type CodeSigningCert `
    -Subject $Subject `
    -KeyUsage DigitalSignature `
    -FriendlyName "Ritmo signing" `
    -CertStoreLocation "Cert:\CurrentUser\My" `
    -NotAfter (Get-Date).AddYears(5) `
    -TextExtension @("2.5.29.37={text}1.3.6.1.5.5.7.3.3")

$pwdPlain = Read-Host "Elige una contraseña para el .pfx (la pondrás también como secret SIGNING_PFX_PASSWORD)"
$pwd = ConvertTo-SecureString -String $pwdPlain -Force -AsPlainText

$pfxPath = Join-Path $OutDir "Ritmo-signing.pfx"
$cerPath = Join-Path $OutDir "Ritmo-signing.cer"
Export-PfxCertificate -Cert $cert -FilePath $pfxPath -Password $pwd | Out-Null
Export-Certificate   -Cert $cert -FilePath $cerPath | Out-Null

$b64Path = Join-Path $OutDir "Ritmo-signing.pfx.base64.txt"
[Convert]::ToBase64String([IO.File]::ReadAllBytes($pfxPath)) | Set-Content -NoNewline -Path $b64Path

Write-Host ""
Write-Host "Listo. Archivos en: $OutDir" -ForegroundColor Green
Write-Host "  - Ritmo-signing.pfx           (PRIVADO: guárdalo a buen recaudo, NO lo subas)"
Write-Host "  - Ritmo-signing.cer           (PÚBLICO: se adjunta a las releases para confiar en él)"
Write-Host "  - Ritmo-signing.pfx.base64.txt (pega su contenido en el secret SIGNING_PFX_BASE64)"
Write-Host ""
Write-Host "En GitHub → Settings → Secrets and variables → Actions, crea:" -ForegroundColor Yellow
Write-Host "  SIGNING_PFX_BASE64    = (contenido de Ritmo-signing.pfx.base64.txt)"
Write-Host "  SIGNING_PFX_PASSWORD  = (la contraseña que acabas de elegir)"
Write-Host ""
Write-Host "Thumbprint del cert: $($cert.Thumbprint)"
