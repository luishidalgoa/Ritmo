<#
.SYNOPSIS
  Lanza una release de Ritmo en UN comando: etiqueta la versión y la empuja, lo que dispara
  el workflow de CD (compila + FIRMA el MSIX + genera el .appinstaller + publica la GitHub
  Release con los assets). No compila nada en local: el trabajo lo hace GitHub Actions.

.EXAMPLE
  .\tools\release.ps1 1.0.3

.NOTES
  Antes de lanzar, conviene:
   - Haber añadido la entrada de la versión en src/Ritmo.Core/Updates/ReleaseNotes.cs
     (carrusel "Novedades" de la app) y en CHANGELOG.md.
   - Tener todo commiteado y pusheado a main (el script lo verifica).
  La versión del manifiesto la fija el propio workflow a partir del tag (no hace falta tocarla).
#>
param([Parameter(Mandatory)][string] $Version)

$ErrorActionPreference = "Stop"

if ($Version -notmatch '^\d+\.\d+\.\d+$') {
    throw "Versión inválida: '$Version'. Usa el formato X.Y.Z (p. ej. 1.0.3)."
}
$tag = "v$Version"

# --- Seguridad: no soltar una release con trabajo a medias ---
if (git status --porcelain) { throw "Hay cambios sin commitear. Comitea o descarta antes de lanzar." }
$branch = (git rev-parse --abbrev-ref HEAD).Trim()
if ($branch -ne "main") { throw "Estás en la rama '$branch', no en 'main'." }
git fetch origin main --quiet
if ((git rev-parse HEAD) -ne (git rev-parse origin/main)) {
    throw "main local y origin/main difieren. Haz push (o pull) antes de lanzar."
}
if (git tag --list $tag) { throw "El tag $tag ya existe. Usa otra versión." }

Write-Host "Etiquetando $tag sobre $(git rev-parse --short HEAD)..." -ForegroundColor Cyan
git tag -a $tag -m "Ritmo $tag"
git push origin $tag

$repo = (gh repo view --json nameWithOwner --jq '.nameWithOwner' 2>$null)
Write-Host ""
Write-Host "Listo. GitHub Actions está compilando, firmando y publicando la release." -ForegroundColor Green
if ($repo) {
    Write-Host "  Progreso : https://github.com/$repo/actions"
    Write-Host "  Release  : https://github.com/$repo/releases"
}
