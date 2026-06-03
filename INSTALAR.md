# Instalar Ritmo

Ritmo se firma con un **certificado auto-firmado** (gratuito), en el que Windows no confía
por defecto. Hay dos formas de instalar:

## Opción A — `RitmoSetup.exe` (la más fácil)

Descarga **`RitmoSetup.exe`** de la [última release](https://github.com/luishidalgoa/Ritmo/releases/latest)
y ejecútalo. De un doble clic: confía en el certificado, instala el runtime necesario y
registra la app.

> ⚠️ Como el propio `RitmoSetup.exe` **no va firmado**, Windows SmartScreen mostrará
> *«Windows protegió tu PC… editor desconocido»*. Pulsa **Más información → Ejecutar de
> todas formas**. (Para quitar TODOS los avisos haría falta publicar en la **Microsoft
> Store** o firmar con **Azure Trusted Signing** — ver final del documento.)

## Opción B — MSIX a mano

Si prefieres el paquete MSIX directamente, en un equipo nuevo verás el error **0x800B010A**
(«No se pudo comprobar el certificado de publicador…») y el botón **Instalar**
deshabilitado. Hay que **confiar una vez en el certificado** y luego instalar.

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
