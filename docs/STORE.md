# Publicar Ritmo en la Microsoft Store

La Store es la vía **«sin certificados»**: Microsoft firma el paquete, los usuarios instalan
con **un clic, sin avisos**, y las actualizaciones las gestiona la Store. No se usa el
certificado auto-firmado (`Ritmo-signing.pfx`) para nada.

> **Trade-off importante:** al publicar en la Store, la **identidad del paquete cambia**
> (Publisher e Identity los asigna la Store). Los usuarios que ya instalaron por sideload
> (`.appinstaller`/`.msix`) **NO se actualizan solos** a la versión de la Store: tendrán que
> instalarla una vez desde la Store. A partir de ahí, la Store lleva los updates.

## 1. Cuenta de desarrollador (Partner Center)

- Alta en <https://partner.microsoft.com/dashboard> → programa **Windows & Xbox**.
- Cuota **única**: ~**19 USD** (individual) / ~**99 USD** (empresa).
- Verifica identidad (individual basta para publicar).

## 2. Reservar el nombre de la app

- En Partner Center → **Apps and games → New product → MSIX/PWA app**.
- Reserva el nombre **«Ritmo»** (si está libre; si no, otro, p. ej. «Ritmo Focus»).
- Apunta de **Product Identity** (Partner Center → Product management → Product identity):
  - `Package/Identity/Name` (algo tipo `12345LuisHidalgo.Ritmo`)
  - `Package/Identity/Publisher` (`CN=...` que asigna la Store)
  - `Package/Properties/PublisherDisplayName`

## 3. Asociar el proyecto a esa identidad

Edita `src/Ritmo.App/Package.appxmanifest` con los 3 valores de arriba:

```xml
<Identity Name="12345LuisHidalgo.Ritmo" Publisher="CN=XXXXXXXX-..." Version="1.1.3.0" />
<Properties>
  <DisplayName>Ritmo</DisplayName>
  <PublisherDisplayName>Luis Hidalgo</PublisherDisplayName>
  ...
```

(En Visual Studio: clic derecho en el proyecto → **Publish → Associate App with the Store**
lo rellena solo.)

## 4. Capacidades restringidas → piden justificación

En el manifiesto usamos capacidades **restringidas**, que en la Store requieren solicitar
acceso (Partner Center → **App management → Restricted capabilities**) con una justificación:

- `runFullTrust` — estándar para apps de escritorio Win32; se aprueba de rutina.
- `unvirtualizedResources` — justificar: «escribir el nombre del escritorio virtual “Ritmo”
  en el registro real para que se vea en la Vista de tareas». Si la Store lo rechaza, se puede
  quitar (la app sigue funcionando; solo se pierde el nombre visible del escritorio).
- `systemAIModels` — **revisar si se usa de verdad**; venía de la plantilla. Si no se usa,
  **quítala** del manifiesto antes de enviar (simplifica la certificación).

## 5. Construir el paquete para la Store

```powershell
dotnet build src/Ritmo.App/Ritmo.App.csproj -c Release -p:Platform=x64 `
  -p:AppxBundle=Never -p:UapAppxPackageBuildMode=StoreUpload -p:GenerateAppxPackageOnBuild=true `
  -p:AppxPackageSigningEnabled=false
```

`StoreUpload` genera un `.msixupload` (no hace falta firmarlo: **lo firma Microsoft**).

## 6. Subir y certificar

- Partner Center → tu app → **Packages** → sube el `.msixupload`.
- Rellena la ficha (descripción, capturas, categoría «Productividad», edad, privacidad).
- **Submit** → pasa **certificación** (apps de escritorio full-trust van por el carril normal;
  suele tardar de horas a un par de días).
- Al aprobarse: instalación de un clic desde la Store, sin avisos ni certificados.

## ¿Conviene mantener también el sideload?

Sí, durante la transición: deja las releases de GitHub (`.appinstaller` + `RitmoSetup.exe`)
para los que ya lo tienen instalado, y promociona la Store para los nuevos. Cuando la mayoría
haya migrado, puedes retirar el sideload.
