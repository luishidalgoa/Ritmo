# Ritmo

App de escritorio (Windows 11) para preparar la oposición **TAI** con técnica Pomodoro y horario semanal.

App **en segundo plano**, look moderno (WinUI 3 / estilo Reloj de Windows 11), **sin inicio de sesión ni usuarios**: se instala en el sistema y listo.

## Qué hace (objetivo)

- ⏱️ **Pomodoro** con sesiones de concentración.
- 🗓️ **Horario semanal**: defines qué sesión toca a qué hora de qué día.
- 🔔 **Avisos previos configurables** (1 h / 10 min / 5 min antes… incluso 2 avisos por sesión).
- 🎯 **Modo concentración**: activa *No molestar*, oculta distractores y silencia el ruido.
- 🌐 Bloqueo de webs distractoras en Edge durante la sesión.
- 🤐 Cierra/silencia apps de ruido (Discord, juegos…).
- 🎵 Lanza tu app de música (Aonsoku, Spotify…), configurable.
- 📚 Abre (o crea) la lista de trabajo **"Estudio"** en Edge.

## Arquitectura

Núcleo puro y testeable, separado de la integración con el SO y la UI:

| Proyecto | Rol | Testeable |
|---|---|---|
| `src/Ritmo.Core` | Lógica pura: modelo, planificador de horario, Pomodoro, avisos. Sin dependencias de Windows. | ✅ 100% |
| `tests/Ritmo.Core.Tests` | Tests xUnit del núcleo. | — |
| `src/Ritmo.App` *(pendiente)* | UI WinUI 3 + integración con el SO (No molestar, Edge, música…). | parcial |

> Filosofía: **el cerebro de la app se prueba sin pantalla**. La UI y el SO se montan encima.

## Desarrollo

Requisitos: .NET 9 SDK, plantillas WinUI 3 (Windows App SDK).

```powershell
dotnet test          # ejecuta los tests del núcleo
dotnet build         # compila la solución
```

## Estado

El inventario vivo de lo implementado (y lo que la IA puede configurar) está en
**[CHANGELOG.md](CHANGELOG.md)** — la fuente única de «qué hace Ritmo hoy».

En síntesis, ya están: núcleo + planificador semanal por fases, motor Pomodoro,
servicio en segundo plano, UI WinUI 3 (horario, entornos, ajustes, notas, isla de
concentración), integración con el SO (No molestar, Edge, apps, música/Navidrome,
calendarios) y un **servidor MCP** para configurar la app desde una IA
(ver [docs/CONECTAR-IA.md](docs/CONECTAR-IA.md)).

> 🤖 Conectar una IA: [docs/CONECTAR-IA.md](docs/CONECTAR-IA.md).
