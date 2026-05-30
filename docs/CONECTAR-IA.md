# Conectar una IA a Ritmo (servidor MCP)

Ritmo incluye un **servidor MCP** (Model Context Protocol) que permite a una IA
—Claude Desktop, Claude Code u otra compatible con MCP— **configurar tu horario
y tus entornos de concentración** hablándole en lenguaje natural.

> Ejemplo: *"Créame la Fase 1 del 1 de junio al 31 de octubre y mete Técnico los
> lunes de 9 a 11 con aviso 10 minutos antes."*

## Qué puede hacer la IA

El servidor expone **40 herramientas** que cubren toda la configuración de la app:
inspeccionar el estado (`get_status`, `get_config`, `list_known_apps`), gestionar el
horario (fases, sesiones, provisionales, rango horario), Pomodoro y ritmos, notas y
atajos, entornos de concentración (apps a abrir/cerrar/silenciar, enlaces, tareas,
perfiles por tipo de sesión, entorno por defecto y por tipo de bloque), música
(Navidrome), calendarios ICS, prioridades de solapamiento e importar configuración.

> El listado completo y agrupado está en el [CHANGELOG](../CHANGELOG.md#-la-ia-servidor-mcp--40-herramientas).
> Empieza siempre por `get_config` para ver el estado con los ids e índices que usan las
> herramientas de editar/borrar.

Todo pasa por la misma capa validada que usa la app (`ConfigurationService`) y se guarda
en el `settings.json` compartido, en `%USERPROFILE%\.ritmo\settings.json`. Lo que
configure la IA lo ve la app y al revés. La **contraseña de Navidrome no se configura por
la IA** (vive en el almacén seguro del sistema; la introduces tú en la app).

## Requisitos

1. Tener **.NET 9** instalado.
2. Compilar el servidor una vez:
   ```powershell
   dotnet build src\Ritmo.Mcp\Ritmo.Mcp.csproj -c Release
   ```
   El ejecutable queda en
   `src\Ritmo.Mcp\bin\Release\net9.0\Ritmo.Mcp.dll`.

## Conectar Claude Desktop

Edita el archivo de configuración de Claude Desktop:

- Windows: `%APPDATA%\Claude\claude_desktop_config.json`

Añade Ritmo a `mcpServers` (ajusta la ruta del DLL a la tuya):

```json
{
  "mcpServers": {
    "ritmo": {
      "command": "dotnet",
      "args": ["C:\\Users\\luish\\Projects\\Ritmo\\src\\Ritmo.Mcp\\bin\\Release\\net9.0\\Ritmo.Mcp.dll"]
    }
  }
}
```

Reinicia Claude Desktop. Verás "ritmo" entre las herramientas (icono del enchufe 🔌).
Ya puedes pedirle que configure tu horario.

## Conectar Claude Code (CLI)

```powershell
claude mcp add ritmo -- dotnet "C:\Users\luish\Projects\Ritmo\src\Ritmo.Mcp\bin\Release\net9.0\Ritmo.Mcp.dll"
```

Comprueba con `claude mcp list` que aparece `ritmo`.

## Conectar otra IA / cliente MCP local

Cualquier cliente MCP que soporte transporte **stdio** sirve. Configúralo para
lanzar el comando:

```
dotnet "<ruta>\Ritmo.Mcp.dll"
```

El servidor habla MCP por entrada/salida estándar (stdio). No abre puertos de red:
es **100% local**, sin nube ni cuentas.

## Probarlo sin una IA

Hay un smoke test que actúa como cliente MCP real:

```powershell
dotnet run --project tests\Ritmo.Mcp.SmokeTest -- `
  "C:\Users\luish\Projects\Ritmo\src\Ritmo.Mcp\bin\Debug\net9.0\Ritmo.Mcp.dll"
```

Debe listar las herramientas (40), crear una fase de prueba y terminar con `SMOKE_OK`.

## Notas de seguridad

- El servidor es **local** (stdio), no escucha en ningún puerto de red.
- Solo modifica el `settings.json` de Ritmo; no toca otros archivos.
- Para pruebas puedes apuntar a otro archivo con la variable de entorno
  `RITMO_SETTINGS_PATH`.
