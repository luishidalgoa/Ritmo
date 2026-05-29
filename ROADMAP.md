# Roadmap de Ritmo

> Fuente de verdad viva: **GitHub Project #12** → https://github.com/users/luishidalgoa/projects/12/views/1
> Progreso por fase: **Milestones** → https://github.com/luishidalgoa/Ritmo/milestones
>
> Este archivo es el resumen legible; el estado real (Todo/In progress/Done) está en el Project.
> Las fechas son orientativas de un proyecto personal y se ajustan sobre la marcha.

## Hitos (milestones)

### ✅ M1 · Núcleo lógico — *completado*
La lógica pura, 100% testeada, sin dependencias de Windows.
- #1 Modelo de dominio + planificador semanal
- #2 Motor Pomodoro (#13 estados · #14 config · #15 tick · #16 control)
- **27 tests verdes.**

### 🔄 M2 · Servicio en segundo plano
El "motor" que vive en segundo plano y dispara los avisos/sesiones.
- #17 Abstracción de reloj y timers (`IClock` / `IScheduler`) ← *en progreso*
- #18 Bucle que consulta `GetNextEvent` y arma timers
- #19 Persistencia del horario y la config (JSON local)
- #20 Arranque con Windows y ejecución en bandeja

### ⬜ M3 · Interfaz WinUI 3
La cara visible, estilo Reloj de Windows 11.
- #4 Esqueleto + temporizador (#21 proyecto WinUI · #22 shell Mica · #23 vista temporizador · #24 bandeja)
- #5 Editor de horario semanal (#25 rejilla · #26 crear/editar sesión · #27 avisos por sesión)
- #6 Avisos previos configurables (#28 wiring UI)

### ⬜ M4 · Concentración e integración con el SO
Lo que "pone el foco" y silencia el ruido.
- #7 Modo concentración: No molestar + ocultar distractores (#29 · #30)
- #8 Bloqueo de webs en Edge (#31 lista · #32 aplicar/revertir)
- #9 Cerrar/silenciar apps de ruido (#33 lista · #34 acción)
- #10 Lanzar app de música (Aonsoku/Spotify)
- #11 Abrir/crear lista "Estudio" en Edge

### ⬜ M5 · Empaquetado y release
- #12 Empaquetado MSIX (#36 instalador · #37 autoarranque)

## Principios que guían el roadmap

1. **De dentro hacia fuera**: primero el núcleo testeable (M1), luego el servicio (M2),
   luego la UI (M3), después la integración con el SO (M4) y por último el empaquetado (M5).
2. **Cada pieza con sus tests** antes de darla por hecha (`dotnet test` verde).
3. **El Project manda**: si este archivo y el Project discrepan, gana el Project.
