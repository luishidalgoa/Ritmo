using System;
using Ritmo.Core.Model;
using Ritmo.Core.Notifications;
using Ritmo.Core.Scheduling;

namespace Ritmo.Core.Tests;

public class NotificationBuilderTests
{
    private static StudySession Session(string title = "Bloque B.II", StudyKind kind = StudyKind.Tecnico) => new()
    {
        Title = title,
        Day = DayOfWeek.Monday,
        Start = new TimeOnly(9, 0),
        Duration = TimeSpan.FromHours(2),
        Kind = kind
    };

    private static PlannedEvent PreAlert(int minutesBefore, StudySession? s = null)
    {
        s ??= Session();
        var start = new DateTime(2026, 6, 1, 9, 0, 0);
        return new PlannedEvent
        {
            At = start - TimeSpan.FromMinutes(minutesBefore),
            Type = PlannedEventType.PreAlert,
            Session = s,
            MinutesBefore = minutesBefore,
            SessionStartAt = start
        };
    }

    private static PlannedEvent Start(StudySession? s = null)
    {
        s ??= Session();
        var start = new DateTime(2026, 6, 1, 9, 0, 0);
        return new PlannedEvent
        {
            At = start,
            Type = PlannedEventType.SessionStart,
            Session = s,
            SessionStartAt = start
        };
    }

    [Theory]
    [InlineData(60, "Tu sesión empieza en 1 hora")]
    [InlineData(120, "Tu sesión empieza en 2 horas")]
    [InlineData(10, "Tu sesión empieza en 10 minutos")]
    [InlineData(5, "Tu sesión empieza en 5 minutos")]
    [InlineData(1, "Tu sesión empieza en 1 minuto")]
    [InlineData(0, "Tu sesión está por empezar")]
    public void Titular_del_aviso_segun_minutos(int min, string expected)
        => Assert.Equal(expected, NotificationBuilder.PreAlertHeadline(min));

    [Fact]
    public void Aviso_previo_incluye_tipo_titulo_y_hora()
    {
        var msg = NotificationBuilder.ForEvent(PreAlert(10));

        Assert.Equal("Tu sesión empieza en 10 minutos", msg.Title);
        Assert.Contains("Técnico", msg.Body);
        Assert.Contains("Bloque B.II", msg.Body);
        Assert.Contains("09:00", msg.Body);
    }

    [Fact]
    public void Inicio_de_sesion_invita_a_concentrarse()
    {
        var msg = NotificationBuilder.ForEvent(Start());

        Assert.Equal("Es la hora de concentrarte", msg.Title);
        Assert.Contains("Técnico", msg.Body);
        Assert.Contains("09:00", msg.Body);
    }

    [Fact]
    public void Usa_la_etiqueta_legible_del_tipo()
    {
        var msg = NotificationBuilder.ForEvent(Start(Session(kind: StudyKind.Legislacion)));
        Assert.Contains("Legislación", msg.Body);
    }

    [Fact]
    public void Sin_titulo_cae_en_la_etiqueta_del_tipo()
    {
        var msg = NotificationBuilder.ForEvent(Start(Session(title: "   ", kind: StudyKind.Simulacro)));
        Assert.Contains("Simulacro", msg.Body);
    }

    [Fact]
    public void Tag_distingue_aviso_de_inicio_y_minutos()
    {
        var pre10 = NotificationBuilder.ForEvent(PreAlert(10)).Tag;
        var pre5 = NotificationBuilder.ForEvent(PreAlert(5)).Tag;
        var start = NotificationBuilder.ForEvent(Start()).Tag;

        Assert.NotEqual(pre10, pre5);     // distinto aviso -> distinto tag
        Assert.NotEqual(pre10, start);    // aviso != inicio
        Assert.StartsWith("prealert-", pre10);
        Assert.StartsWith("start-", start);
    }

    [Fact]
    public void Tag_es_estable_para_la_misma_ocurrencia()
    {
        // Re-disparar el MISMO evento debe dar el mismo tag (para reemplazar, no apilar).
        Assert.Equal(
            NotificationBuilder.ForEvent(PreAlert(10)).Tag,
            NotificationBuilder.ForEvent(PreAlert(10)).Tag);
    }

    [Theory]
    [InlineData(StudyKind.Tecnico, "Técnico")]
    [InlineData(StudyKind.Legislacion, "Legislación")]
    [InlineData(StudyKind.Ingles, "Inglés")]
    [InlineData(StudyKind.Descanso, "Descanso")]
    [InlineData(StudyKind.PorDefinir, "Por definir")]
    [InlineData(StudyKind.Personal, "Personal")]
    public void Label_traduce_el_tipo(StudyKind kind, string expected)
        => Assert.Equal(expected, kind.Label());
}
