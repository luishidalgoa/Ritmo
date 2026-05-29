# GitHub Project — Ritmo project (#12)

Fuente de verdad de las tareas. Vista: https://github.com/users/luishidalgoa/projects/12/views/1

## IDs para automatización con `gh`

| Clave | Valor |
|---|---|
| Project number | `12` |
| Owner | `luishidalgoa` |
| Project ID | `PVT_kwHOBxkAFc4BZKGC` |
| Status field ID | `PVTSSF_lAHOBxkAFc4BZKGCzhUKyts` |
| Option · Todo | `f75ad846` |
| Option · In progress | `47fc9ee4` |
| Option · Done | `98236657` |

## Recetas

Ver tareas:
```powershell
gh project item-list 12 --owner luishidalgoa --format json
```

Añadir un issue nuevo al Project:
```powershell
gh project item-add 12 --owner luishidalgoa --url https://github.com/luishidalgoa/Ritmo/issues/<N>
```

Cambiar el Status de un item (necesita el item-id DENTRO del project, no el nº de issue):
```powershell
# 1) localizar el item-id por la URL del issue
gh project item-list 12 --owner luishidalgoa --format json
# 2) fijar estado (ej. In progress)
gh project item-edit --id <ITEM_ID> --project-id PVT_kwHOBxkAFc4BZKGC `
  --field-id PVTSSF_lAHOBxkAFc4BZKGCzhUKyts --single-select-option-id 47fc9ee4
```

> Convención: una tarea empieza → **In progress**; se cierra → **Done** (y `gh issue close` con comentario), siempre con OK del usuario para cerrar.
