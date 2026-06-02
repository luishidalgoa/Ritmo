using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Xml.Linq;

namespace Ritmo.Core.Sync;

/// <summary>
/// Una respuesta de un recurso CalDAV (un &lt;response&gt; del multistatus): su href + propiedades
/// que nos interesan para Recordatorios de Apple (#64).
/// </summary>
public sealed record CalDavResource(
    string Href,
    string? Etag = null,
    string? DisplayName = null,
    string? CalendarData = null,
    bool SupportsVTodo = false);

/// <summary>
/// Parseo PURO de las respuestas WebDAV/CalDAV (XML "multistatus", DAV:/CalDAV namespaces) que usa la
/// sincronización con Recordatorios de Apple vía iCloud CalDAV (#64). Sin red: testable con XML de muestra.
/// </summary>
public static class CalDavXml
{
    private static readonly XNamespace D = "DAV:";
    private static readonly XNamespace C = "urn:ietf:params:xml:ns:caldav";

    /// <summary>Extrae el href del &lt;current-user-principal&gt; de un PROPFIND inicial.</summary>
    public static string? CurrentUserPrincipal(string xml)
        => FirstHrefUnder(xml, D + "current-user-principal");

    /// <summary>Extrae el href del &lt;calendar-home-set&gt; del principal.</summary>
    public static string? CalendarHomeSet(string xml)
        => FirstHrefUnder(xml, C + "calendar-home-set");

    private static string? FirstHrefUnder(string xml, XName parent)
    {
        var doc = TryParse(xml);
        if (doc is null) return null;
        return doc.Descendants(parent)
                  .Descendants(D + "href")
                  .Select(h => h.Value.Trim())
                  .FirstOrDefault(v => !string.IsNullOrEmpty(v));
    }

    /// <summary>
    /// Parsea un &lt;multistatus&gt; en recursos: href + etag + displayname + calendar-data, y si la
    /// colección soporta el componente VTODO (las listas de Recordatorios). Recursos sin propiedades
    /// 200 OK se devuelven con lo que haya.
    /// </summary>
    public static IReadOnlyList<CalDavResource> ParseMultistatus(string xml)
    {
        var doc = TryParse(xml);
        var result = new List<CalDavResource>();
        if (doc is null) return result;

        foreach (var resp in doc.Descendants(D + "response"))
        {
            var href = resp.Elements(D + "href").Select(h => h.Value.Trim()).FirstOrDefault();
            if (string.IsNullOrEmpty(href)) continue;

            string? etag = null, displayName = null, calData = null;
            bool vtodo = false;

            foreach (var prop in resp.Descendants(D + "prop"))
            {
                if (prop.Element(D + "getetag") is { } et) etag = et.Value.Trim();
                if (prop.Element(D + "displayname") is { } dn) displayName = dn.Value;
                if (prop.Element(C + "calendar-data") is { } cd) calData = cd.Value;
                var compSet = prop.Element(C + "supported-calendar-component-set");
                if (compSet is not null)
                    vtodo = compSet.Elements(C + "comp")
                                   .Any(c => string.Equals((string?)c.Attribute("name"), "VTODO", StringComparison.OrdinalIgnoreCase));
            }

            result.Add(new CalDavResource(href, etag, displayName, calData, vtodo));
        }
        return result;
    }

    private static XDocument? TryParse(string xml)
    {
        try { return XDocument.Parse(xml); }
        catch { return null; }
    }
}

/// <summary>
/// Construcción y parseo PUROS de tareas iCalendar VTODO (RFC 5545) para Recordatorios de Apple (#64).
/// Solo los campos que sincronizamos: UID, SUMMARY (texto), STATUS (hecho/no), LAST-MODIFIED.
/// </summary>
public static class IcalTodo
{
    /// <summary>Escapa un valor de texto iCalendar (RFC 5545 §3.3.11).</summary>
    public static string Escape(string s) => (s ?? "")
        .Replace("\\", "\\\\").Replace(";", "\\;").Replace(",", "\\,")
        .Replace("\r\n", "\\n").Replace("\n", "\\n").Replace("\r", "\\n");

    /// <summary>Invierte <see cref="Escape"/>.</summary>
    public static string Unescape(string s)
    {
        if (string.IsNullOrEmpty(s)) return "";
        var sb = new StringBuilder(s.Length);
        for (int i = 0; i < s.Length; i++)
        {
            if (s[i] == '\\' && i + 1 < s.Length)
            {
                char next = s[++i];
                sb.Append(next switch { 'n' or 'N' => '\n', _ => next });
            }
            else sb.Append(s[i]);
        }
        return sb.ToString();
    }

    /// <summary>
    /// Construye un VCALENDAR con un único VTODO. <paramref name="dtstampUtc"/> en formato iCal
    /// (yyyyMMddTHHmmssZ); se pasa desde fuera para mantener esto puro/testable.
    /// </summary>
    public static string Build(string uid, string summary, bool done, string dtstampUtc)
    {
        var sb = new StringBuilder();
        sb.Append("BEGIN:VCALENDAR\r\n");
        sb.Append("VERSION:2.0\r\n");
        sb.Append("PRODID:-//Ritmo//ES\r\n");
        sb.Append("BEGIN:VTODO\r\n");
        sb.Append("UID:").Append(uid).Append("\r\n");
        sb.Append("DTSTAMP:").Append(dtstampUtc).Append("\r\n");
        sb.Append("SUMMARY:").Append(Escape(summary)).Append("\r\n");
        sb.Append("STATUS:").Append(done ? "COMPLETED" : "NEEDS-ACTION").Append("\r\n");
        if (done) sb.Append("PERCENT-COMPLETE:100\r\n");
        sb.Append("END:VTODO\r\n");
        sb.Append("END:VCALENDAR\r\n");
        return sb.ToString();
    }

    /// <summary>
    /// Parsea el primer VTODO de un texto iCalendar. Devuelve null si no hay VTODO. Hace "unfolding"
    /// de líneas plegadas (continuaciones que empiezan por espacio/tab, RFC 5545 §3.1).
    /// </summary>
    public static (string Uid, string Summary, bool Done, string? LastModified)? Parse(string ics)
    {
        if (string.IsNullOrEmpty(ics)) return null;
        var lines = Unfold(ics);
        bool inTodo = false;
        string uid = "", summary = "", lastMod = null!;
        bool done = false;
        foreach (var raw in lines)
        {
            var line = raw;
            if (line.StartsWith("BEGIN:VTODO", StringComparison.OrdinalIgnoreCase)) { inTodo = true; continue; }
            if (line.StartsWith("END:VTODO", StringComparison.OrdinalIgnoreCase)) break;
            if (!inTodo) continue;

            var (name, value) = SplitProp(line);
            switch (name.ToUpperInvariant())
            {
                case "UID": uid = value; break;
                case "SUMMARY": summary = Unescape(value); break;
                case "STATUS": done = value.Trim().Equals("COMPLETED", StringComparison.OrdinalIgnoreCase); break;
                case "COMPLETED": done = true; break;
                case "LAST-MODIFIED": lastMod = value.Trim(); break;
            }
        }
        return inTodo || uid.Length > 0 || summary.Length > 0
            ? (uid, summary, done, string.IsNullOrEmpty(lastMod) ? null : lastMod)
            : null;
    }

    private static (string Name, string Value) SplitProp(string line)
    {
        int colon = line.IndexOf(':');
        if (colon < 0) return (line, "");
        var name = line[..colon];
        int semi = name.IndexOf(';');          // ignora parámetros (SUMMARY;LANGUAGE=es:..)
        if (semi >= 0) name = name[..semi];
        return (name, line[(colon + 1)..]);
    }

    private static List<string> Unfold(string ics)
    {
        var rawLines = ics.Replace("\r\n", "\n").Replace("\r", "\n").Split('\n');
        var lines = new List<string>();
        foreach (var l in rawLines)
        {
            if (l.Length > 0 && (l[0] == ' ' || l[0] == '\t') && lines.Count > 0)
                lines[^1] += l[1..];           // continuación de la línea anterior
            else lines.Add(l);
        }
        return lines;
    }
}
