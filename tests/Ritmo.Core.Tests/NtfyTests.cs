using System.Text.Json;
using Ritmo.Core.Commands;
using Ritmo.Core.Notifications;
using Ritmo.Core.Scheduling;

namespace Ritmo.Core.Tests;

/// <summary>Push al móvil vía ntfy (#122): builder puro de la publicación + comando de config.</summary>
public class NtfyTests
{
    [Theory]
    [InlineData(null, "https://ntfy.sh")]
    [InlineData("", "https://ntfy.sh")]
    [InlineData("   ", "https://ntfy.sh")]
    [InlineData("https://ntfy.sh/", "https://ntfy.sh")]
    [InlineData("https://push.midominio.com/", "https://push.midominio.com")]
    [InlineData("  https://push.midominio.com  ", "https://push.midominio.com")]
    public void NormalizeServer(string? input, string expected) =>
        Assert.Equal(expected, NtfyPublish.NormalizeServer(input));

    private static NotificationMessage Msg(string title, string body) =>
        new() { Title = title, Body = body, Tag = "t" };

    [Fact]
    public void For_publica_a_la_raiz_con_el_topic_en_el_json()
    {
        var pub = NtfyPublish.For("https://ntfy.sh/", "ritmo-abc", Msg("Hola", "Mundo"), PlannedEventType.PreAlert);
        Assert.Equal("https://ntfy.sh", pub.Url);   // raíz, sin topic en la ruta (modo JSON)

        using var doc = JsonDocument.Parse(pub.JsonBody);
        var root = doc.RootElement;
        Assert.Equal("ritmo-abc", root.GetProperty("topic").GetString());
        Assert.Equal("Hola", root.GetProperty("title").GetString());
        Assert.Equal("Mundo", root.GetProperty("message").GetString());
    }

    [Fact]
    public void El_inicio_de_sesion_va_con_prioridad_alta_y_el_aviso_previo_normal()
    {
        var start = NtfyPublish.For(null, "t", Msg("a", "b"), PlannedEventType.SessionStart);
        var pre = NtfyPublish.For(null, "t", Msg("a", "b"), PlannedEventType.PreAlert);
        Assert.Equal(4, JsonDocument.Parse(start.JsonBody).RootElement.GetProperty("priority").GetInt32());
        Assert.Equal(3, JsonDocument.Parse(pre.JsonBody).RootElement.GetProperty("priority").GetInt32());
    }

    [Fact]
    public void Los_acentos_viajan_literales_en_utf8()
    {
        var pub = NtfyPublish.For(null, "t", Msg("Es la hora de concentración", "Inglés · 09:00"), PlannedEventType.SessionStart);
        // UnsafeRelaxedJsonEscaping -> sin secuencias \u; el texto va literal.
        Assert.Contains("concentración", pub.JsonBody);
        Assert.Contains("Inglés", pub.JsonBody);
        Assert.DoesNotContain("\\u", pub.JsonBody);
    }

    // ---------- Comando ----------
    private static ConfigurationService New() => new(new InMemorySettingsStore());

    [Fact]
    public void SetNtfy_activa_con_topic()
    {
        var svc = New();
        var r = svc.SetNtfy(true, "https://ntfy.sh", "mi-topic");
        Assert.True(r.Success);
        var s = svc.GetSettings();
        Assert.True(s.NtfyEnabled);
        Assert.Equal("mi-topic", s.NtfyTopic);
    }

    [Fact]
    public void SetNtfy_activar_sin_topic_falla()
    {
        var r = New().SetNtfy(true, "https://ntfy.sh", "");
        Assert.False(r.Success);
    }

    [Fact]
    public void SetNtfy_servidor_no_http_falla_al_activar()
    {
        var r = New().SetNtfy(true, "ftp://malo", "t");
        Assert.False(r.Success);
    }

    [Fact]
    public void SetNtfy_desactivar_no_exige_topic()
    {
        var svc = New();
        svc.SetNtfy(true, null, "t");
        var r = svc.SetNtfy(false, null, null);
        Assert.True(r.Success);
        Assert.False(svc.GetSettings().NtfyEnabled);
    }

    [Fact]
    public void La_config_ntfy_sobrevive_al_round_trip()
    {
        var svc = New();
        svc.SetNtfy(true, "https://push.example.com", "secreto-123");
        var json = svc.ExportJson();
        var svc2 = New();
        svc2.ImportJson(json);
        var s = svc2.GetSettings();
        Assert.True(s.NtfyEnabled);
        Assert.Equal("https://push.example.com", s.NtfyServerUrl);
        Assert.Equal("secreto-123", s.NtfyTopic);
    }
}
