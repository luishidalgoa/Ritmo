using ModelContextProtocol.Client;

// Smoke test: arranca el servidor Ritmo.Mcp como un cliente MCP real (igual que
// haría Claude) y verifica que lista las tools y que get_status responde.
// Uso: dotnet run --project tests/Ritmo.Mcp.SmokeTest -- <ruta-al-Ritmo.Mcp.dll>

var dll = args.Length > 0
    ? args[0]
    : Path.GetFullPath(Path.Combine(AppContext.BaseDirectory,
        "..", "..", "..", "..", "..", "src", "Ritmo.Mcp", "bin", "Debug", "net9.0", "Ritmo.Mcp.dll"));

Console.WriteLine($"Servidor: {dll}");

// Archivo de settings temporal para no tocar la config real del usuario.
var tmpSettings = Path.Combine(Path.GetTempPath(), "RitmoSmoke_" + Guid.NewGuid().ToString("N"), "settings.json");

var transport = new StdioClientTransport(new StdioClientTransportOptions
{
    Name = "Ritmo",
    Command = "dotnet",
    Arguments = [dll],
    EnvironmentVariables = new Dictionary<string, string?> { ["RITMO_SETTINGS_PATH"] = tmpSettings }
});

await using var client = await McpClient.CreateAsync(transport);

var tools = await client.ListToolsAsync();
Console.WriteLine($"TOOLS ({tools.Count}): {string.Join(", ", tools.Select(t => t.Name))}");

// Invocar get_status para probar el ciclo completo.
var status = await client.CallToolAsync("get_status");
var text = status.Content.OfType<ModelContextProtocol.Protocol.TextContentBlock>().FirstOrDefault()?.Text;
Console.WriteLine($"get_status -> {text}");

// Ciclo end-to-end: la "IA" crea una fase y comprueba que aparece en el estado.
var marker = "Fase prueba " + Guid.NewGuid().ToString("N")[..6];
var add = await client.CallToolAsync("add_phase", new Dictionary<string, object?>
{
    ["name"] = marker,
    ["validFrom"] = "2026-06-01",
    ["validTo"] = "2026-10-31"
});
var addText = add.Content.OfType<ModelContextProtocol.Protocol.TextContentBlock>().FirstOrDefault()?.Text;
Console.WriteLine($"add_phase -> {addText}");

var status2 = await client.CallToolAsync("get_status");
var text2 = status2.Content.OfType<ModelContextProtocol.Protocol.TextContentBlock>().FirstOrDefault()?.Text ?? "";
Console.WriteLine($"get_status -> {text2}");

var ok = tools.Count >= 6 && text is not null
         && (addText?.StartsWith("OK") ?? false)
         && text2.Contains(marker);
Console.WriteLine(ok ? "SMOKE_OK" : "SMOKE_FAIL");
Environment.Exit(ok ? 0 : 1);
