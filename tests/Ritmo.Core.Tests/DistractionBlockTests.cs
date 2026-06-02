using Ritmo.Core.Focus;

namespace Ritmo.Core.Tests;

public class DistractionBlockTests
{
    private static readonly string[] Blocked = { "youtube.com", "reddit.com", "instagram.com" };

    [Fact]
    public void Detecta_por_etiqueta_principal_en_el_titulo()
    {
        Assert.Equal("youtube.com", DistractionBlock.MatchedSite("(3) YouTube - Perfil 1 - Microsoft Edge", Blocked));
        Assert.Equal("reddit.com", DistractionBlock.MatchedSite("r/programming - Reddit — Google Chrome", Blocked));
    }

    [Fact]
    public void Detecta_dominio_literal_en_el_titulo()
        => Assert.Equal("youtube.com", DistractionBlock.MatchedSite("youtube.com/watch?v=abc", Blocked));

    [Fact]
    public void No_coincide_si_no_esta()
        => Assert.Null(DistractionBlock.MatchedSite("Documento de trabajo - Word", Blocked));

    [Fact]
    public void Titulo_vacio_o_nulo_no_coincide()
    {
        Assert.Null(DistractionBlock.MatchedSite("", Blocked));
        Assert.Null(DistractionBlock.MatchedSite(null, Blocked));
    }

    [Fact]
    public void No_falso_positivo_dentro_de_otra_palabra()
        // "redditor" no es "reddit" como palabra; "youtuber" no es "youtube".
        => Assert.Null(DistractionBlock.MatchedSite("Soy un youtuber profesional - Edge", new[] { "youtube.com" }));

    [Fact]
    public void Etiqueta_corta_no_dispara_falsos_positivos()
        // x.com → etiqueta "x" (len 1) se ignora; no debe marcar "Excel" ni "Inbox".
        => Assert.Null(DistractionBlock.MatchedSite("Inbox - Excel - Microsoft Edge", new[] { "x.com" }));

    [Fact]
    public void Normaliza_la_entrada_del_usuario()
        // El usuario puede escribir con esquema/www/ruta; igual debe casar.
        => Assert.Equal("youtube.com", DistractionBlock.MatchedSite("YouTube - Edge", new[] { "https://www.youtube.com/feed" }));

    [Fact]
    public void Insensible_a_mayusculas()
        => Assert.Equal("instagram.com", DistractionBlock.MatchedSite("INSTAGRAM - Edge", Blocked));
}
