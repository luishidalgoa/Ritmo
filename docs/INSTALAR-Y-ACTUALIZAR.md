# Instalar y actualizar Ritmo

Ritmo se distribuye como **MSIX firmado** desde las *Releases* de GitHub, y se
**actualiza solo** mediante **App Installer** de Windows (nativo, silencioso). No hay
tienda ni cuentas.

## Para el usuario final

### Primera instalación (una sola vez)
1. Ve a la última *Release*: `https://github.com/luishidalgoa/Ritmo/releases/latest`.
2. Descarga **`Ritmo-signing.cer`** e instálalo como confiable (solo la primera vez):
   - Doble clic → *Instalar certificado* → **Equipo local** → *Colocar en el siguiente
     almacén* → **Personas de confianza** (Trusted People) → Finalizar.
   - (Es un certificado auto-firmado del autor; basta con confiar en él una vez.)
3. Descarga **`Ritmo.appinstaller`** y ábrelo → *Instalar*. Quedas suscrito a las
   actualizaciones.

### Actualizaciones
A partir de ahí **no tienes que hacer nada**: App Installer comprueba la *Release* más
reciente al abrir la app y aplica la actualización en segundo plano. Cuando se actualice,
verás el aviso del botón **«Novedades»** dentro de la app con lo nuevo.

---

## Para publicar una versión (mantenedor)

### Preparación (una sola vez): certificado de firma
El MSIX debe ir firmado. Genera un certificado auto-firmado:

```powershell
pwsh tools/new-signing-cert.ps1
```

Esto crea en `tools/out/`:
- `Ritmo-signing.pfx` — **privado**, guárdalo a buen recaudo (NO se sube al repo).
- `Ritmo-signing.cer` — público (la CD ya lo adjunta a cada release).
- `Ritmo-signing.pfx.base64.txt` — para el secret.

En GitHub → **Settings → Secrets and variables → Actions**, crea:
- `SIGNING_PFX_BASE64` = contenido de `Ritmo-signing.pfx.base64.txt`.
- `SIGNING_PFX_PASSWORD` = la contraseña que elegiste.

> El subject del cert es `CN=AppPublisher`, que **debe coincidir** con el `Publisher`
> de `src/Ritmo.App/Package.appxmanifest`.

### Publicar
Empuja un tag de versión (o usa *Run workflow* en la pestaña Actions):

```bash
git tag v1.0.2
git push origin v1.0.2
```

El workflow `release.yml`:
1. Fija la versión `1.0.2.0` en el manifiesto.
2. Compila y **firma** el MSIX.
3. Genera el `Ritmo.appinstaller` apuntando a las URLs estables
   `releases/latest/download/...`.
4. Crea la *Release* con `Ritmo-x64.msix`, `Ritmo.appinstaller` y `Ritmo-signing.cer`.

Como el `.appinstaller` apunta a `latest/download`, las apps ya instaladas se
auto-actualizan a esa nueva versión sin tocar nada.

### Recuerda
- La versión del tag debe ser **mayor** que la instalada (App Installer compara versiones).
- Añade las novedades a nivel usuario en `src/Ritmo.Core/Updates/ReleaseNotes.cs` (carrusel
  «Novedades»), con `Version` igual a la que publicas.
