# Lanza Ritmo.App de forma ESTABLE, desacoplada del proceso dotnet.
#
# Por qué: `dotnet run` sobre una app WinUI empaquetada mantiene un proceso
# lanzador; cuando ese proceso termina, cancela sus tareas (TaskCanceledException)
# y derriba la app (cierre con 0xc000027b). Registrar el MSIX y lanzar por AUMID
# evita esa dependencia: la app vive por sí misma.
#
# Uso:  pwsh -File scripts\run-app.ps1

$ErrorActionPreference = "Stop"
$root = Split-Path $PSScriptRoot -Parent
$proj = Join-Path $root "src\Ritmo.App\Ritmo.App.csproj"
$aumid = "1C50E89F-FF4B-4943-95BB-2A162633C5D2_1z32rh13vfry6!App"

Write-Host "1/4 Cerrando instancias previas..."
Get-Process -Name "Ritmo.App" -ErrorAction SilentlyContinue | Stop-Process -Force -ErrorAction SilentlyContinue

Write-Host "2/4 Compilando (win-x64)..."
dotnet build $proj -c Debug -r win-x64 | Out-Null
if ($LASTEXITCODE -ne 0) { Write-Error "Fallo de compilación"; exit 1 }

Write-Host "3/4 Registrando el paquete MSIX (modo desarrollo)..."
$manifest = Join-Path $root "src\Ritmo.App\bin\Debug\net9.0-windows10.0.26100.0\win-x64\AppxManifest.xml"
Add-AppxPackage -Register $manifest -ForceUpdateFromAnyVersion

Write-Host "4/4 Lanzando por AUMID..."
Start-Process "shell:AppsFolder\$aumid"
Write-Host "Listo. Ritmo.App lanzada de forma independiente."
