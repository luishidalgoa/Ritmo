# Instalar Ritmo

Ritmo se distribuye como paquete **MSIX** firmado con un **certificado auto-firmado**
(gratuito). Windows no confía en ese certificado por defecto, así que en un equipo
nuevo verás el error **0x800B010A** («No se pudo comprobar el certificado de
publicador…») y el botón **Instalar** aparece deshabilitado.

Solución: **confiar una vez en el certificado público** y luego instalar. Solo hay que
hacerlo la primera vez; las auto-actualizaciones posteriores ya funcionan solas.

## 1. Descarga los ficheros de la última versión

Desde la página de releases:
<https://github.com/luishidalgoa/Ritmo/releases/latest>

- `Ritmo-signing.cer` — el certificado **público** (no es secreto).
- `Ritmo.appinstaller` — el instalador con auto-update (recomendado), **o**
- `Ritmo-x64.msix` — el paquete suelto.

## 2. Confía en el certificado (una sola vez)

### Opción rápida — PowerShell **como Administrador**

```powershell
Import-Certificate -FilePath "$HOME\Downloads\Ritmo-signing.cer" -CertStoreLocation Cert:\LocalMachine\Root
```

### Opción con ratón

1. Doble clic en `Ritmo-signing.cer` → **Instalar certificado**.
2. Ubicación del almacén: **Equipo local** (pedirá permisos de administrador).
3. *«Colocar todos los certificados en el siguiente almacén»* → **Examinar** →
   **Entidades de certificación raíz de confianza**.
4. **Siguiente** → **Finalizar** → **Sí** en el aviso de seguridad.

## 3. Instala Ritmo

Abre `Ritmo.appinstaller` (o `Ritmo-x64.msix`). Ahora el **Editor** aparece como de
confianza y el botón **Instalar** está disponible. Pulsa **Instalar**.

> Si usas `Ritmo.appinstaller`, Ritmo se **auto-actualizará** solo al abrirlo cuando
> haya una versión nueva.

## ¿Sin este paso? (sin avisos para nadie)

El paso del certificado es la consecuencia de firmar gratis (auto-firmado). Para que
nadie tenga que hacerlo:

- **Microsoft Store** — Microsoft firma el paquete; instalación de un clic, sin avisos.
- **Azure Trusted Signing** (~10 USD/mes) — mantiene este mismo flujo MSIX, pero la
  firma encadena a una raíz de Microsoft → instala sin avisos ni confiar nada.
- **Certificado de firma comercial** (DigiCert/Sectigo, de pago anual) — mismo efecto.
