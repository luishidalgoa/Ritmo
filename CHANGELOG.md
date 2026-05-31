# Changelog de Ritmo

Todo lo que Ritmo sabe hacer hoy, y qué se ha ido añadiendo. Formato inspirado en
[Keep a Changelog](https://keepachangelog.com/); fechas en `yyyy-MM-dd`.

Este archivo tiene **dos partes**:

1. **[Capacidades actuales](#capacidades-actuales)** — el inventario vivo de lo que
   está implementado, agrupado por área. Incluye la subsección **🤖 La IA (MCP)**
   con todo lo que una IA puede ver y configurar.
2. **[Registro de cambios](#registro-de-cambios)** — orden cronológico (lo más
   nuevo arriba). Aquí se anota cada mejora cuando se implementa.

> **Regla del harness** (ver `AGENTS.md`): al implementar una mejora o funcionalidad
> nueva, **se registra aquí**: una línea datada en el «Registro de cambios» con su
> nº de issue, y —si introduce o cambia una capacidad— se actualiza también la
> sección «Capacidades actuales» (incluida la subsección 🤖 IA/MCP si se tocan las
> herramientas del servidor).

---

## Capacidades actuales

### 🤖 La IA (servidor MCP) — 44 herramientas

Una IA compatible con MCP (Claude Desktop/Code u otra, 100% local por stdio) puede
**ver y configurar toda la app** hablándole en lenguaje natural. Todo pasa por la
fachada validada `ConfigurationService` (misma que usa la UI) y se guarda en el
`settings.json` compartido (`%USERPROFILE%\.ritmo\settings.json`), así que los
cambios de la IA los ve la app al instante y viceversa. Guía de conexión:
[`docs/CONECTAR-IA.md`](docs/CONECTAR-IA.md).

**Inspección (leer el estado)**
- `get_status` — resumen corto (fases, entornos, notas).
- `get_config` — **toda** la configuración en JSON, con los ids e índices que usan
  las herramientas de editar/borrar.
- `list_known_apps` — catálogo de apps conocidas con sus nombres de proceso válidos.

**Horario y plan**
- Fases: `add_phase`, `update_phase`, `remove_phase`.
- Sesiones recurrentes: `add_session`, `update_session`, `remove_session`.
- Sesiones provisionales (con fecha): `add_one_off_session`, `remove_one_off_session`.
- Rango horario, granularidad, vista previa y colores por tipo de bloque: `set_view_hours`, `set_view_granularity`, `set_day_preview`, `set_kind_color`.

**Pomodoro**
- Pomodoro por defecto: `set_pomodoro`.
- Ritmos propios: `add_rhythm`, `update_rhythm`, `remove_rhythm`.

**Entornos de concentración**
- Entorno: `upsert_focus_environment` (crear/editar; *merge* que conserva enlaces,
  tareas y perfiles), `remove_environment`, `set_default_environment`.
- Enlaces del entorno: `add_environment_link`, `remove_environment_link`.
- Tareas del entorno: `add_environment_task`, `toggle_environment_task`, `remove_environment_task`.
- Perfiles por tipo de sesión (qué se abre en cada bloque): `set_session_profile`, `clear_session_profile`.
- Mapeo tipo de bloque → entorno: `map_environment_to_kind`, `clear_environment_kind`.

**Notas y atajos**
- Notas (markdown, opcional post-it de sesión): `add_note`, `update_note`, `remove_note`.
- Atajos globales: `add_shortcut`, `remove_shortcut`.

**Notificaciones, música, calendarios y solapamientos**
- Notificaciones push al móvil (ntfy): `set_ntfy`.
- Navidrome (servidor + usuario; **sin contraseña**, esa va en el almacén seguro):
  `set_navidrome_connection`, `clear_navidrome_connection`.
- Calendarios externos ICS: `add_calendar_feed`, `remove_calendar_feed`.
- Prioridad ante solapamiento horario↔calendario: `set_overlap_priority`, `clear_overlap_priority`.

**Respaldo**
- `import_config` — reemplaza toda la configuración desde un JSON (formato de `get_config`).

### ⏱️ Pomodoro

- Motor de estados (Idle / Focus / Break / LongBreak) con tick que recibe el «ahora»
  (determinista y testeable). Pausar / reanudar / saltar / reiniciar. (#13–#16)
- Configuración de duraciones y ciclos (#14). Presets **Clásico** (25/5/15) y
  **Profundo** (50/10/20). **Ritmos propios** con nombre, creables en Ajustes y
  asignables a un entorno (#96).

### 🗓️ Horario semanal y plan por fases

- Modelo de **fases temporales** (`SchedulePhase` + `SchedulePlan`): de una fecha a
  otra rige un horario; al pasar la fecha límite entra la siguiente fase (#39, #119).
- **Gestor de fases** con acceso a versiones futuras/pasadas (#46).
- Sesiones: crear/editar/eliminar (día, hora, **hora de fin** en vez de duración, tipo) (#26, #80);
  crear el mismo bloque en **varios días a la vez** (#81); **fusión visual** de sesiones
  idénticas en días contiguos (#86); **arrastrar y redimensionar** estilo Excel sin solapar (#82, #88–#90).
- Bloques **tentativos** y tipo «Por definir»; sesiones **no-concentración** para ver la
  semana completa (#40, #63).
- **Sesión provisional**: superponer un bloque extraordinario solo en la semana de su fecha (#103).
- **Navegación entre semanas** + navegador de calendario mes/año (#113, #119).
- **Hoy resaltado** + **línea reactiva de la hora actual** en la columna de hoy (#69, #115).
- **Rejilla responsive**: las columnas de día llenan el ancho disponible (#117).
- **Granularidad de la rejilla configurable** (60/30/15 min, 60 por defecto): solo cambia las
  líneas-guía de fondo; los bloques se posicionan por su minuto real, así una hora irregular
  (p. ej. 16:40) se ve donde toca sin desalinear los demás días (#61).
- **Colores del horario configurables**: color de fondo personalizable por tipo de bloque
  (Técnico, Legislación, Inglés…) desde Ajustes; por defecto, la paleta tipo Excel (#45).
- **Panel lateral de detalle** de sesión y **resolución de solapamientos** con eventos del
  calendario (elegir qué lado prioriza) (#114).
- Avisos previos configurables por sesión (1 h / 10 min / 5 min, hasta 2; desplegables con
  variedad + personalizado) (#6, #27, #87).

### 🎯 Concentración (modo focus)

- Al concentrarse: **No molestar**, ocultar distractores, **cerrar/silenciar** apps de
  ruido, **bloquear webs** en Edge, **abrir apps** de estudio, **abrir un Workspace** de
  Edge, opción de **escritorio virtual nuevo** de Windows (#30, #32, #34, #35, #108–#110).
- **Isla flotante estilo StandBy**: al entrar en focus la app se minimiza y aparece una
  ventana arriba a la derecha con la **hora del sistema** en grande + mini-controles del
  Pomodoro (abrir en grande / pausar / reanudar / saltar) (#118).
- Concentrarse en un bloque concreto o con un entorno desde el panel derecho (#67, #69, #111).
- **Vista previa del día al iniciar concentración** (configurable, #47): al pulsar «Iniciar» se
  muestra un resumen de los bloques de hoy (orden, color por tipo, bloque actual resaltado) antes
  de concentrarse. Toggle en Ajustes y herramienta MCP `set_day_preview`.

### 🧰 Entornos de trabajo

- Modelo `FocusEnvironment` + persistencia + entorno por defecto (#51, #52).
- Gestor de entornos (crear/editar/elegir), creación y edición **desde el panel derecho**
  con nav desplegable, selector fácil del entorno activo (#53, #92, #102, #104).
- **Apps** por categoría con catálogo + detección de instaladas (#94, #97), elegibles desde un modal **«Conectores»** filtrable por categoría (#101).
- **Enlaces/herramientas** agrupados, **módulo de Tareas** por entorno (#74, #77).
- **Webs a bloquear** con favicon (#99); apps a cerrar clarificadas (#100).
- **Comportamiento por tipo de sesión**: qué apps/enlaces se abren en cada tipo de bloque (#70, #116).
- Asociar tipo de bloque → entorno (#70).

### 🎵 Música

- Lanzar app de música configurable (#10). Elegir entre apps instaladas (#98).
- **Navidrome** (API Subsonic) en vez de VLC; la contraseña vive en el almacén seguro
  del sistema, nunca en el JSON (#107).

### 📝 Notas

- Notas personalizables con **markdown** (#41, #72), como **post-its de sesión** en panel
  lateral (#73).

### 📅 Calendarios externos

- Suscripción a calendarios por enlace **ICS** (Google/Outlook/iCloud, solo lectura) e
  **import/export iCalendar** del horario (#44, #112).

### ⚙️ Ajustes, persistencia e infraestructura

- Pantalla de **Ajustes** central; editar notas y atajos desde Ajustes (#54, #55).
- Persistencia JSON local (plan, fases, notas, view-config, entornos) (#19, #43, #52).
- **Exportar/importar** configuración completa como respaldo (#56).
- **Servicio en segundo plano**: bucle de timers sobre el planificador, arranque con
  Windows y bandeja del sistema (#3, #18, #20).
- **Toasts** de Windows conectados a los avisos previos (#28, #29).
- **Notificaciones push al móvil vía ntfy** (opt-in, #122): cada aviso del horario se publica
  además en un topic de ntfy, y la app ntfy (Android/iOS) suscrita al topic lo recibe en el
  teléfono. Modo JSON (acentos/emoji intactos), configurable en Ajustes con botón "Enviar prueba".
- **Capa de comandos** `ConfigurationService` como punto único de validación para UI e IA (#57).

### 📚 Ayuda

- **Enciclopedia/wiki** con tooltips modernos sobre los términos (#93, #95).

### 🖥️ Plataforma

- App WinUI 3 (Fluent/Mica, estilo Reloj de Windows 11), sin login ni usuarios.
- Núcleo `Ritmo.Core` 100% puro y testeable (xUnit); UI y SO son *hosts* tontos.
- **Actualizaciones**: la app se publica y **auto-actualiza desde GitHub Releases** (MSIX firmado +
  `.appinstaller` vía App Installer; CD en GitHub Actions). Al actualizar, el botón **«Novedades»**
  abre un carrusel con las mejoras de la nueva versión a nivel usuario; en Ajustes hay
  **«Buscar actualizaciones»**.

---

## Registro de cambios

### 2026-05-31

- **#101 — Conectores: catálogo de apps por categoría en un modal.** El editor de entornos deja de
  mostrar el catálogo de apps inline (lo hacía muy largo): ahora hay un botón **«Conectores…»** que
  abre un popover **filtrable por categoría** (Productividad, Navegadores, Mensajería…) para elegir
  qué hace Ritmo con cada app (abrir/cerrar/silenciar). Reutiliza el catálogo `KnownApps` y el mismo
  estado; el editor muestra un resumen («Abrir N · Cerrar M · Silenciar K»). Es un `Flyout` (no un
  `ContentDialog`) porque el editor ya es uno y no se pueden anidar.
- **Novedades + base del sistema de actualizaciones (Fase 1).** Nuevo botón **«Novedades»** en el
  menú que se **activa (badge)** cuando la app se actualiza a una versión con notas nuevas; al
  pulsarlo abre un **carrusel** (FlipView + PipsPager) que explica las mejoras a nivel usuario.
  Núcleo puro `ReleaseNotes` (notas por versión + `Since`, con tests) + `AppSettings.LastSeenVersion`
  + `ConfigurationService.SetLastSeenVersion`. Regla de harness: cada feature de usuario añade su
  nota en `ReleaseNotes`.
- **Sistema de actualizaciones — Fases 2 y 3.** **CD** (`.github/workflows/release.yml`): en un tag
  `v*` compila + **firma** el MSIX (cert auto-firmado en *secrets*), genera el `.appinstaller`
  (auto-update nativo vía App Installer desde `releases/latest/download`) y publica la GitHub Release
  con `.msix`/`.appinstaller`/`.cer`. Script `tools/new-signing-cert.ps1` + guía
  `docs/INSTALAR-Y-ACTUALIZAR.md`. **Fix** del manifiesto (el `windows.startupTask` usaba tokens no
  sustituidos → MakeAppx fallaba). En Ajustes, **«Buscar actualizaciones»** (consulta la GitHub API,
  informativo). Verificado: el empaquetado+firma del MSIX funciona en local y la comprobación maneja
  el caso «sin releases». Falta que el mantenedor configure los *secrets* y publique el primer tag.
- **#45 — Color por tipo de bloque configurable** (completa el editor tipo Excel). `ScheduleColors`
  pasa a honrar `ViewConfig.ColorsByKind` (override estático refrescado antes de cada render); si un
  tipo no tiene color propio, usa el de por defecto. UI en Ajustes › «Colores del horario» (una fila
  por tipo con muestra + `ColorPicker` en flyout + «Usar por defecto»). Comando `SetKindColor` (con
  test, valida #RRGGBB) y herramienta MCP `set_kind_color`. La UI usa una **paleta curada propia**
  (`SchedulePalette`): rejilla de muestras en columnas por color y filas de mayor a menor intensidad
  (tintes derivados hacia blanco) + «Usar por defecto», en vez del ColorPicker genérico. Verificado en
  la app (Técnico→amarillo, Legislación→morado se reflejan en la rejilla; paleta visible al abrir).
- **#47 — Vista previa del día al iniciar concentración** (configurable). Al pulsar «Iniciar» en el
  temporizador, si está activada (por defecto sí), se muestra `DayPreviewDialog` con los bloques de hoy
  (horario de la fase + provisionales, ordenados, color por tipo, bloque actual resaltado y marca ✦ de
  provisional) antes de arrancar el foco. Toggle en Ajustes, comando `SetShowDayPreviewOnFocusStart` (con
  test) y herramienta MCP `set_day_preview`. Verificado en la app.
- **#123 — Conexiones con apps externas** (declutter de Ajustes, estilo conectores de Claude).
  - **Modal de descubrimiento** `ConnectionsDialog` ("Añadir conexión"): catálogo de lo conectable
    —notificaciones al móvil (ntfy) y calendarios OAuth ("Próximamente", futuro #112)—. "Conectar"
    crea la conexión (topic privado generado) y cierra; el modal es solo para descubrir/añadir.
  - **Gestión inline en Ajustes**: las conexiones YA creadas se ven y gestionan directamente en
    Ajustes › Conexiones (toggle activar/pausar, servidor, topic + Generar/Copiar, Enviar prueba,
    Eliminar), sin volver a abrir el modal. Si no hay ninguna, estado vacío con invitación.
  - **Guía visual tipo carrusel** `NtfyGuideDialog` (botón "Cómo conectar mi móvil"): pasos con
    ilustración + `PipsPager`, incl. enlaces de descarga por plataforma y el topic con copiar.
  Verificado en la app (descubrir → conectar → gestión inline → guía).
- **#122 — Notificaciones push al móvil vía ntfy** (opt-in, parte A del ticket). Cuando un aviso
  se dispara, además del toast de Windows se publica en ntfy (`{servidor}/{topic}`, modo JSON) y
  el móvil suscrito al topic lo recibe. Núcleo puro `NtfyPublish` (con tests) decide el QUÉ; el host
  `NtfyPublisher` hace el POST. Sección en Ajustes (activar + servidor + topic + "Generar" + "Enviar
  prueba") y herramienta MCP `set_ntfy`. Por defecto desactivado (Ritmo sigue 100% local). Verificado
  end-to-end contra ntfy.sh: el suscriptor recibe título/mensaje/prioridad/tags con UTF-8 intacto.
- **#61 — Granularidad de la rejilla del horario configurable.** Selector 60/30/15 min en
  Ajustes (60 por defecto). La granularidad solo dibuja las líneas-guía de fondo; los bloques
  pasan a posicionarse por su **minuto real** (geometría pura `ScheduleGeometry`, con tests),
  así un bloque a las 16:40 se ve donde toca sin desalinear los demás días. El arrastre se
  ajusta a la rejilla activa. Nueva herramienta MCP `set_view_granularity`. Verificado en la
  app (captura: 16:40 entre las líneas de 16:00 y 17:00).
- **#120 — Control total de la configuración desde la IA (MCP).** `RitmoTools` pasa de
  6 a **40 herramientas**, cubriendo toda la superficie de `ConfigurationService`: la IA
  puede ver (`get_config`/`list_known_apps`) y configurar fases, sesiones, provisionales,
  Pomodoro y ritmos, rango horario, notas, atajos, entornos (apps/enlaces/tareas/perfiles),
  Navidrome, calendarios, solapamientos e importar. Verificado por `tools/call` real.
- **#65 (bug) — La IA y la app comparten `settings.json`.** La ruta pasa a
  `%USERPROFILE%\.ritmo\settings.json` (fuera de la redirección MSIX) con migración
  automática de la configuración antigua, para que la app empaquetada y el servidor MCP
  lean/escriban el mismo archivo.
- **#106 — cancelado.** Se descarta el login OAuth de Spotify; la música se queda con Navidrome.

### Base inicial (hasta 2026-05-30)

Primera gran tanda de funcionalidad (milestones M1–M5): núcleo + planificador, motor
Pomodoro, servicio en segundo plano, UI WinUI 3 completa (horario, entornos, ajustes,
notas, isla de concentración), integración con el SO (No molestar, Edge, apps, música,
calendarios) y el servidor MCP. El detalle por funcionalidad está arriba, en
**Capacidades actuales**, con el nº de issue de cada una; el histórico completo de issues
cerrados y sus milestones vive en GitHub (Project #12).
