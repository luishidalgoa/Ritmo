using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Text.Json;
using System.Threading;

namespace Ritmo_App.Services;

/// <summary>
/// Servidor local mínimo (127.0.0.1) que publica el estado del bloqueo para la EXTENSIÓN de
/// navegador (#8, bloqueo "duro" a nivel de red). La extensión hace polling de GET /state y, si
/// <c>active=true</c>, bloquea esos dominios con declarativeNetRequest; si Ritmo no responde,
/// no bloquea (fail-open). Solo loopback. Ritmo es app de plena confianza (MSIX runFullTrust),
/// así que puede escuchar aquí sin la restricción de loopback del AppContainer.
///
/// Complementa al bloqueo "blando" (DistractionGuard): este corta a nivel de red (robusto) pero
/// tarda unos segundos en sincronizar; el blando minimiza al instante.
/// </summary>
internal static class BlockStateServer
{
    public const int Port = 47615;

    private static TcpListener? _listener;
    private static Thread? _thread;
    private static volatile bool _running;
    private static volatile string _json = "{\"active\":false,\"domains\":[]}";

    public static void Start()
    {
        if (_running) return;
        try
        {
            _listener = new TcpListener(IPAddress.Loopback, Port);
            _listener.Start();
            _running = true;
            _thread = new Thread(AcceptLoop) { IsBackground = true, Name = "RitmoBlockState" };
            _thread.Start();
        }
        catch { _running = false; /* puerto ocupado / fallo: el bloqueo blando sigue cubriendo */ }
    }

    public static void Stop()
    {
        _running = false;
        try { _listener?.Stop(); } catch { }
        _listener = null;
    }

    /// <summary>Actualiza el estado que se sirve a la extensión.</summary>
    public static void SetState(bool active, IReadOnlyList<string> domains)
    {
        try { _json = JsonSerializer.Serialize(new { active, domains = domains ?? Array.Empty<string>() as IReadOnlyList<string> }); }
        catch { /* best-effort */ }
    }

    private static void AcceptLoop()
    {
        while (_running && _listener is not null)
        {
            try
            {
                using var client = _listener.AcceptTcpClient();
                using var stream = client.GetStream();
                // Lee y descarta la petición; siempre respondemos el estado actual.
                try { stream.ReadTimeout = 500; var _ = stream.Read(new byte[1024], 0, 1024); } catch { }

                var body = Encoding.UTF8.GetBytes(_json);
                var header =
                    "HTTP/1.1 200 OK\r\n" +
                    "Content-Type: application/json\r\n" +
                    "Access-Control-Allow-Origin: *\r\n" +
                    "Cache-Control: no-store\r\n" +
                    $"Content-Length: {body.Length}\r\n" +
                    "Connection: close\r\n\r\n";
                var hb = Encoding.ASCII.GetBytes(header);
                stream.Write(hb, 0, hb.Length);
                stream.Write(body, 0, body.Length);
                stream.Flush();
            }
            catch { if (!_running) break; /* listener cerrado: salir; otro fallo: seguir */ }
        }
    }
}
