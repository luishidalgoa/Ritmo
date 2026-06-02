# Extensión de bloqueo de Ritmo

Bloqueo "duro" (a nivel de red) de las webs distractoras mientras Ritmo está en una
sesión de concentración. Complementa al bloqueo "blando" de Ritmo (que minimiza la
ventana del navegador al instante); esta extensión, además, impide que las páginas
bloqueadas carguen.

## Cómo funciona

- Ritmo expone su estado en un servidor local: `http://127.0.0.1:47615/state`
  → `{ "active": true|false, "domains": ["youtube.com", ...] }`.
- La extensión consulta ese estado y, cuando hay una sesión activa, bloquea esos
  dominios con `declarativeNetRequest`. Cuando Ritmo se cierra o termina la sesión,
  deja de bloquear automáticamente.

## Instalar en Microsoft Edge

1. Abre `edge://extensions`.
2. Activa **Modo de desarrollador** (abajo a la izquierda).
3. Pulsa **Cargar desempaquetada** y elige **esta carpeta**.

## Instalar en Google Chrome / Brave

1. Abre `chrome://extensions`.
2. Activa **Modo de desarrollador** (arriba a la derecha).
3. Pulsa **Cargar descomprimida** y elige **esta carpeta**.

> La lista de webs bloqueadas se configura por entorno dentro de Ritmo
> (Ajustes → Entornos → módulo de concentración).
