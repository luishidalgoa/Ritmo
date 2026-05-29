using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Ritmo.Core.Commands;
using Ritmo.Core.Persistence;
using Ritmo.Mcp;

// Servidor MCP de Ritmo por stdio: una IA (Claude Desktop/Code u otra) se conecta
// y usa las herramientas para configurar el horario y los entornos de focus.
//
// IMPORTANTE: en transporte stdio, la salida estándar (stdout) ES el canal del
// protocolo MCP. Cualquier log debe ir a stderr, nunca a stdout.

var builder = Host.CreateApplicationBuilder(args);

// Logs a stderr (stdout queda reservado para el protocolo MCP).
builder.Logging.AddConsole(o => o.LogToStandardErrorThreshold = LogLevel.Trace);

// El servidor comparte el MISMO settings.json que la app de escritorio,
// así lo que configure la IA lo ve la app y viceversa. Se puede sobreescribir la
// ruta con la variable de entorno RITMO_SETTINGS_PATH (útil para pruebas).
builder.Services.AddSingleton<ISettingsStore>(_ =>
{
    var custom = Environment.GetEnvironmentVariable("RITMO_SETTINGS_PATH");
    return string.IsNullOrWhiteSpace(custom)
        ? JsonSettingsStore.Default()
        : new JsonSettingsStore(custom);
});
builder.Services.AddSingleton<ConfigurationService>();

builder.Services
    .AddMcpServer()
    .WithStdioServerTransport()
    .WithTools<RitmoTools>();

await builder.Build().RunAsync();
