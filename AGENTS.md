# Ritmo — contexto del proyecto (harness para agentes)

> **App de escritorio Windows 11** para preparar oposiciones con técnica Pomodoro
> y horario semanal. En segundo plano, look moderno (WinUI 3, estilo Reloj de
> Windows 11), **sin login ni usuarios**: se instala en el sistema y ya.

## ⚑ Al empezar CUALQUIER sesión nueva — haz esto PRIMERO

1. **Revisa el tablero de tareas** (fuente de verdad), antes de tocar código:
   - Project: **Ritmo project** → https://github.com/users/luishidalgoa/projects/12/views/1
   - Comando: `gh project item-list 12 --owner luishidalgoa --format json`
   - Mira qué está en **In progress** (eso es lo que continúa) y qué hay en **Todo**.
   - **Roadmap por fases**: ver `ROADMAP.md` y los Milestones
     (`gh api repos/luishidalgoa/Ritmo/milestones?state=all`). Cada issue está
     asignada a su milestone (M1…M5); trabaja en orden de milestone salvo que el
     usuario diga otra cosa.
2. **Toda tarea sale de un issue del Project.** Si no existe, créalo y añádelo al
   Project ANTES de programar. Pásalo a *In progress* al empezar y a *Done* al cerrar.
3. **No hagas `git push` ni cierres issues sin OK explícito del usuario.**

## Reglas de trabajo (innegociables)

- **Demostrar con tests.** Cada pieza de lógica se acompaña de tests que se
  ejecutan y se ven pasar (`dotnet test`). El usuario lo exige: nada se da por
  bueno sin verlo verde. Verificar = correr, no suponer.
- **Núcleo puro, hosts tontos.** La lógica (horario, Pomodoro, avisos) vive en
  `Ritmo.Core` SIN dependencias de Windows → 100% testeable. La UI (WinUI) y la
  integración con el SO son *hosts* que consumen el núcleo. No metas lógica de
  negocio en la UI.
- **Determinismo testeable.** Las piezas que dependen del tiempo reciben el
  "ahora" como parámetro (no leen el reloj por dentro). Así se testean sin esperar.
- **Idioma.** Código y nombres en inglés; comentarios y commits en español.
- **Documenta lo no obvio.** Siempre que añadas algo que no sea evidente para el
  usuario, regístralo en la enciclopedia y explícalo con un tooltip de ayuda:
  1. Añade/ajusta la entrada en el glosario del núcleo
     (`src/Ritmo.Core/Help/Glossary.cs`) → aparece en la página **Ayuda** (wiki).
  2. Pon un tooltip ⓘ junto al término en la UI con `HelpHint.Icon(clave)` /
     `HelpHint.Attach(elemento, clave)` / `HelpHint.Header(texto, clave)`.
  El sistema de tooltips debe verse **moderno y legible** (título + descripción,
  ancho acotado). Conceptos triviales no hace falta documentarlos.
- **Commits** terminan con la línea de co-autoría que pida el harness global.

## Arquitectura

| Proyecto | Rol | Testeable |
|---|---|---|
| `src/Ritmo.Core` | Lógica pura: modelo, planificador semanal, Pomodoro, avisos. | ✅ 100% |
| `tests/Ritmo.Core.Tests` | Tests xUnit del núcleo. | — |
| `src/Ritmo.App` *(pendiente)* | UI WinUI 3 + integración SO (No molestar, Edge, música…). | parcial |

Decisión clave sobre el "modo concentración": Windows 11 **no expone API pública**
para iniciar el Focus nativo → la app **replica el efecto** (No molestar vía API de
notificaciones + ocultar distractores + cerrar/silenciar ruido). No dependemos del
Focus del SO.

## Entorno

- **OS**: Windows 11 Home (build 26200). Shell: PowerShell 7.
- **.NET 9 SDK** (9.0.314) + plantillas WinUI 3 (`Microsoft.WindowsAppSDK.WinUI.CSharp.Templates`).
- `gh` CLI autenticado como `luishidalgoa` (scope `project` disponible).
- Repo: https://github.com/luishidalgoa/Ritmo  ·  rama `main`.
- IDs del Project para automatización: ver `.github/PROJECT.md`.

## Comandos frecuentes

```powershell
dotnet test                      # ejecuta los tests del núcleo (deben quedar verdes)
dotnet build                     # compila la solución
gh project item-list 12 --owner luishidalgoa --format json   # ver tareas
gh issue list --repo luishidalgoa/Ritmo                      # ver issues
```

## Estado (resumen; el detalle vivo está en el Project)

- ✅ Núcleo: modelo + planificador semanal (avisos previos, sesión activa, medianoche). 11 tests verdes.
- 🔄 Motor Pomodoro (en progreso / siguiente).
- ⬜ Servicio en segundo plano · UI WinUI 3 · integración SO · MSIX. Ver Project #12.
